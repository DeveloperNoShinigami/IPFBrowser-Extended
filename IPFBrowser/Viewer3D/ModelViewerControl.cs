// 3D Viewer Control using OpenTK/OpenGL
// Provides real-time 3D model preview for XAC and other formats

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace IPFBrowser.Viewer3D
{
    /// <summary>
    /// OpenGL-based 3D viewer control for rendering game models
    /// </summary>
    public class ModelViewerControl : UserControl
    {
        private GLControl _glControl;
        private bool _glInitialized = false;
        private string _initError = null;
        
        // Debug logging
        private static readonly string _logPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "ErrorLogs", "model_debug.log");
        
        private static void Log(string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(_logPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        // Camera
        private Vector3 _cameraPosition = new Vector3(0, 2, 5);
        private Vector3 _cameraTarget = new Vector3(0, 0, 0);
        private float _cameraDistance = 5.0f;
        private float _cameraYaw = (float)Math.PI; // Start facing front of model
        private float _cameraPitch = 0.3f;

        // Mouse control
        private Point _lastMousePos;
        private bool _isDragging = false;
        private MouseButtons _dragButton;
            private bool _cameraLocked = false; // Toggleable via right-click

        // Model data
        private List<RenderMesh> _meshes = new List<RenderMesh>();
        private List<RenderBone> _bones = new List<RenderBone>();
        private BoundingBox _modelBounds;
        private float _modelSize = 1.0f; // Approximate size of the model for projection calculations
        private bool _hasModel = false;
        private Dictionary<int, int> _textureIds = new Dictionary<int, int>(); // MaterialId -> OpenGL texture ID
        private List<MaterialInfo> _materials = new List<MaterialInfo>(); // Store material info for texture loading
        private int _attachmentStartIndex = -1; // Index where attachment meshes start in _meshes
        private List<string> _attachmentTextureNames = new List<string>(); // DiffuseTex names from attachment PROPERTIES
        
        // Parent skeleton for attachment models
        private FileFormats.XAC.XacFile _parentSkeleton = null;
        private Dictionary<string, Matrix4> _parentBoneTransforms = new Dictionary<string, Matrix4>();
        private bool _isAttachmentModel = false;
        private string _attachmentBoneName = null;

        // Rendering options
        public bool ShowWireframe { get; set; } = false;
        public bool ShowNormals { get; set; } = false;
        public bool ShowBones { get; set; } = false;
        public Color BackgroundColor { get; set; } = Color.FromArgb(40, 40, 45);
        
        /// <summary>
        /// Returns true if attachments have been added to the model
        /// </summary>
        public bool HasAttachments => _attachmentStartIndex >= 0 && _attachmentStartIndex < _meshes.Count;
        
        /// <summary>
        /// Gets the list of texture names from attachment PROPERTIES (DiffuseTex)
        /// </summary>
        public List<string> AttachmentTextureNames => _attachmentTextureNames;
        
        /// <summary>
        /// Returns true if OpenGL is successfully initialized and ready to render
        /// </summary>
        public bool IsOpenGLReady => _glInitialized && _glControl != null;
        
        /// <summary>
        /// Returns true if a model has been loaded
        /// </summary>
        public bool HasModel => _hasModel;
        
        /// <summary>
        /// Error message if OpenGL failed to initialize
        /// </summary>
        public string InitializationError => _initError;
        
        /// <summary>
        /// Gets the list of materials with their texture names for the current model
        /// </summary>
        public List<MaterialInfo> Materials => _materials;
        
        /// <summary>
        /// Returns true if the current model is an attachment (face, head, hair, etc.)
        /// </summary>
        public bool IsAttachmentModel => _isAttachmentModel;
        
        /// <summary>
        /// The name of the bone this attachment should attach to (e.g., "Bip01 Head")
        /// </summary>
        public string AttachmentBoneName => _attachmentBoneName;
        
        /// <summary>
        /// Returns true if a parent skeleton has been loaded
        /// </summary>
        public bool HasParentSkeleton => _parentSkeleton != null;
        
        // Required textures from XAC file
        private List<string> _requiredTextures = new List<string>();
        
        /// <summary>
        /// Gets the list of texture filenames required by the current model (from DiffuseTex properties)
        /// </summary>
        public List<string> RequiredTextures => _requiredTextures;
        
        // Animation state
        private FileFormats.XSM.XsmFile _currentAnimation = null;
        private float _animationTime = 0f;
        private bool _isAnimationPlaying = false;
        private System.Windows.Forms.Timer _animationTimer;
        private DateTime _lastAnimationUpdate;
        
        // Animation toolbar buttons
        private ToolStripButton _playAnimBtn;
        private ToolStripButton _stopAnimBtn;
        
        /// <summary>
        /// Returns true if an animation is currently loaded
        /// </summary>
        public bool HasAnimation => _currentAnimation != null;
        
        /// <summary>
        /// Returns true if animation is currently playing
        /// </summary>
        public bool IsAnimationPlaying => _isAnimationPlaying;

        // Events
        public event EventHandler<string> StatusChanged;
        public event EventHandler TexturesNeeded; // Fired when model is loaded and needs textures

        public ModelViewerControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Create toolbar
            var toolbar = new ToolStrip();
            
            var resetBtn = new ToolStripButton("Reset View");
            resetBtn.DisplayStyle = ToolStripItemDisplayStyle.Text;
            resetBtn.Click += (s, e) => ResetCamera();
            toolbar.Items.Add(resetBtn);
            
            toolbar.Items.Add(new ToolStripSeparator());
            
            var wireframeBtn = new ToolStripButton("Wireframe");
            wireframeBtn.DisplayStyle = ToolStripItemDisplayStyle.Text;
            wireframeBtn.CheckOnClick = true;
            wireframeBtn.Click += (s, e) => {
                ShowWireframe = wireframeBtn.Checked;
                Refresh();
            };
            toolbar.Items.Add(wireframeBtn);

            var bonesBtn = new ToolStripButton("Bones");
            bonesBtn.DisplayStyle = ToolStripItemDisplayStyle.Text;
            bonesBtn.CheckOnClick = true;
            bonesBtn.Checked = true;
            bonesBtn.Click += (s, e) => {
                ShowBones = bonesBtn.Checked;
                Refresh();
            };
            toolbar.Items.Add(bonesBtn);

            toolbar.Items.Add(new ToolStripSeparator());
            
            // Animation controls
            _playAnimBtn = new ToolStripButton("▶ Play");
            _playAnimBtn.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _playAnimBtn.Enabled = false;
            _playAnimBtn.Click += (s, e) => {
                if (_isAnimationPlaying)
                {
                    StopAnimation();
                    _playAnimBtn.Text = "▶ Play";
                }
                else
                {
                    PlayAnimation();
                    _playAnimBtn.Text = "⏸ Pause";
                }
            };
            toolbar.Items.Add(_playAnimBtn);
            
            _stopAnimBtn = new ToolStripButton("⏹ Stop");
            _stopAnimBtn.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _stopAnimBtn.Enabled = false;
            _stopAnimBtn.Click += (s, e) => {
                StopAnimation();
                _animationTime = 0f;
                _playAnimBtn.Text = "▶ Play";
                Refresh();
            };
            toolbar.Items.Add(_stopAnimBtn);

            toolbar.Items.Add(new ToolStripSeparator());
            toolbar.Items.Add(new ToolStripLabel("Drag: Rotate | Shift+Drag: Pan | Scroll: Zoom | WASD: Move | Q/E: Up/Down | R: Reset"));

            toolbar.Dock = DockStyle.Top;
            this.Controls.Add(toolbar);

            // Create info panel
            var infoLabel = new Label
            {
                Name = "InfoLabel",
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(30, 30, 35),
                ForeColor = Color.LightGray,
                Padding = new Padding(5),
                Text = "No model loaded"
            };
            this.Controls.Add(infoLabel);

            // Create GLControl with error handling
            try
            {
                // Try default graphics mode first (more compatible)
                _glControl = new GLControl();
                _glControl.Dock = DockStyle.Fill;
                _glControl.Load += GlControl_Load;
                _glControl.Paint += GlControl_Paint;
                _glControl.Resize += GlControl_Resize;
                _glControl.MouseDown += GlControl_MouseDown;
                _glControl.MouseUp += GlControl_MouseUp;
                _glControl.MouseMove += GlControl_MouseMove;
                _glControl.MouseWheel += GlControl_MouseWheel;
                _glControl.KeyDown += GlControl_KeyDown;
                _glControl.PreviewKeyDown += GlControl_PreviewKeyDown;
                this.Controls.Add(_glControl);
                _glControl.BringToFront();
                
                // Force context creation by making current
                _glControl.MakeCurrent();
                _glInitialized = true;
                SetupGL();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GLControl creation failed: {ex}");
                _initError = ex.Message;
                _glControl = null;
                var errorLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = $"OpenGL initialization failed:\n{ex.Message}\n\nPlease ensure your graphics drivers are up to date.",
                    ForeColor = Color.Red,
                    BackColor = Color.FromArgb(40, 40, 45)
                };
                this.Controls.Add(errorLabel);
            }

            this.ResumeLayout(false);
        }

        private void GlControl_Load(object sender, EventArgs e)
        {
            // Already initialized in constructor, but ensure it's set
            if (!_glInitialized && _glControl != null)
            {
                try
                {
                    _glControl.MakeCurrent();
                    _glInitialized = true;
                    SetupGL();
                    ResetCamera();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"GlControl_Load failed: {ex}");
                }
            };
        }

        private void SetupGL()
        {
            GL.ClearColor(BackgroundColor.R / 255f, BackgroundColor.G / 255f, BackgroundColor.B / 255f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less); // Standard depth function
            GL.Enable(EnableCap.Lighting);
            GL.Enable(EnableCap.Light0);
            GL.Enable(EnableCap.ColorMaterial);
            GL.Enable(EnableCap.Normalize);
            
            // Enable alpha blending for transparent face textures
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            
            // Enable alpha testing to discard fully transparent pixels
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Greater, 0.1f);
            
            // Disable backface culling to show all faces (some models have inconsistent winding)
            GL.Disable(EnableCap.CullFace);

            // Setup light
            GL.Light(LightName.Light0, LightParameter.Position, new float[] { 1, 1, 1, 0 });
            GL.Light(LightName.Light0, LightParameter.Ambient, new float[] { 0.3f, 0.3f, 0.3f, 1 });
            GL.Light(LightName.Light0, LightParameter.Diffuse, new float[] { 0.8f, 0.8f, 0.8f, 1 });
            GL.Light(LightName.Light0, LightParameter.Specular, new float[] { 1, 1, 1, 1 });

            GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
        }

        private void GlControl_Resize(object sender, EventArgs e)
        {
            if (!_glInitialized) return;

            _glControl.MakeCurrent();
            GL.Viewport(0, 0, _glControl.Width, _glControl.Height);

            float aspect = (float)_glControl.Width / _glControl.Height;
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45), aspect, 0.05f, 1000f);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref projection);
        }

        private void GlControl_Paint(object sender, PaintEventArgs e)
        {
            if (!_glInitialized) return;

            _glControl.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Update projection - calculate near/far based on model size and camera distance
            float aspect = (float)_glControl.Width / Math.Max(1, _glControl.Height);
            
            // Use model size to determine appropriate near/far planes
            // Near should be small enough to not clip close geometry
            // Far should be large enough to see the whole model
            float sceneSize = Math.Max(_modelSize, _cameraDistance) * 2f;
            float nearPlane = sceneSize * 0.001f;  // 0.1% of scene size
            float farPlane = sceneSize * 10f;      // 10x scene size
            
            // Clamp to reasonable values
            nearPlane = Math.Max(0.001f, nearPlane);
            farPlane = Math.Max(100f, farPlane);
            
            Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(45), aspect, nearPlane, farPlane);
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadMatrix(ref projection);

            // Setup camera
            UpdateCamera();

            // Draw grid
            DrawGrid();

            // Draw model
            if (_hasModel)
            {
                DrawMeshes();
                if (ShowBones)
                    DrawBones();
            }
            else
            {
                DrawNoModelText();
            }

            _glControl.SwapBuffers();
        }

        private void UpdateCamera()
        {
            // Calculate camera position from spherical coordinates
            float x = (float)(Math.Cos(_cameraPitch) * Math.Sin(_cameraYaw)) * _cameraDistance;
            float y = (float)(Math.Sin(_cameraPitch)) * _cameraDistance;
            float z = (float)(Math.Cos(_cameraPitch) * Math.Cos(_cameraYaw)) * _cameraDistance;

            _cameraPosition = _cameraTarget + new Vector3(x, y, z);

            Matrix4 view = Matrix4.LookAt(_cameraPosition, _cameraTarget, Vector3.UnitY);
            GL.MatrixMode(MatrixMode.Modelview);
            GL.LoadMatrix(ref view);
        }

        private void DrawGrid()
        {
            GL.Disable(EnableCap.Lighting);
            GL.Begin(PrimitiveType.Lines);

            float size = 10f;
            float step = 1f;

            // Grid lines
            GL.Color3(0.3f, 0.3f, 0.35f);
            for (float i = -size; i <= size; i += step)
            {
                GL.Vertex3(i, 0, -size);
                GL.Vertex3(i, 0, size);
                GL.Vertex3(-size, 0, i);
                GL.Vertex3(size, 0, i);
            }

            // Axis lines
            GL.Color3(0.8f, 0.2f, 0.2f); // X = Red
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(size, 0, 0);

            GL.Color3(0.2f, 0.8f, 0.2f); // Y = Green
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, size, 0);

            GL.Color3(0.2f, 0.2f, 0.8f); // Z = Blue
            GL.Vertex3(0, 0, 0);
            GL.Vertex3(0, 0, size);

            GL.End();
            GL.Enable(EnableCap.Lighting);
        }

        private void DrawMeshes()
        {
            // First pass: Draw opaque meshes (body)
            // Second pass: Draw transparent meshes (face/hair) with alpha blending
            
            // Calculate the camera viewing direction for layer visibility
            Vector3 cameraForward = _cameraTarget - _cameraPosition;
            cameraForward.Y = 0; // Horizontal only
            if (cameraForward.LengthSquared > 0.001f)
                cameraForward.Normalize();
            else
                cameraForward = -Vector3.UnitZ;
            
            // Pass 1: Opaque meshes (non-billboard)
            foreach (var mesh in _meshes)
            {
                if (mesh.IsBillboard) continue; // Skip for second pass
                DrawSingleMesh(mesh);
            }
            
            // Pass 2: Transparent/billboard meshes with alpha blending
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false); // Don't write to depth buffer for transparent objects


            // Find best face and best hair mesh for the camera angle using centroid-vs-bone method
            int bestFaceLayer = -1;
            float bestFaceDot = -2.0f;
            int bestHairLayer = -1;
            float bestHairDot = -2.0f;

            float faceThreshold = 0.35f; // stricter threshold for face visibility
            float hairThreshold = 0.15f; // looser for hair

            for (int i = 0; i < _meshes.Count; i++)
            {
                var mesh = _meshes[i];
                if (!mesh.IsBillboard) continue;

                // Compute centroid (world coords) for the mesh
                Vector3 centroid = Vector3.Zero;
                foreach (var v in mesh.Vertices) centroid += v;
                centroid /= mesh.Vertices.Length;

                // Use mesh.BillboardCenter (bone anchor) if available
                Vector3 anchor = mesh.BillboardCenter;

                Vector3 layerDir = centroid - anchor;
                layerDir.Y = 0;
                if (layerDir.LengthSquared < 0.0001f) continue;
                layerDir.Normalize();

                Vector3 camDir = _cameraPosition - anchor;
                camDir.Y = 0;
                if (camDir.LengthSquared < 0.0001f) continue;
                camDir.Normalize();

                float dot = Vector3.Dot(layerDir, camDir);

                // Debug selection info
                try { Log($"SelDbg: mesh={i} name='{mesh.Name}' centroid=({centroid.X:F2},{centroid.Y:F2},{centroid.Z:F2}) anchor=({anchor.X:F2},{anchor.Y:F2},{anchor.Z:F2}) dot={dot:F3} spriteSheet={mesh.IsSpriteSheet}"); } catch { }

                if (mesh.IsSpriteSheet)
                {
                    if (dot > bestFaceDot) { bestFaceDot = dot; bestFaceLayer = i; }
                }
                else
                {
                    if (dot > bestHairDot) { bestHairDot = dot; bestHairLayer = i; }
                }
            }

            Log($"Selection result: bestHair={bestHairLayer} dot={bestHairDot:F3}, bestFace={bestFaceLayer} dot={bestFaceDot:F3}");

            // Draw long hair mesh first, then head/face, then other hair if needed
            int longHairLayer = -1;
            if (bestHairLayer >= 0 && _meshes[bestHairLayer].Name.ToLower().Contains("long"))
                longHairLayer = bestHairLayer;

            // Draw long hair mesh (back hair) first if present
            if (longHairLayer >= 0 && bestHairDot >= hairThreshold)
                DrawSingleMesh(_meshes[longHairLayer]);

            // Draw head/face mesh next (should always overlay hair from the front)
            if (bestFaceLayer >= 0 && bestFaceDot > 0.0f)
            {
                GL.DepthFunc(DepthFunction.Always); // Always draw face on top
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                GL.DepthMask(false);
                DrawSingleMesh(_meshes[bestFaceLayer]);
                GL.DepthMask(true);
                GL.Disable(EnableCap.Blend);
                GL.DepthFunc(DepthFunction.Less); // Restore default
            }

            // Draw any other hair mesh (side/top) after face if needed
            if (bestHairLayer >= 0 && bestHairLayer != longHairLayer && bestHairDot >= hairThreshold)
                DrawSingleMesh(_meshes[bestHairLayer]);

            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.Texture2D);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
        }
        
                private void DrawSingleMesh(RenderMesh mesh)
                {
                    // Align hair mesh with face mesh (rotate 90deg counterclockwise around Z axis)
                    bool isHairMesh = false;
                    try {
                        if (!mesh.IsSpriteSheet && !string.IsNullOrEmpty(mesh.Name) && mesh.Name.ToLower().Contains("hair"))
                            isHairMesh = true;
                    } catch { isHairMesh = false; }

                    // Move long hair mesh backward to avoid overlap with face
                    bool pushedMatrix = false;
                    if (isHairMesh && mesh.Name.ToLower().Contains("long"))
                    {
                        GL.PushMatrix();
                        GL.Translate(0.0f, 0.0f, -0.15f); // Move back along Z axis
                        pushedMatrix = true;
                    }

                    bool hasTexture = mesh.TextureId > 0 && mesh.UVs != null && !ShowWireframe;
                
                if (ShowWireframe)
                {
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                    GL.Disable(EnableCap.Lighting);
                    GL.Disable(EnableCap.Texture2D);
                }
                else
                {
                    GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                    
                    if (hasTexture)
                    {
                        // Disable lighting to show pure texture colors
                        GL.Disable(EnableCap.Lighting);
                        GL.Enable(EnableCap.Texture2D);
                        GL.BindTexture(TextureTarget.Texture2D, mesh.TextureId);
                        
                        // Set texture environment to replace (use texture color directly)
                        GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Replace);
                        
                        GL.Color3(1.0f, 1.0f, 1.0f); // White base color
                    }
                    else
                    {
                        GL.Enable(EnableCap.Lighting);
                        GL.Disable(EnableCap.Texture2D);
                    }
                }

                if (!hasTexture)
                    GL.Color3(mesh.Color.R / 255f, mesh.Color.G / 255f, mesh.Color.B / 255f);

                // Face sprite sheets contain EXPRESSIONS (different facial expressions in grid)
                // NOT viewing angles - each face layer (c01, c03, c05, c07) already has its own
                // angle-specific texture. We just show frame 0 (default expression).
                //
                // Hair textures are atlases - different mesh layers have UVs that sample 
                // different parts of the atlas. Don't apply sprite sheet logic to hair.
                //
                // Face mesh UVs are rotated 90° in the XAC file:
                //   - U=0 at bottom vertices, U=1 at top (U maps to mesh Y axis)
                //   - V=0 at right edge, V=1 at left (V maps to mesh X axis, inverted)
                // We need to rotate UVs 90° counter-clockwise to fix this.
                
                float spriteScaleU = 1.0f, spriteScaleV = 1.0f;

            // ...existing code for mesh drawing...

            // Restore matrix if we pushed for long hair
            if (pushedMatrix)
            {
                GL.PopMatrix();
            }
                float spriteOffsetU = 0, spriteOffsetV = 0;
                bool rotateUV90 = false;
                
                if (mesh.IsSpriteSheet && mesh.SpriteColumns > 1 && mesh.SpriteRows > 1)
                {
                    // Always show frame 0 (first expression) for faces
                    // The sprite sheet is for expressions, not viewing angles
                    int frame = 0;
                    
                    int col = frame % mesh.SpriteColumns;  // column (horizontal in texture)
                    int row = frame / mesh.SpriteColumns;  // row (vertical in texture)
                    
                    // Scale to one frame
                    spriteScaleU = 1.0f / mesh.SpriteColumns;
                    spriteScaleV = 1.0f / mesh.SpriteRows;
                    spriteOffsetU = col * spriteScaleU;
                    spriteOffsetV = row * spriteScaleV;
                    
                    // Face meshes have rotated UV mapping - need to fix
                    rotateUV90 = true;
                }

                // Calculate mesh center for face/hair alignment
                Vector3 meshCenter = Vector3.Zero;
                if (mesh.OriginalVertices != null && mesh.OriginalVertices.Length > 0)
                {
                    foreach (var v in mesh.OriginalVertices) meshCenter += v;
                    meshCenter /= mesh.OriginalVertices.Length;
                }
                // Use bone position as anchor (for attachments)
                Vector3 boneAnchor = mesh.BillboardCenter;
                // Offset to align chin at tip of neck (face mesh only)
                float chinYOffset = 0.0f;
                if (mesh.IsSpriteSheet)
                {
                    // Find lowest Y vertex (chin)
                    float minY = float.MaxValue;
                    foreach (var v in mesh.OriginalVertices) if (v.Y < minY) minY = v.Y;
                    chinYOffset = -minY; // Move chin to Y=0
                }
                // Do not rotate UVs for hair; use original UVs as loaded

                GL.Begin(PrimitiveType.Triangles);
                for (int i = 0; i < mesh.Indices.Length; i++)
                {
                    int idx = mesh.Indices[i];
                    if (idx < mesh.Vertices.Length)
                    {
                        Vector3 vertex = mesh.OriginalVertices != null && idx < mesh.OriginalVertices.Length ? mesh.OriginalVertices[idx] : mesh.Vertices[idx];
                        // Center mesh on bone anchor
                        vertex -= meshCenter;
                        // Rotate hair mesh 90deg counterclockwise around Z axis to align with face
                        if (isHairMesh) {
                            float x = vertex.X;
                            float y = vertex.Y;
                            vertex.X = -y;
                            vertex.Y = x;
                        }
                        vertex += boneAnchor;
                        // For face mesh, offset so chin is at bone anchor
                        if (mesh.IsSpriteSheet)
                            vertex.Y += chinYOffset;
                        // Do NOT apply billboarding rotation for hair or face meshes
                        // Only select and render the mesh layer that matches the camera angle
                        if (hasTexture && idx < mesh.UVs.Length)
                        {
                            Vector2 uv = mesh.UVs[idx];
                            if (mesh.IsSpriteSheet)
                            {
                                uv = new Vector2(
                                    uv.Y * spriteScaleU + spriteOffsetU,
                                    (1.0f - uv.X) * spriteScaleV + spriteOffsetV
                                );
                            }
                            // For hair meshes, do not modify UVs; use as loaded
                            GL.TexCoord2(uv);
                        }
                        if (mesh.Normals != null && idx < mesh.Normals.Length)
                            GL.Normal3(mesh.Normals[idx]);
                        GL.Vertex3(vertex);
                    }
                }
                GL.End();
        }
        
        /// <summary>
        /// Calculate which sprite frame to show based on camera angle relative to the billboard
        /// </summary>
        private int CalculateSpriteFrame(Vector3 billboardCenter, int totalFrames)
        {
            // Calculate the angle from the billboard to the camera (on XZ plane)
            Vector3 toCamera = _cameraPosition - billboardCenter;
            toCamera.Y = 0;
            
            if (toCamera.LengthSquared < 0.001f)
                return 0;
            
            // Calculate angle in radians (0 = looking at front, PI = looking at back)
            float angle = (float)Math.Atan2(toCamera.X, toCamera.Z);
            
            // Normalize to 0-1 range (0 = front, 0.5 = back)
            // Add PI to shift range from [-PI, PI] to [0, 2PI]
            float normalizedAngle = (angle + (float)Math.PI) / (2.0f * (float)Math.PI);
            
            // Map to frame index
            // Frame 0 = front, frames increase going clockwise
            int frame = (int)(normalizedAngle * totalFrames) % totalFrames;
            
            return frame;
        }
        
        /// <summary>
        /// Determine if a billboard layer should be visible based on camera viewing angle.
        /// TOS uses multiple layers positioned around the head - each layer is only visible
        /// when the camera is looking at it from the appropriate angle.
        /// </summary>
        private bool IsBillboardLayerVisible(RenderMesh mesh, Vector3 cameraForward)
        {
            if (mesh.Vertices == null || mesh.Vertices.Length == 0)
                return true;
            
            // Calculate the average Z position of this mesh (relative to bone)
            // Layers at negative Z are "back of head", positive Z are "front"
            float avgZ = 0;
            foreach (var v in mesh.Vertices)
                avgZ += v.Z;
            avgZ /= mesh.Vertices.Length;
            
            // Camera forward points from camera toward target
            // If camera is in front of character (looking at front), forward.Z will be positive
            // If camera is behind character (looking at back), forward.Z will be negative
            
            // The mesh layer's avgZ tells us which side of the head it's on:
            // - Negative Z (e.g., -12 to -15) = back of head layers
            // - Positive Z (e.g., +4 to +7) = front of head layers
            // - Near zero = side layers (always show, or show based on X position)
            
            // Calculate which "hemisphere" the camera is viewing
            // cameraForward.Z > 0 means camera is looking from front to back
            // cameraForward.Z < 0 means camera is looking from back to front
            
            // For layers to be visible, they should face toward the camera:
            // - Front layers (avgZ > 0) visible when camera is in front (cameraForward.Z > 0 is looking at it)
            // - Back layers (avgZ < 0) visible when camera is behind (cameraForward.Z < 0 is looking at it)
            
            // Calculate dot product between layer normal and camera direction
            // Layer normal is approximately (0, 0, sign(avgZ))
            Vector3 layerNormal = new Vector3(0, 0, avgZ > 0 ? 1 : -1);
            
            // If avgZ is near zero, this is a side layer - check X position instead
            if (Math.Abs(avgZ) < 3.0f)
            {
                float avgX = 0;
                foreach (var v in mesh.Vertices)
                    avgX += v.X;
                avgX /= mesh.Vertices.Length;
                
                // Side layers: use X position to determine visibility
                // Left side layers (avgX < 0) visible when camera.X component suggests left viewing
                // Right side layers (avgX > 0) visible when camera.X component suggests right viewing
                if (Math.Abs(avgX) > 3.0f)
                {
                    layerNormal = new Vector3(avgX > 0 ? 1 : -1, 0, 0);
                }
                else
                {
                    // Near center - always visible (like face center layer)
                    return true;
                }
            }
            
            // Dot product: if positive, layer faces camera; if negative, layer faces away
            float dot = Vector3.Dot(layerNormal, cameraForward);
            
            // Layer is visible if it faces the camera (dot > threshold)
            // Use a threshold slightly below 0 to allow some overlap at transitions
            return dot > -0.3f;
        }
        
        /// <summary>
        /// Apply billboard rotation to make face/hair layers face the camera.
        /// This transforms the vertex from the original mesh space to camera-facing space.
        /// </summary>
        /// <param name="originalVertex">The original local vertex position (relative to mesh origin)</param>
        /// <param name="boneWorldPos">The bone's world position where the billboard is attached</param>
        /// <param name="layerIndex">Layer stacking index (0=front, higher=further back)</param>
        private Vector3 ApplyBillboardRotation(Vector3 originalVertex, Vector3 boneWorldPos, int layerIndex)
        {
            // For billboards, we want the layers to always face the camera
            // and stack front-to-back with consistent spacing
            
            // originalVertex is the LOCAL position from the XAC file
            // We need to transform it so the billboard faces the camera
            
            // Calculate camera direction (horizontal only for billboard rotation)
            Vector3 toCamera = _cameraPosition - boneWorldPos;
            toCamera.Y = 0;
            
            if (toCamera.LengthSquared < 0.001f)
                toCamera = Vector3.UnitZ; // Default forward
            
            toCamera.Normalize();
            
            // Calculate the angle from +Z to the camera direction
            float yawAngle = (float)Math.Atan2(toCamera.X, toCamera.Z);
            
            // Create the billboard coordinate system:
            // - right: points to the right of the billboard (camera's left)
            // - up: points up (Y axis)
            // - forward: points from billboard towards camera
            float cosA = (float)Math.Cos(yawAngle);
            float sinA = (float)Math.Sin(yawAngle);
            
            // Right vector on XZ plane (perpendicular to camera direction)
            Vector3 right = new Vector3(cosA, 0, -sinA);
            Vector3 up = Vector3.UnitY;
            Vector3 forward = toCamera; // Towards camera
            
            // The original vertex has:
            // X: horizontal offset from center
            // Y: vertical offset from center (height)
            // Z: depth offset (how far forward/back the vertex is)
            
            // For face layers in TOS:
            // - Negative Y in the original = below center
            // - Positive Z in the original = in front (towards camera)
            
            // Calculate the depth for this layer (higher index = further from camera)
            float layerSpacing = 0.3f; // Small spacing between layers to prevent z-fighting
            float depthOffset = layerIndex * layerSpacing;
            
            // Transform the local position to world-aligned billboard coordinates
            // We add the bone position to place the billboard at the attachment point
            Vector3 billboardPos = boneWorldPos 
                + right * originalVertex.X       // Horizontal position (left/right)
                + up * originalVertex.Y          // Vertical position (up/down from bone)
                + forward * (originalVertex.Z - depthOffset); // Depth towards camera, minus layer offset
            
            return billboardPos;
        }

        private void DrawBones()
        {
            if (_bones.Count == 0) return;
            
            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.DepthTest); // Draw bones on top
            
            // Draw bone connections (lines from parent to child)
            GL.LineWidth(2.0f);
            GL.Begin(PrimitiveType.Lines);
            GL.Color3(1.0f, 0.8f, 0.0f); // Yellow/gold for bones
            
            foreach (var bone in _bones)
            {
                if (bone.ParentIndex >= 0 && bone.ParentIndex < _bones.Count)
                {
                    var parent = _bones[bone.ParentIndex];
                    GL.Vertex3(parent.WorldPosition);
                    GL.Vertex3(bone.WorldPosition);
                }
            }
            GL.End();
            
            // Draw bone joints as points
            GL.PointSize(6.0f);
            GL.Begin(PrimitiveType.Points);
            
            foreach (var bone in _bones)
            {
                // Root bones are red, others are yellow
                if (bone.ParentIndex < 0)
                    GL.Color3(1.0f, 0.2f, 0.2f); // Red for root
                else
                    GL.Color3(1.0f, 0.9f, 0.3f); // Yellow for others
                    
                GL.Vertex3(bone.WorldPosition);
            }
            GL.End();
            
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Lighting);
            GL.LineWidth(1.0f);
            GL.PointSize(1.0f);
        }

        private void DrawNoModelText()
        {
            // Just show empty view - info is in the label
        }

        #region Mouse Controls

        private void GlControl_MouseDown(object sender, MouseEventArgs e)
        {
            // Right-click toggles camera lock (useful for inspecting static views)
            if (e.Button == MouseButtons.Right)
            {
                _cameraLocked = !_cameraLocked;
                UpdateInfoLabel(_cameraLocked ? "Camera locked" : "Camera unlocked");
                return;
            }

            _isDragging = true;
            _dragButton = e.Button;
            _lastMousePos = e.Location;
        }

        private void GlControl_MouseUp(object sender, MouseEventArgs e)
        {
            _isDragging = false;
        }

        private void GlControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            if (_cameraLocked) return;

            float dx = e.X - _lastMousePos.X;
            float dy = e.Y - _lastMousePos.Y;

            if (_dragButton == MouseButtons.Left)
            {
                if (Control.ModifierKeys.HasFlag(Keys.Shift))
                {
                    // Pan
                    Vector3 right = Vector3.Cross(_cameraPosition - _cameraTarget, Vector3.UnitY).Normalized();
                    Vector3 up = Vector3.UnitY;
                    float panSpeed = _cameraDistance * 0.002f;
                    _cameraTarget -= right * dx * panSpeed;
                    _cameraTarget += up * dy * panSpeed;
                }
                else
                {
                    // Rotate
                    float rotSpeed = 0.01f;
                    _cameraYaw -= dx * rotSpeed;
                    _cameraPitch += dy * rotSpeed;
                    float maxPitch = (float)(Math.PI / 2 - 0.01);
                    _cameraPitch = Math.Max(-maxPitch, Math.Min(maxPitch, _cameraPitch));
                }
            }

            _lastMousePos = e.Location;
            UpdateCamera();
            Refresh();
        }

        private void GlControl_MouseWheel(object sender, MouseEventArgs e)
        {
            // Use proportional zoom for smooth scaling at any distance
            float zoomFactor = 1.0f - (e.Delta * 0.001f);
            _cameraDistance *= zoomFactor;
            _cameraDistance = Math.Max(0.1f, Math.Min(500f, _cameraDistance));
            Refresh();
        }

        private void GlControl_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            // Mark arrow keys and WASD as input keys so KeyDown fires
            switch (e.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.W:
                case Keys.A:
                case Keys.S:
                case Keys.D:
                case Keys.Q:
                case Keys.E:
                    e.IsInputKey = true;
                    break;
            }
        }

        private void GlControl_KeyDown(object sender, KeyEventArgs e)
        {
            // Calculate movement vectors based on camera orientation
            Vector3 forward = (_cameraTarget - _cameraPosition).Normalized();
            Vector3 right = Vector3.Cross(forward, Vector3.UnitY).Normalized();
            Vector3 up = Vector3.UnitY;
            
            float moveSpeed = _cameraDistance * 0.1f; // Scale movement with zoom level
            float rotateSpeed = 0.1f;
            
            bool moved = false;
            
            switch (e.KeyCode)
            {
                // WASD - Pan camera target
                case Keys.W:
                case Keys.Up:
                    _cameraTarget += forward * moveSpeed;
                    moved = true;
                    break;
                case Keys.S:
                case Keys.Down:
                    _cameraTarget -= forward * moveSpeed;
                    moved = true;
                    break;
                case Keys.A:
                case Keys.Left:
                    _cameraTarget -= right * moveSpeed;
                    moved = true;
                    break;
                case Keys.D:
                case Keys.Right:
                    _cameraTarget += right * moveSpeed;
                    moved = true;
                    break;
                    
                // Q/E - Move up/down
                case Keys.Q:
                    _cameraTarget -= up * moveSpeed;
                    moved = true;
                    break;
                case Keys.E:
                    _cameraTarget += up * moveSpeed;
                    moved = true;
                    break;
                    
                // R - Reset camera
                case Keys.R:
                    ResetCamera();
                    moved = true;
                    break;
                    
                // Numpad for rotation
                case Keys.NumPad4:
                    _cameraYaw -= rotateSpeed;
                    moved = true;
                    break;
                case Keys.NumPad6:
                    _cameraYaw += rotateSpeed;
                    moved = true;
                    break;
                case Keys.NumPad8:
                    _cameraPitch = Math.Min(1.5f, _cameraPitch + rotateSpeed);
                    moved = true;
                    break;
                case Keys.NumPad2:
                    _cameraPitch = Math.Max(-1.5f, _cameraPitch - rotateSpeed);
                    moved = true;
                    break;
            }
            
            if (moved)
            {
                e.Handled = true;
                Refresh();
            }
        }

        #endregion

        #region Public Methods

        public void ResetCamera()
        {
            _cameraDistance = 5.0f;
            _cameraYaw = (float)Math.PI; // Face front of model
            _cameraPitch = 0.3f;
            _cameraTarget = new Vector3(0, 0, 0);

            if (_hasModel && _modelBounds != null)
            {
                // Center on model
                _cameraTarget = (_modelBounds.Min + _modelBounds.Max) * 0.5f;
                _modelSize = (_modelBounds.Max - _modelBounds.Min).Length;
                _cameraDistance = _modelSize * 1.5f; // Position camera at 1.5x model size
            }
            else
            {
                _modelSize = 1.0f;
            }

            Refresh();
        }

        public void LoadXacModel(FileFormats.XAC.XacFile xac)
        {
            _meshes.Clear();
            _hasModel = false;
            _attachmentStartIndex = -1; // Reset attachment tracking
            _attachmentTextureNames.Clear(); // Reset attachment texture names

            if (xac == null || xac.Meshes.Count == 0)
            {
                UpdateInfoLabel("No mesh data found in XAC file");
                Refresh();
                return;
            }

            var allVertices = new List<Vector3>();
            int totalVerts = 0;
            int totalTris = 0;

            foreach (var mesh in xac.Meshes)
            {
                // Skip collision meshes - they're usually simple bounding shapes
                if (mesh.IsCollisionMesh)
                    continue;
                
                // Parse all vertex data from attribute layers first
                Vector3[] allPositions = null;
                Vector3[] allNormals = null;
                Vector2[] allUVs = null;

                // Extract vertex positions (type 0)
                var posLayer = mesh.VertexAttributes.Find(a => a.Type == 0);
                if (posLayer != null && posLayer.Data != null)
                {
                    int vertCount = posLayer.Data.Length / 12; // 3 floats * 4 bytes
                    allPositions = new Vector3[vertCount];

                    using (var ms = new System.IO.MemoryStream(posLayer.Data))
                    using (var br = new System.IO.BinaryReader(ms))
                    {
                        for (int i = 0; i < vertCount; i++)
                        {
                            allPositions[i] = new Vector3(
                                br.ReadSingle(),
                                br.ReadSingle(),
                                br.ReadSingle()
                            );
                            allVertices.Add(allPositions[i]);
                        }
                    }
                }

                // Extract normals (type 1)
                var normLayer = mesh.VertexAttributes.Find(a => a.Type == 1);
                if (normLayer != null && normLayer.Data != null)
                {
                    int normCount = normLayer.Data.Length / 12;
                    allNormals = new Vector3[normCount];

                    using (var ms = new System.IO.MemoryStream(normLayer.Data))
                    using (var br = new System.IO.BinaryReader(ms))
                    {
                        for (int i = 0; i < normCount; i++)
                        {
                            allNormals[i] = new Vector3(
                                br.ReadSingle(),
                                br.ReadSingle(),
                                br.ReadSingle()
                            );
                        }
                    }
                }

                // Extract UVs (type 3) - first UV set
                var uvLayer = mesh.VertexAttributes.Find(a => a.Type == 3);
                if (uvLayer != null && uvLayer.Data != null)
                {
                    int uvSize = uvLayer.AttribSize > 0 ? uvLayer.AttribSize : 8;
                    int uvCount = uvLayer.Data.Length / uvSize;
                    allUVs = new Vector2[uvCount];

                    using (var ms = new System.IO.MemoryStream(uvLayer.Data))
                    using (var br = new System.IO.BinaryReader(ms))
                    {
                        for (int i = 0; i < uvCount; i++)
                        {
                            float u = br.ReadSingle();
                            float v = br.ReadSingle();
                            if (uvSize > 8)
                                br.ReadBytes(uvSize - 8);
                            // Try WITHOUT flip to test
                            allUVs[i] = new Vector2(u, v);
                        }
                    }
                }

                // Now create a RenderMesh for each submesh
                // Each submesh has its own vertex range and indices relative to that range
                int vertexStart = 0;
                foreach (var subMesh in mesh.SubMeshes)
                {
                    var renderMesh = new RenderMesh();
                    renderMesh.Color = Color.FromArgb(180, 180, 190);
                    renderMesh.MaterialId = subMesh.MaterialId;
                    
                    int subVertCount = subMesh.NumVertices;
                    
                    // Copy vertex slice for this submesh
                    if (allPositions != null && vertexStart + subVertCount <= allPositions.Length)
                    {
                        renderMesh.Vertices = new Vector3[subVertCount];
                        Array.Copy(allPositions, vertexStart, renderMesh.Vertices, 0, subVertCount);
                    }
                    
                    if (allNormals != null && vertexStart + subVertCount <= allNormals.Length)
                    {
                        renderMesh.Normals = new Vector3[subVertCount];
                        Array.Copy(allNormals, vertexStart, renderMesh.Normals, 0, subVertCount);
                    }
                    
                    if (allUVs != null && vertexStart + subVertCount <= allUVs.Length)
                    {
                        renderMesh.UVs = new Vector2[subVertCount];
                        Array.Copy(allUVs, vertexStart, renderMesh.UVs, 0, subVertCount);
                    }
                    
                    // Indices in XAC are already local to each submesh (0-based within submesh)
                    // so we can use them directly without adjustment
                    renderMesh.Indices = new int[subMesh.NumIndices];
                    int maxIdx = 0;
                    int minIdx = int.MaxValue;
                    for (int i = 0; i < subMesh.NumIndices; i++)
                    {
                        renderMesh.Indices[i] = subMesh.Indices[i];
                        if (subMesh.Indices[i] > maxIdx) maxIdx = subMesh.Indices[i];
                        if (subMesh.Indices[i] < minIdx) minIdx = subMesh.Indices[i];
                    }
                    Log($"  SubMesh: MaterialId={subMesh.MaterialId}, NumVerts={subVertCount}, NumIndices={subMesh.NumIndices}, IndexRange=[{minIdx}-{maxIdx}]");
                    if (maxIdx >= subVertCount)
                        Log($"    WARNING: Index {maxIdx} out of bounds (vertices: {subVertCount})");
                    
                    // Log first triangle's data for debugging
                    if (renderMesh.Indices.Length >= 3 && renderMesh.UVs != null)
                    {
                        Log($"    First triangle indices: [{renderMesh.Indices[0]}, {renderMesh.Indices[1]}, {renderMesh.Indices[2]}]");
                        for (int ti = 0; ti < 3 && ti < renderMesh.Indices.Length; ti++)
                        {
                            int tidx = renderMesh.Indices[ti];
                            if (tidx < renderMesh.UVs.Length && tidx < renderMesh.Vertices.Length)
                            {
                                var v = renderMesh.Vertices[tidx];
                                var uv = renderMesh.UVs[tidx];
                                Log($"      [{ti}] idx={tidx}: Vert=({v.X:F2},{v.Y:F2},{v.Z:F2}), UV=({uv.X:F3},{uv.Y:F3})");
                            }
                        }
                    }
                    
                    totalVerts += subVertCount;
                    totalTris += subMesh.NumIndices / 3;
                    
                    _meshes.Add(renderMesh);
                    
                    vertexStart += subVertCount;
                }
            }

            // Calculate bounds
            if (allVertices.Count > 0)
            {
                _modelBounds = new BoundingBox();
                _modelBounds.Min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                _modelBounds.Max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

                foreach (var v in allVertices)
                {
                    _modelBounds.Min = Vector3.ComponentMin(_modelBounds.Min, v);
                    _modelBounds.Max = Vector3.ComponentMax(_modelBounds.Max, v);
                }
                
                // Calculate model size for projection settings
                _modelSize = (_modelBounds.Max - _modelBounds.Min).Length;
            }

            // Load bone/node hierarchy
            _bones.Clear();
            if (xac.Nodes != null && xac.Nodes.Count > 0)
            {
                // First pass: Create all bones with local transforms
                foreach (var node in xac.Nodes)
                {
                    var bone = new RenderBone
                    {
                        Name = node.Name,
                        ParentIndex = node.ParentNodeId,
                        LocalPosition = new Vector3(node.Position.X, node.Position.Y, node.Position.Z)
                    };
                    
                    // Build local transformation matrix
                    // Use rotation and position to create local transform
                    var rotation = new Quaternion(node.Rotation.X, node.Rotation.Y, node.Rotation.Z, node.Rotation.W);
                    var scale = new Vector3(node.Scale.X, node.Scale.Y, node.Scale.Z);
                    var position = bone.LocalPosition;
                    
                    // Create transform: first rotate, then translate
                    // This puts the bone at 'position' with 'rotation' orientation
                    Matrix4 rotMatrix = Matrix4.CreateFromQuaternion(rotation);
                    Matrix4 scaleMatrix = Matrix4.CreateScale(scale);
                    Matrix4 transMatrix = Matrix4.CreateTranslation(position);
                    
                    // Local transform = Translation * Rotation * Scale
                    // When we multiply Parent * Local, the translation happens in parent space
                    bone.LocalTransform = scaleMatrix * rotMatrix * transMatrix;
                    
                    _bones.Add(bone);
                }
                
                // Second pass: Calculate world transforms by walking the hierarchy
                for (int i = 0; i < _bones.Count; i++)
                {
                    var bone = _bones[i];
                    
                    if (bone.ParentIndex < 0 || bone.ParentIndex >= _bones.Count)
                    {
                        // Root bone - world transform equals local transform
                        bone.WorldTransform = bone.LocalTransform;
                        bone.WorldPosition = bone.LocalPosition;
                    }
                    else
                    {
                        // Child bone - transform local position by parent's world transform
                        var parent = _bones[bone.ParentIndex];
                        
                        // World transform = Parent's world * my local
                        bone.WorldTransform = bone.LocalTransform * parent.WorldTransform;
                        
                        // World position = parent world position + (local position rotated by parent's world rotation)
                        // Extract parent's world rotation from its world transform
                        Vector3 parentWorldPos = parent.WorldPosition;
                        
                        // Transform local position by parent's world transform (without translation)
                        // to get the offset in world space
                        Vector4 localPos4 = new Vector4(bone.LocalPosition, 1.0f);
                        Vector4 worldPos4 = Vector4.Transform(localPos4, parent.WorldTransform);
                        
                        bone.WorldPosition = new Vector3(worldPos4.X, worldPos4.Y, worldPos4.Z);
                    }
                }
            }

            // Extract material information for texture loading
            _materials.Clear();
            for (int i = 0; i < xac.Materials.Count; i++)
            {
                var mat = xac.Materials[i];
                var matInfo = new MaterialInfo
                {
                    Id = i,
                    Name = mat.Name
                };
                
                // Extract texture names from material layers
                if (mat.Layers != null)
                {
                    foreach (var layer in mat.Layers)
                    {
                        if (!string.IsNullOrEmpty(layer.Texture))
                        {
                            matInfo.TextureNames.Add(layer.Texture);
                        }
                    }
                }
                
                _materials.Add(matInfo);
            }

            _hasModel = true;

            // Debug: Log material and mesh info to file
            Log($"=== XAC Model Loaded ===");
            Log($"Total Meshes (RenderMesh): {_meshes.Count}");
            for (int i = 0; i < _meshes.Count; i++)
            {
                var m = _meshes[i];
                Log($"  Mesh {i}: MaterialId={m.MaterialId}, Verts={m.Vertices?.Length ?? 0}, UVs={m.UVs?.Length ?? 0}, Indices={m.Indices?.Length ?? 0}");
                if (m.UVs != null && m.UVs.Length > 0)
                {
                    // Calculate UV bounds
                    float minU = float.MaxValue, maxU = float.MinValue;
                    float minV = float.MaxValue, maxV = float.MinValue;
                    foreach (var uv in m.UVs)
                    {
                        if (uv.X < minU) minU = uv.X;
                        if (uv.X > maxU) maxU = uv.X;
                        if (uv.Y < minV) minV = uv.Y;
                        if (uv.Y > maxV) maxV = uv.Y;
                    }
                    Log($"    UV Bounds: U=[{minU:F3}, {maxU:F3}], V=[{minV:F3}, {maxV:F3}]");
                }
            }
            Log($"Materials: {xac.Materials.Count}");
            for (int i = 0; i < xac.Materials.Count; i++)
            {
                var mat = xac.Materials[i];
                Log($"  Material {i}: Name={mat.Name}, Layers={mat.Layers?.Count ?? 0}");
                if (mat.Layers != null)
                {
                    foreach (var layer in mat.Layers)
                    {
                        Log($"    Layer: Texture={layer.Texture}, MapType={layer.MapType}, TilingU={layer.TilingU}, TilingV={layer.TilingV}");
                    }
                }
            }
            Log($"========================");

            // Extract required textures from XAC
            _requiredTextures.Clear();
            _requiredTextures.AddRange(xac.GetAllDiffuseTextures());
            
            // Also add textures from material layers
            foreach (var mat in xac.Materials)
            {
                if (mat.Layers != null)
                {
                    foreach (var layer in mat.Layers)
                    {
                        if (!string.IsNullOrEmpty(layer.Texture) && !_requiredTextures.Contains(layer.Texture))
                        {
                            _requiredTextures.Add(layer.Texture);
                        }
                    }
                }
            }
            
            Log($"Required Textures: {_requiredTextures.Count}");
            foreach (var tex in _requiredTextures)
                Log($"  - {tex}");

            // Update info
            string info = $"Meshes: {_meshes.Count} | Vertices: {totalVerts:N0} | Triangles: {totalTris:N0}";
            if (xac.Nodes.Count > 0)
                info += $" | Bones: {xac.Nodes.Count}";
            if (xac.Materials.Count > 0)
                info += $" | Materials: {xac.Materials.Count}";
            if (_requiredTextures.Count > 0)
                info += $" | Textures needed: {_requiredTextures.Count}";

            UpdateInfoLabel(info);
            ResetCamera();
            
            // Check if this is an attachment model
            DetectAttachmentModel(xac);
            
            // Notify that textures are needed
            TexturesNeeded?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Detect if the loaded model is an attachment (face, head, hair, etc.)
        /// </summary>
        private void DetectAttachmentModel(FileFormats.XAC.XacFile xac)
        {
            _isAttachmentModel = false;
            _attachmentBoneName = null;
            
            if (xac.Nodes == null || xac.Nodes.Count == 0)
                return;
            
            // Attachment models typically have few nodes (1-5) and reference specific bones
            var attachmentBones = new[] { "Bip01 Head", "Bip01 Neck", "Bip01 Ponytail" };
            
            foreach (var node in xac.Nodes)
            {
                foreach (var boneName in attachmentBones)
                {
                    if (node.Name != null && node.Name.Contains(boneName))
                    {
                        _attachmentBoneName = boneName;
                        break;
                    }
                }
                if (_attachmentBoneName != null) break;
            }
            
            // Small node count + mesh data + attachment bone = likely attachment
            bool isSmallNodeCount = xac.Nodes.Count <= 5;
            bool hasMeshData = xac.Meshes.Count > 0;
            
            _isAttachmentModel = (_attachmentBoneName != null && isSmallNodeCount && hasMeshData);
            
            // If we already have a parent skeleton, apply transform automatically
            if (_isAttachmentModel && _parentSkeleton != null && 
                _attachmentBoneName != null && _parentBoneTransforms.ContainsKey(_attachmentBoneName))
            {
                ApplyParentBoneTransform(_attachmentBoneName);
            }
        }
        
        /// <summary>
        /// Load a parent skeleton file (bodybase) to provide bone transforms for attachments
        /// </summary>
        public void LoadParentSkeleton(FileFormats.XAC.XacFile skeleton)
        {
            _parentSkeleton = skeleton;
            _parentBoneTransforms.Clear();
            
            if (skeleton == null || skeleton.Nodes == null)
                return;
            
            // Build bone hierarchy and calculate world transforms
            var bones = new List<RenderBone>();
            
            // First pass: Create bones with local transforms
            foreach (var node in skeleton.Nodes)
            {
                var bone = new RenderBone
                {
                    Name = node.Name,
                    ParentIndex = node.ParentNodeId,
                    LocalPosition = new Vector3(node.Position.X, node.Position.Y, node.Position.Z)
                };
                
                var rotation = new Quaternion(node.Rotation.X, node.Rotation.Y, node.Rotation.Z, node.Rotation.W);
                var scale = new Vector3(node.Scale.X, node.Scale.Y, node.Scale.Z);
                
                Matrix4 rotMatrix = Matrix4.CreateFromQuaternion(rotation);
                Matrix4 scaleMatrix = Matrix4.CreateScale(scale);
                Matrix4 transMatrix = Matrix4.CreateTranslation(bone.LocalPosition);
                
                bone.LocalTransform = scaleMatrix * rotMatrix * transMatrix;
                bones.Add(bone);
            }
            
            // Second pass: Calculate world transforms
            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                
                if (bone.ParentIndex < 0 || bone.ParentIndex >= bones.Count)
                {
                    bone.WorldTransform = bone.LocalTransform;
                }
                else
                {
                    var parent = bones[bone.ParentIndex];
                    bone.WorldTransform = bone.LocalTransform * parent.WorldTransform;
                }
                
                // Store the world transform by bone name
                if (!string.IsNullOrEmpty(bone.Name))
                {
                    _parentBoneTransforms[bone.Name] = bone.WorldTransform;
                }
            }
            
            Log($"Loaded parent skeleton with {bones.Count} bones");
            
            // If we already have an attachment model loaded, re-apply transforms
            if (_isAttachmentModel && !string.IsNullOrEmpty(_attachmentBoneName))
            {
                ApplyParentBoneTransform(_attachmentBoneName);
            }
        }
        
        /// <summary>
        /// Clear the parent skeleton
        /// </summary>
        public void ClearParentSkeleton()
        {
            _parentSkeleton = null;
            _parentBoneTransforms.Clear();
        }
        
        /// <summary>
        /// Apply parent bone transform to all mesh vertices
        /// </summary>
        private void ApplyParentBoneTransform(string boneName)
        {
            if (!_parentBoneTransforms.ContainsKey(boneName))
            {
                Log($"Cannot find bone '{boneName}' in parent skeleton");
                return;
            }
            
            var boneTransform = _parentBoneTransforms[boneName];
            Log($"Applying parent bone transform for '{boneName}'");
            
            // Transform all mesh vertices by the bone's world transform
            foreach (var mesh in _meshes)
            {
                if (mesh.Vertices == null) continue;
                
                for (int i = 0; i < mesh.Vertices.Length; i++)
                {
                    var v = new Vector4(mesh.Vertices[i], 1.0f);
                    var transformed = Vector4.Transform(v, boneTransform);
                    mesh.Vertices[i] = new Vector3(transformed.X, transformed.Y, transformed.Z);
                }
                
                // Also transform normals (without translation)
                if (mesh.Normals != null)
                {
                    var normalMatrix = new Matrix4(boneTransform.Row0, boneTransform.Row1, boneTransform.Row2, Vector4.UnitW);
                    for (int i = 0; i < mesh.Normals.Length; i++)
                    {
                        var n = new Vector4(mesh.Normals[i], 0.0f);
                        var transformed = Vector4.Transform(n, normalMatrix);
                        mesh.Normals[i] = new Vector3(transformed.X, transformed.Y, transformed.Z).Normalized();
                    }
                }
            }
            
            // Recalculate bounds after transform
            RecalculateBounds();
            
            UpdateInfoLabel($"Attachment positioned on '{boneName}'");
            Refresh();
        }
        
        /// <summary>
        /// Recalculate model bounds after transformation
        /// </summary>
        private void RecalculateBounds()
        {
            _modelBounds = new BoundingBox();
            _modelBounds.Min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _modelBounds.Max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            
            foreach (var mesh in _meshes)
            {
                if (mesh.Vertices == null) continue;
                
                foreach (var v in mesh.Vertices)
                {
                    _modelBounds.Min = Vector3.ComponentMin(_modelBounds.Min, v);
                    _modelBounds.Max = Vector3.ComponentMax(_modelBounds.Max, v);
                }
            }
            
            _modelSize = (_modelBounds.Max - _modelBounds.Min).Length;
            if (_modelSize < 0.001f) _modelSize = 1.0f;
        }
        
        /// <summary>
        /// Find a bone by name in the current model's skeleton
        /// </summary>
        public RenderBone FindBone(string boneName)
        {
            return _bones.FirstOrDefault(b => b.Name == boneName);
        }
        
        /// <summary>
        /// Get list of bone names for display
        /// </summary>
        public List<string> GetBoneNames()
        {
            return _bones.Select(b => b.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();
        }
        
        /// <summary>
        /// Add an attachment mesh (face, head, hair) to the current model
        /// The attachment will be transformed by the appropriate bone's world transform
        /// </summary>
        public bool AddAttachmentMesh(FileFormats.XAC.XacFile attachmentXac, string targetBoneName = null)
        {
            if (!_hasModel || _bones.Count == 0)
            {
                Log("Cannot add attachment: no model or skeleton loaded");
                return false;
            }
            
            if (attachmentXac.Meshes == null || attachmentXac.Meshes.Count == 0)
            {
                Log("Cannot add attachment: no meshes in attachment file");
                return false;
            }
            
            // Find which bone to attach to
            string boneName = targetBoneName;
            
            if (string.IsNullOrEmpty(boneName))
            {
                // Try to detect from attachment's node names
                var attachmentBones = new[] { "Bip01 Head", "Bip01 Neck", "Bip01 Ponytail", "Bip01 L Hand", "Bip01 R Hand" };
                foreach (var node in attachmentXac.Nodes)
                {
                    foreach (var ab in attachmentBones)
                    {
                        if (node.Name != null && node.Name.Contains(ab))
                        {
                            boneName = ab;
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(boneName)) break;
                }
            }
            
            // Default to head if not detected
            if (string.IsNullOrEmpty(boneName))
                boneName = "Bip01 Head";
            
            // Find the bone in our skeleton
            var targetBone = FindBone(boneName);
            if (targetBone == null)
            {
                Log($"Cannot find bone '{boneName}' in current model's skeleton");
                return false;
            }
            
            Log($"Adding attachment to bone '{boneName}'");
            Matrix4 boneTransform = targetBone.WorldTransform;
            
            // Mark where attachments start (first attachment only)
            if (_attachmentStartIndex < 0)
            {
                _attachmentStartIndex = _meshes.Count;
                _attachmentTextureNames.Clear(); // Clear previous attachment textures
            }
            
            // Extract DiffuseTex from attachment PROPERTIES chunks
            var diffuseTextures = attachmentXac.GetAllDiffuseTextures();
            Log($"Scanning attachment for DiffuseTex: found {diffuseTextures.Count} texture(s)");
            foreach (var tex in diffuseTextures)
            {
                if (!_attachmentTextureNames.Contains(tex))
                {
                    _attachmentTextureNames.Add(tex);
                    Log($"Found attachment texture: '{tex}'");
                }
            }
            
            int addedMeshes = 0;
            int addedVerts = 0;
            
            // Process each mesh in the attachment
            foreach (var mesh in attachmentXac.Meshes)
            {
                if (mesh.IsCollisionMesh) continue;
                if (mesh.SubMeshes == null) continue;
                
                // Get the node transform and name for this mesh (if available)
                Matrix4 nodeTransform = Matrix4.Identity;
                string nodeName = null;
                string expectedTextureName = null;
                
                if (mesh.NodeIndex >= 0 && mesh.NodeIndex < attachmentXac.Nodes.Count)
                {
                    var node = attachmentXac.Nodes[mesh.NodeIndex];
                    nodeName = node.Name;
                    
                    // Derive expected texture name from node name
                    // e.g., "appraisal_c01_face_std" -> "appraisal c01 face std.dds"
                    if (!string.IsNullOrEmpty(nodeName))
                    {
                        expectedTextureName = nodeName.Replace("_", " ") + ".dds";
                    }
                    
                    // Convert XacFile.Matrix44 to OpenTK.Matrix4
                    if (node.Transform.Values != null && node.Transform.Values.Length == 16)
                    {
                        nodeTransform = new Matrix4(
                            node.Transform.Values[0], node.Transform.Values[1], node.Transform.Values[2], node.Transform.Values[3],
                            node.Transform.Values[4], node.Transform.Values[5], node.Transform.Values[6], node.Transform.Values[7],
                            node.Transform.Values[8], node.Transform.Values[9], node.Transform.Values[10], node.Transform.Values[11],
                            node.Transform.Values[12], node.Transform.Values[13], node.Transform.Values[14], node.Transform.Values[15]
                        );
                    }
                    Log($"Mesh uses node '{node.Name}' (index {mesh.NodeIndex}), expected texture: '{expectedTextureName}'");
                }
                
                // Parse all vertex data from attribute layers first (like LoadModel does)
                Vector3[] allPositions = null;
                Vector3[] allNormals = null;
                Vector2[] allUVs = null;
                
                // Extract vertex positions (type 0)
                var posLayer = mesh.VertexAttributes?.Find(a => a.Type == 0);
                if (posLayer != null && posLayer.Data != null)
                {
                    int vertCount = posLayer.Data.Length / 12; // 3 floats * 4 bytes
                    allPositions = new Vector3[vertCount];
                    
                    using (var ms = new MemoryStream(posLayer.Data))
                    using (var br = new BinaryReader(ms))
                    {
                        for (int i = 0; i < vertCount; i++)
                        {
                            allPositions[i] = new Vector3(
                                br.ReadSingle(),
                                br.ReadSingle(),
                                br.ReadSingle()
                            );
                        }
                    }
                }
                
                // Extract normals (type 1)
                var normLayer = mesh.VertexAttributes?.Find(a => a.Type == 1);
                if (normLayer != null && normLayer.Data != null)
                {
                    int normCount = normLayer.Data.Length / 12;
                    allNormals = new Vector3[normCount];
                    
                    using (var ms = new MemoryStream(normLayer.Data))
                    using (var br = new BinaryReader(ms))
                    {
                        for (int i = 0; i < normCount; i++)
                        {
                            allNormals[i] = new Vector3(
                                br.ReadSingle(),
                                br.ReadSingle(),
                                br.ReadSingle()
                            );
                        }
                    }
                }
                
                // Extract UVs (type 3)
                var uvLayer = mesh.VertexAttributes?.Find(a => a.Type == 3);
                if (uvLayer != null && uvLayer.Data != null)
                {
                    int uvSize = uvLayer.AttribSize > 0 ? uvLayer.AttribSize : 8;
                    int uvCount = uvLayer.Data.Length / uvSize;
                    allUVs = new Vector2[uvCount];
                    
                    using (var ms = new MemoryStream(uvLayer.Data))
                    using (var br = new BinaryReader(ms))
                    {
                        for (int i = 0; i < uvCount; i++)
                        {
                            float u = br.ReadSingle();
                            float v = br.ReadSingle();
                            if (uvSize > 8)
                                br.ReadBytes(uvSize - 8);
                            allUVs[i] = new Vector2(u, v);
                        }
                    }
                }
                
                if (allPositions == null)
                {
                    Log("No position data in attachment mesh");
                    continue;
                }
                
                // Get the bone's world position for offsetting the attachment
                Vector3 boneWorldPos = targetBone.WorldPosition;
                Log($"Bone '{boneName}' world position: ({boneWorldPos.X:F2}, {boneWorldPos.Y:F2}, {boneWorldPos.Z:F2})");
                
                // Log ALL vertices for detailed analysis of face/hair mesh shapes
                Log($"Mesh '{nodeName}' has {allPositions.Length} vertices:");
                for (int v = 0; v < Math.Min(allPositions.Length, 20); v++)
                {
                    var pos = allPositions[v];
                    Log($"  v[{v}]: ({pos.X:F2}, {pos.Y:F2}, {pos.Z:F2})");
                }
                if (allPositions.Length > 20)
                    Log($"  ... and {allPositions.Length - 20} more vertices");
                
                // Now create a RenderMesh for each submesh
                // Face/head attachments have vertices that need:
                // 1. Node transform (from the mesh's parent node in the attachment file)
                // 2. Bone world position offset (from the body skeleton)
                
                // Determine layer index from node name (c01=0, c03=1, c05=2, c07=3, c09=4)
                int billboardLayerIndex = 0;
                if (nodeName.Contains("c01")) billboardLayerIndex = 0;
                else if (nodeName.Contains("c03")) billboardLayerIndex = 1;
                else if (nodeName.Contains("c05")) billboardLayerIndex = 2;
                else if (nodeName.Contains("c07")) billboardLayerIndex = 3;
                else if (nodeName.Contains("c09")) billboardLayerIndex = 4;
                else billboardLayerIndex = addedMeshes; // Fallback to mesh count as order
                
                int vertexStart = 0;
                foreach (var subMesh in mesh.SubMeshes)
                {
                    int subVertCount = subMesh.NumVertices;
                    
                    var renderMesh = new RenderMesh
                    {
                        Name = nodeName,
                        TextureName = expectedTextureName,
                        MaterialId = subMesh.MaterialId,
                        Color = Color.FromArgb(180, 180, 190),
                        // Face/hair attachments are billboards - they need alpha blending
                        // and special rendering to look correct
                        IsBillboard = true,
                        BillboardCenter = boneWorldPos,
                        BillboardLayerIndex = billboardLayerIndex
                    };
                    
                    // Store ORIGINAL vertices for billboard calculation
                    // Transform vertices: add bone world position to place attachment on skeleton
                    if (allPositions != null && vertexStart + subVertCount <= allPositions.Length)
                    {
                        // Store original local positions
                        renderMesh.OriginalVertices = new Vector3[subVertCount];
                        renderMesh.Vertices = new Vector3[subVertCount];
                        for (int i = 0; i < subVertCount; i++)
                        {
                            renderMesh.OriginalVertices[i] = allPositions[vertexStart + i];
                            // Position = local vertex + bone world position
                            renderMesh.Vertices[i] = allPositions[vertexStart + i] + boneWorldPos;
                        }
                        
                        // Log first vertex info
                        if (renderMesh.Vertices.Length > 0)
                        {
                            var v = renderMesh.Vertices[0];
                            var orig = allPositions[vertexStart];
                            Log($"Attachment layer {billboardLayerIndex}: local=({orig.X:F2},{orig.Y:F2},{orig.Z:F2}), world=({v.X:F2},{v.Y:F2},{v.Z:F2})");
                        }
                    }
                    
                    // Copy normals
                    if (allNormals != null && vertexStart + subVertCount <= allNormals.Length)
                    {
                        renderMesh.Normals = new Vector3[subVertCount];
                        for (int i = 0; i < subVertCount; i++)
                        {
                            renderMesh.Normals[i] = allNormals[vertexStart + i];
                        }
                    }
                    
                    // Copy UVs
                    if (allUVs != null && vertexStart + subVertCount <= allUVs.Length)
                    {
                        renderMesh.UVs = new Vector2[subVertCount];
                        renderMesh.OriginalUVs = new Vector2[subVertCount]; // Store for sprite sheet animation
                        Array.Copy(allUVs, vertexStart, renderMesh.UVs, 0, subVertCount);
                        Array.Copy(allUVs, vertexStart, renderMesh.OriginalUVs, 0, subVertCount);
                        
                        // Log UV bounds and vertex-UV pairs for debugging face orientation
                        float minU = float.MaxValue, maxU = float.MinValue;
                        float minV = float.MaxValue, maxV = float.MinValue;
                        for (int i = 0; i < subVertCount; i++)
                        {
                            var uv = renderMesh.UVs[i];
                            if (uv.X < minU) minU = uv.X;
                            if (uv.X > maxU) maxU = uv.X;
                            if (uv.Y < minV) minV = uv.Y;
                            if (uv.Y > maxV) maxV = uv.Y;
                        }
                        Log($"  UV bounds: U=[{minU:F3}, {maxU:F3}], V=[{minV:F3}, {maxV:F3}]");
                        
                        // Log all vertex+UV pairs for small meshes (face quads)
                        if (subVertCount <= 8)
                        {
                            Log($"  Vertex+UV pairs for {nodeName}:");
                            for (int i = 0; i < subVertCount; i++)
                            {
                                var v = renderMesh.OriginalVertices[i];
                                var uv = renderMesh.UVs[i];
                                Log($"    [{i}] Pos=({v.X:F2},{v.Y:F2},{v.Z:F2}) UV=({uv.X:F3},{uv.Y:F3})");
                            }
                        }
                    }
                    
                    // Copy indices
                    if (subMesh.Indices != null)
                    {
                        renderMesh.Indices = new int[subMesh.NumIndices];
                        Array.Copy(subMesh.Indices, 0, renderMesh.Indices, 0, subMesh.NumIndices);
                    }
                    
                    vertexStart += subVertCount;
                    
                    _meshes.Add(renderMesh);
                    addedMeshes++;
                    addedVerts += subVertCount;
                }
            }
            
            // Update bounds
            RecalculateBounds();
            
            // Update info
            int totalVerts = _meshes.Sum(m => m.Vertices?.Length ?? 0);
            int totalTris = _meshes.Sum(m => (m.Indices?.Length ?? 0) / 3);
            string info = $"Meshes: {_meshes.Count} | Vertices: {totalVerts:N0} | Triangles: {totalTris:N0}";
            if (_bones.Count > 0)
                info += $" | Bones: {_bones.Count}";
            info += $" | +Attachment on {boneName}";
            UpdateInfoLabel(info);
            
            Log($"Added attachment: {addedMeshes} meshes, {addedVerts} vertices on '{boneName}'");
            Refresh();
            
            return true;
        }

        /// <summary>
        /// Load a texture from a bitmap for a specific material ID
        /// </summary>
        public void LoadTexture(int materialId, System.Drawing.Bitmap bitmap)
        {
            if (!_glInitialized || bitmap == null) return;
            
            _glControl.MakeCurrent();
            
            // Delete existing texture if any
            if (_textureIds.ContainsKey(materialId))
            {
                GL.DeleteTexture(_textureIds[materialId]);
            }
            
            // Create new texture
            int texId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texId);
            
            // Lock bitmap data - no flip needed, UVs handle orientation
            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            // Upload to GPU
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                bitmap.Width, bitmap.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
            
            bitmap.UnlockBits(bmpData);
            
            // Generate mipmaps for better quality at distance
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            
            // Set texture parameters with mipmapping and high quality filtering
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            
            // Enable anisotropic filtering for better quality at oblique angles
            float maxAniso;
            GL.GetFloat((GetPName)ExtTextureFilterAnisotropic.MaxTextureMaxAnisotropyExt, out maxAniso);
            GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt, maxAniso);
            
            _textureIds[materialId] = texId;
            
            Log($"LoadTexture: materialId={materialId}, textureId={texId}, size={bitmap.Width}x{bitmap.Height}");
            
            // Assign texture to BODY meshes only (not attachments)
            int endIndex = _attachmentStartIndex >= 0 ? _attachmentStartIndex : _meshes.Count;
            for (int i = 0; i < endIndex; i++)
            {
                var mesh = _meshes[i];
                if (mesh.UVs != null && mesh.UVs.Length > 0)
                {
                    mesh.TextureId = texId;
                    Log($"  Assigned to body mesh {i}, UVs={mesh.UVs.Length}");
                }
                else
                {
                    Log($"  Skipped mesh {i} (no UVs)");
                }
            }
            
            Refresh();
        }
        
        /// <summary>
        /// Load a texture and apply only to attachment meshes (face, hair, etc.)
        /// If textureFileName is provided, will try to match to specific mesh
        /// </summary>
        public void LoadAttachmentTexture(System.Drawing.Bitmap bitmap, string textureFileName = null)
        {
            if (!_glInitialized || bitmap == null) return;
            if (_attachmentStartIndex < 0)
            {
                Log("No attachments loaded to apply texture to");
                return;
            }
            
            _glControl.MakeCurrent();
            
            // Create new texture
            int texId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texId);
            
            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                bitmap.Width, bitmap.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
            
            bitmap.UnlockBits(bmpData);
            
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            
            Log($"LoadAttachmentTexture: textureId={texId}, size={bitmap.Width}x{bitmap.Height}, filename={textureFileName ?? "none"}");
            
            // Detect sprite sheet layout based on texture dimensions
            // TOS face/hair textures use specific layouts:
            // - Face textures: 5 columns × 4 rows (each sprite ~92×96)
            // - Hair textures: typically 8×8 grid or similar
            int spriteColumns = 1, spriteRows = 1;
            bool isSpriteSheet = false;
            
            // Known TOS sprite sheet sizes (width × height -> columns × rows)
            // Face sprites are ~92×96 pixels each
            // Hair sprites vary but typically ~76×76 for 608×608 textures
            if (bitmap.Width == 460 && bitmap.Height == 384)
            {
                // 460/5 = 92, 384/4 = 96 -> 5×4 = 20 frames
                spriteColumns = 5;
                spriteRows = 4;
                isSpriteSheet = true;
            }
            else if (bitmap.Width == 440 && bitmap.Height == 384)
            {
                // Similar to above, slightly different width
                spriteColumns = 5;
                spriteRows = 4;
                isSpriteSheet = true;
            }
            else if (bitmap.Width == 608 && bitmap.Height == 608)
            {
                // Hair texture - 608x608 is square but likely NOT a sprite sheet
                // The hair meshes are complex 3D shapes, not simple billboards
                // Don't treat as sprite sheet - just use the texture directly
                spriteColumns = 1;
                spriteRows = 1;
                isSpriteSheet = false;
            }
            else if (bitmap.Width >= 400 && bitmap.Height >= 300)
            {
                // Generic detection for other face textures
                // Assume ~92×96 sprite size
                spriteColumns = Math.Max(1, bitmap.Width / 92);
                spriteRows = Math.Max(1, bitmap.Height / 96);
                if (spriteColumns > 1 && spriteRows > 1) isSpriteSheet = true;
            }
            else if (bitmap.Width >= 500 && bitmap.Height >= 500)
            {
                // Large square texture (hair) - assume ~76×76 sprites
                spriteColumns = Math.Max(1, bitmap.Width / 76);
                spriteRows = Math.Max(1, bitmap.Height / 76);
                isSpriteSheet = true;
            }
            
            if (isSpriteSheet)
                Log($"  Detected sprite sheet: {spriteColumns}x{spriteRows} = {spriteColumns * spriteRows} frames");
            
            // Normalize texture filename for matching (remove path, lowercase, underscores)
            string normalizedTextureName = null;
            if (!string.IsNullOrEmpty(textureFileName))
            {
                normalizedTextureName = Path.GetFileName(textureFileName).ToLowerInvariant().Replace(" ", "_");
            }
            
            int assignedCount = 0;
            
            // First pass: try exact matching
            // Apply ONLY to attachment meshes (from _attachmentStartIndex onwards)
            for (int i = _attachmentStartIndex; i < _meshes.Count; i++)
            {
                var mesh = _meshes[i];
                if (mesh.UVs == null || mesh.UVs.Length == 0) continue;
                
                // If texture filename provided, try to match to specific mesh
                if (normalizedTextureName != null && !string.IsNullOrEmpty(mesh.TextureName))
                {
                    string normalizedMeshTexture = mesh.TextureName.ToLowerInvariant().Replace(" ", "_");
                    
                    // Check for exact match
                    if (normalizedMeshTexture == normalizedTextureName)
                    {
                        mesh.TextureId = texId;
                        mesh.IsSpriteSheet = isSpriteSheet;
                        mesh.SpriteColumns = spriteColumns;
                        mesh.SpriteRows = spriteRows;
                        assignedCount++;
                        Log($"  Exact match: mesh {i} ('{mesh.Name}'), expected='{mesh.TextureName}', spriteSheet={isSpriteSheet}");
                    }
                }
                else if (normalizedTextureName == null)
                {
                    // No filename provided - apply to all attachment meshes
                    mesh.TextureId = texId;
                    mesh.IsSpriteSheet = isSpriteSheet;
                    mesh.SpriteColumns = spriteColumns;
                    mesh.SpriteRows = spriteRows;
                    assignedCount++;
                    Log($"  Assigned to attachment mesh {i}, UVs={mesh.UVs.Length}, spriteSheet={isSpriteSheet}");
                }
            }
            
            // Second pass: if no exact matches, try partial matching (for hair layers that share one texture)
            // Hair meshes: "appraisal_c01_hair_std" -> expected "appraisal c01 hair std.dds"
            // Hair texture: "appraisal_hair_std.dds"
            // We match if removing the c0X suffix from mesh name gives the texture name
            if (assignedCount == 0 && normalizedTextureName != null)
            {
                Log($"  No exact matches, trying partial matching for shared textures");
                for (int i = _attachmentStartIndex; i < _meshes.Count; i++)
                {
                    var mesh = _meshes[i];
                    if (mesh.UVs == null || mesh.UVs.Length == 0) continue;
                    if (string.IsNullOrEmpty(mesh.TextureName)) continue;
                    
                    string normalizedMeshTexture = mesh.TextureName.ToLowerInvariant().Replace(" ", "_");
                    
                    // Remove c0X pattern from mesh texture name (e.g., "appraisal_c01_hair_std.dds" -> "appraisal_hair_std.dds")
                    string meshTextureWithoutLayer = System.Text.RegularExpressions.Regex.Replace(
                        normalizedMeshTexture, @"_c\d+_", "_");
                    
                    if (meshTextureWithoutLayer == normalizedTextureName)
                    {
                        mesh.TextureId = texId;
                        mesh.IsSpriteSheet = isSpriteSheet;
                        mesh.SpriteColumns = spriteColumns;
                        mesh.SpriteRows = spriteRows;
                        assignedCount++;
                        Log($"  Partial match (shared texture): mesh {i} ('{mesh.Name}'), spriteSheet={isSpriteSheet} ({spriteColumns}x{spriteRows})");
                    }
                }
            }
            
            Log($"  Total assigned: {assignedCount} meshes");
            
            Refresh();
        }

        public void Clear()
        {
            _meshes.Clear();
            _bones.Clear();
            _hasModel = false;
            _attachmentStartIndex = -1;
            _attachmentTextureNames.Clear();
            UpdateInfoLabel("No model loaded");
            Refresh();
        }
        
        private void CalculateBoneWorldTransform(int boneIndex)
        {
            if (boneIndex < 0 || boneIndex >= _bones.Count)
                return;
                
            var bone = _bones[boneIndex];
            
            if (bone.ParentIndex < 0 || bone.ParentIndex >= _bones.Count)
            {
                // Root bone - world transform equals local transform
                bone.WorldTransform = bone.LocalTransform;
            }
            else
            {
                // Child bone - multiply parent world transform by local transform
                // In Assimp: transform = parent.transform * child.transform
                // In OpenTK (row-major): child * parent gives same result
                var parent = _bones[bone.ParentIndex];
                bone.WorldTransform = parent.WorldTransform * bone.LocalTransform;
            }
            
            // Extract world position from the world transform matrix
            // In OpenTK Matrix4, translation is at M41, M42, M43 (row 4, column 1-3)
            bone.WorldPosition = new Vector3(
                bone.WorldTransform.M41,
                bone.WorldTransform.M42,
                bone.WorldTransform.M43
            );
        }
        
        private Vector3 CalculateBoneWorldPosition(int boneIndex)
        {
            if (boneIndex < 0 || boneIndex >= _bones.Count)
                return Vector3.Zero;
                
            var bone = _bones[boneIndex];
            Vector3 worldPos = bone.LocalPosition;
            
            // Walk up parent chain to accumulate transforms
            int parentIdx = bone.ParentIndex;
            while (parentIdx >= 0 && parentIdx < _bones.Count)
            {
                worldPos += _bones[parentIdx].LocalPosition;
                parentIdx = _bones[parentIdx].ParentIndex;
            }
            
            return worldPos;
        }

        private void UpdateInfoLabel(string text)
        {
            var label = this.Controls.Find("InfoLabel", false);
            if (label.Length > 0)
            {
                label[0].Text = text;
            }
            StatusChanged?.Invoke(this, text);
        }
        
        #region Animation Methods
        
        /// <summary>
        /// Load an XSM animation file for playback
        /// </summary>
        public void LoadAnimation(FileFormats.XSM.XsmFile animation)
        {
            _currentAnimation = animation;
            _animationTime = 0f;
            _isAnimationPlaying = false;
            
            // Enable/disable toolbar buttons
            if (_playAnimBtn != null)
                _playAnimBtn.Enabled = animation != null;
            if (_stopAnimBtn != null)
                _stopAnimBtn.Enabled = animation != null;
            
            if (animation == null)
            {
                Log("Animation cleared");
                UpdateInfoLabel("Animation cleared");
                return;
            }
            
            Log($"=== Animation Loaded ===");
            Log($"Name: {animation.Metadata?.MotionName ?? "Unknown"}");
            Log($"FPS: {animation.Metadata?.FPS ?? 30}");
            Log($"Motion Parts: {animation.MotionParts.Count}");
            foreach (var part in animation.MotionParts)
            {
                Log($"  {part.Name}: Pos={part.NumPositionKeys}, Rot={part.NumRotationKeys}, Scale={part.NumScaleKeys}");
            }
            
            float duration = GetAnimationDuration();
            UpdateInfoLabel($"Animation: {animation.Metadata?.MotionName ?? "Unknown"} | Duration: {duration:F2}s | Parts: {animation.MotionParts.Count}");
        }
        
        /// <summary>
        /// Get the total duration of the current animation in seconds
        /// </summary>
        public float GetAnimationDuration()
        {
            if (_currentAnimation == null) return 0f;
            
            float maxTime = 0f;
            foreach (var part in _currentAnimation.MotionParts)
            {
                if (part.PositionKeys != null)
                    foreach (var key in part.PositionKeys)
                        if (key.Time > maxTime) maxTime = key.Time;
                if (part.RotationKeys != null)
                    foreach (var key in part.RotationKeys)
                        if (key.Time > maxTime) maxTime = key.Time;
                if (part.ScaleKeys != null)
                    foreach (var key in part.ScaleKeys)
                        if (key.Time > maxTime) maxTime = key.Time;
            }
            return maxTime;
        }
        
        /// <summary>
        /// Start playing the animation
        /// </summary>
        public void PlayAnimation()
        {
            if (_currentAnimation == null) return;
            
            _isAnimationPlaying = true;
            _lastAnimationUpdate = DateTime.Now;
            
            // Initialize timer if needed
            if (_animationTimer == null)
            {
                _animationTimer = new System.Windows.Forms.Timer();
                _animationTimer.Interval = 16; // ~60 FPS
                _animationTimer.Tick += AnimationTimer_Tick;
            }
            _animationTimer.Start();
            
            UpdateInfoLabel($"Playing: {_currentAnimation.Metadata?.MotionName ?? "Animation"}");
        }
        
        /// <summary>
        /// Pause the animation
        /// </summary>
        public void PauseAnimation()
        {
            _isAnimationPlaying = false;
            _animationTimer?.Stop();
            UpdateInfoLabel($"Paused at {_animationTime:F2}s");
        }
        
        /// <summary>
        /// Stop and reset the animation to the beginning
        /// </summary>
        public void StopAnimation()
        {
            _isAnimationPlaying = false;
            _animationTimer?.Stop();
            _animationTime = 0f;
            
            // Reset bones to bind pose
            ResetBonesToBindPose();
            Refresh();
            
            UpdateInfoLabel("Animation stopped");
        }
        
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (!_isAnimationPlaying || _currentAnimation == null) return;
            
            // Calculate delta time
            var now = DateTime.Now;
            float deltaTime = (float)(now - _lastAnimationUpdate).TotalSeconds;
            _lastAnimationUpdate = now;
            
            // Advance animation time
            _animationTime += deltaTime;
            
            // Loop animation
            float duration = GetAnimationDuration();
            if (duration > 0 && _animationTime >= duration)
            {
                _animationTime = _animationTime % duration;
            }
            
            // Apply animation to bones
            ApplyAnimationFrame(_animationTime);
            
            // Redraw
            Refresh();
        }
        
        private void ApplyAnimationFrame(float time)
        {
            if (_currentAnimation == null || _bones.Count == 0) return;
            
            // Apply each motion part to corresponding bone
            foreach (var part in _currentAnimation.MotionParts)
            {
                // Find bone by name
                var bone = _bones.FirstOrDefault(b => b.Name == part.Name);
                if (bone == null) continue;
                
                // Interpolate position
                var position = InterpolatePosition(part, time);
                
                // Interpolate rotation
                var rotation = InterpolateRotation(part, time);
                
                // Interpolate scale
                var scale = InterpolateScale(part, time);
                
                // Update bone local transform
                Matrix4 scaleMatrix = Matrix4.CreateScale(scale.X, scale.Y, scale.Z);
                Matrix4 rotMatrix = Matrix4.CreateFromQuaternion(new OpenTK.Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W));
                Matrix4 transMatrix = Matrix4.CreateTranslation(position.X, position.Y, position.Z);
                
                bone.LocalTransform = scaleMatrix * rotMatrix * transMatrix;
                bone.LocalPosition = new Vector3(position.X, position.Y, position.Z);
            }
            
            // Recalculate world transforms
            RecalculateBoneWorldTransforms();
        }
        
        private FileFormats.XSM.Vector3 InterpolatePosition(FileFormats.XSM.XsmMotionPart part, float time)
        {
            if (part.PositionKeys == null || part.PositionKeys.Count == 0)
                return part.PosePosition;
            
            if (part.PositionKeys.Count == 1)
                return part.PositionKeys[0].Position;
            
            // Find keyframes to interpolate between
            var keys = part.PositionKeys;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (time >= keys[i].Time && time <= keys[i + 1].Time)
                {
                    float t = (time - keys[i].Time) / (keys[i + 1].Time - keys[i].Time);
                    return LerpVector3(keys[i].Position, keys[i + 1].Position, t);
                }
            }
            
            return keys[keys.Count - 1].Position;
        }
        
        private FileFormats.XSM.Quaternion InterpolateRotation(FileFormats.XSM.XsmMotionPart part, float time)
        {
            if (part.RotationKeys == null || part.RotationKeys.Count == 0)
                return part.PoseRotation;
            
            if (part.RotationKeys.Count == 1)
                return part.RotationKeys[0].Rotation;
            
            // Find keyframes to interpolate between
            var keys = part.RotationKeys;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (time >= keys[i].Time && time <= keys[i + 1].Time)
                {
                    float t = (time - keys[i].Time) / (keys[i + 1].Time - keys[i].Time);
                    return SlerpQuaternion(keys[i].Rotation, keys[i + 1].Rotation, t);
                }
            }
            
            return keys[keys.Count - 1].Rotation;
        }
        
        private FileFormats.XSM.Vector3 InterpolateScale(FileFormats.XSM.XsmMotionPart part, float time)
        {
            if (part.ScaleKeys == null || part.ScaleKeys.Count == 0)
                return part.PoseScale;
            
            if (part.ScaleKeys.Count == 1)
                return part.ScaleKeys[0].Scale;
            
            // Find keyframes to interpolate between
            var keys = part.ScaleKeys;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (time >= keys[i].Time && time <= keys[i + 1].Time)
                {
                    float t = (time - keys[i].Time) / (keys[i + 1].Time - keys[i].Time);
                    return LerpVector3(keys[i].Scale, keys[i + 1].Scale, t);
                }
            }
            
            return keys[keys.Count - 1].Scale;
        }
        
        private FileFormats.XSM.Vector3 LerpVector3(FileFormats.XSM.Vector3 a, FileFormats.XSM.Vector3 b, float t)
        {
            return new FileFormats.XSM.Vector3
            {
                X = a.X + (b.X - a.X) * t,
                Y = a.Y + (b.Y - a.Y) * t,
                Z = a.Z + (b.Z - a.Z) * t
            };
        }
        
        private FileFormats.XSM.Quaternion SlerpQuaternion(FileFormats.XSM.Quaternion a, FileFormats.XSM.Quaternion b, float t)
        {
            // Convert to OpenTK quaternions for slerp, then back
            var qa = new OpenTK.Quaternion(a.X, a.Y, a.Z, a.W);
            var qb = new OpenTK.Quaternion(b.X, b.Y, b.Z, b.W);
            var result = OpenTK.Quaternion.Slerp(qa, qb, t);
            return new FileFormats.XSM.Quaternion
            {
                X = result.X,
                Y = result.Y,
                Z = result.Z,
                W = result.W
            };
        }
        
        private void RecalculateBoneWorldTransforms()
        {
            for (int i = 0; i < _bones.Count; i++)
            {
                var bone = _bones[i];
                
                if (bone.ParentIndex < 0 || bone.ParentIndex >= _bones.Count)
                {
                    bone.WorldTransform = bone.LocalTransform;
                    bone.WorldPosition = bone.LocalPosition;
                }
                else
                {
                    var parent = _bones[bone.ParentIndex];
                    bone.WorldTransform = bone.LocalTransform * parent.WorldTransform;
                    
                    Vector4 localPos4 = new Vector4(bone.LocalPosition, 1.0f);
                    Vector4 worldPos4 = Vector4.Transform(localPos4, parent.WorldTransform);
                    bone.WorldPosition = new Vector3(worldPos4.X, worldPos4.Y, worldPos4.Z);
                }
            }
        }
        
        private void ResetBonesToBindPose()
        {
            // For now, just keep the original transforms - would need to store bind pose separately
        }
        
        #endregion

        #endregion
    }

    #region Helper Classes

    public class RenderMesh
    {
        public string Name { get; set; } // Node/mesh name for texture matching
        public string TextureName { get; set; } // Expected texture filename
        public Vector3[] Vertices { get; set; }
        public Vector3[] OriginalVertices { get; set; } // Store original vertices for billboard computation
        public Vector3[] Normals { get; set; }
        public Vector2[] UVs { get; set; }
        public Vector2[] OriginalUVs { get; set; } // Store original UVs for sprite sheet animation
        public int[] Indices { get; set; }
        public Color Color { get; set; }
        public int MaterialId { get; set; }
        public int TextureId { get; set; } = 0; // OpenGL texture ID
        public bool IsBillboard { get; set; } = false; // Face/hair billboards that face camera
        public Vector3 BillboardCenter { get; set; } // Center point for billboard rotation
        public int BillboardLayerIndex { get; set; } = 0; // Layer stacking index (0=front, 1=next, etc.)
        
        // Sprite sheet properties for face/hair animation
        public bool IsSpriteSheet { get; set; } = false; // Whether texture is a sprite sheet
        public int SpriteColumns { get; set; } = 1; // Number of columns in sprite sheet
        public int SpriteRows { get; set; } = 1; // Number of rows in sprite sheet
        public int CurrentSpriteFrame { get; set; } = 0; // Current frame to display
    }

    public class RenderBone
    {
        public string Name { get; set; }
        public int ParentIndex { get; set; }
        public Vector3 LocalPosition { get; set; }
        public Vector3 WorldPosition { get; set; }
        public Matrix4 LocalTransform { get; set; } = Matrix4.Identity;
        public Matrix4 WorldTransform { get; set; } = Matrix4.Identity;
    }

    public class BoundingBox
    {
        public Vector3 Min { get; set; }
        public Vector3 Max { get; set; }
    }

    public class MaterialInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<string> TextureNames { get; set; } = new List<string>();
        public bool HasTexture => TextureNames.Count > 0;
    }

    #endregion
}
