// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using IPFBrowser.FileFormats;
using IPFBrowser.FileFormats.DDS;
using IPFBrowser.FileFormats.IES;
using IPFBrowser.FileFormats.IPF;
using IPFBrowser.FileFormats.TGA;
using IPFBrowser.FileFormats.XAC;
using IPFBrowser.FileFormats.XSM;
using IPFBrowser.Viewer3D;
using ScintillaNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace IPFBrowser
{
	public partial class FrmMain : Form
	{
		private Ipf _openedIpf;

		private Dictionary<string, List<string>> _folders = new Dictionary<string, List<string>>();
		private Dictionary<string, IpfFile> _files = new Dictionary<string, IpfFile>();

		private Dictionary<string, FileFormat> _fileTypes = new Dictionary<string, FileFormat>();

		// IES Editing state
		private bool _isEditingIes = false;
		private string _currentIesFilePath = null;
		private string _currentIesXml = null;  // Track current XML being edited

		// Pending changes - stores modified IES data (as XML) until saved to IPF
		private Dictionary<string, string> _pendingChanges = new Dictionary<string, string>();

		// Imported files - stores new files to be added to the IPF (path -> binary data)
		private Dictionary<string, byte[]> _importedFiles = new Dictionary<string, byte[]>();

		// Files marked for deletion from IPF
		private HashSet<string> _deletedFiles = new HashSet<string>();

		// 3D Viewer
		private ModelViewerControl _modelViewer;
		
		// Multiple IPF support - load additional IPFs for textures, models, etc.
		private List<Ipf> _additionalIpfs = new List<Ipf>();
		private Dictionary<string, IpfFile> _additionalFiles = new Dictionary<string, IpfFile>();
		
		// Context menu for file list
		private ContextMenuStrip _fileListContextMenu;
		
		// Flag to prevent preview change on right-click
		private bool _isRightClick = false;
		
		// Track selected tree nodes for multi-select
		private List<TreeNode> _selectedTreeNodes = new List<TreeNode>();

		/// <summary>
		/// Initializes form.
		/// </summary>
		/// <param name="args"></param>
		public FrmMain(string[] args)
		{
			InitializeComponent();

			// Hide toolbar buttons - we use double-click/context-menu for editing now
			try
			{
				BtnEditIes.Visible = false;
				BtnCancelEdit.Visible = false;
			}
			catch { /* Designer may not have these controls in some layouts */ }

			// File list interaction: double-click to edit, right-click for context menu
			LstFiles.DoubleClick += LstFiles_DoubleClick;

			// Add shortcut for Discard All Changes (already implemented in MnuDiscardChanges handler)
			try
			{
				MnuDiscardChanges.Shortcut = Shortcut.CtrlShiftK; // Ctrl+Shift+K to discard all
			}
			catch { /* Designer item may not be initialized in rare cases */ }

			// Initialize file types
			_fileTypes[".ies"] = new FileFormat("table.png", PreviewType.IesTable);

			_fileTypes[".lua"] = new FileFormat("page_white_code.png", PreviewType.Text, Lexer.Lua);
			_fileTypes[".txt"] = new FileFormat("page_white_text.png", PreviewType.Text, Lexer.Null);
			_fileTypes[".lst"] = new FileFormat("page_white_text.png", PreviewType.Text, Lexer.Null);
			_fileTypes[".fx"] = new FileFormat("page_white_code.png", PreviewType.Text, Lexer.Cpp);

			_fileTypes[".dds"] = new FileFormat("image.png", PreviewType.DdsImage);
			_fileTypes[".tga"] = new FileFormat("image.png", PreviewType.TgaImage);
			_fileTypes[".ttf"] = new FileFormat("image.png", PreviewType.TtfFont);

			_fileTypes[".xml"] = new FileFormat("page_white_code.png", PreviewType.Text, Lexer.Xml);
			_fileTypes[".effect"] = _fileTypes[".xml"];
			_fileTypes[".skn"] = _fileTypes[".xml"];
			_fileTypes[".xsd"] = _fileTypes[".xml"];
			_fileTypes[".sani"] = _fileTypes[".xml"];

			_fileTypes[".jpg"] = new FileFormat("image.png", PreviewType.Image);
			_fileTypes[".bmp"] = _fileTypes[".jpg"];
			_fileTypes[".png"] = _fileTypes[".jpg"];

			// 3D Model formats
			_fileTypes[".xac"] = new FileFormat("brick.png", PreviewType.Model3D);
			_fileTypes[".xsm"] = new FileFormat("film.png", PreviewType.Animation3D);
			_fileTypes[".xpm"] = new FileFormat("brick.png", PreviewType.HexView);  // Pose/Morph data
			_fileTypes[".colmesh"] = new FileFormat("brick.png", PreviewType.HexView);

			// World/Level formats (hex view for now)
			_fileTypes[".3dworld"] = new FileFormat("world.png", PreviewType.HexView);
			_fileTypes[".3dprop"] = new FileFormat("brick.png", PreviewType.HexView);
			_fileTypes[".3drender"] = new FileFormat("page_white.png", PreviewType.HexView);
			_fileTypes[".3deffect"] = new FileFormat("lightning.png", PreviewType.HexView);
			_fileTypes[".pathengine"] = new FileFormat("map.png", PreviewType.HexView);
			_fileTypes[".tok"] = new FileFormat("page_white.png", PreviewType.HexView);
			_fileTypes[".lightcell"] = new FileFormat("lightbulb.png", PreviewType.HexView);

			// Effect formats
			_fileTypes[".fxdb"] = new FileFormat("page_white_code.png", PreviewType.HexView);
			_fileTypes[".psb"] = new FileFormat("lightning.png", PreviewType.HexView);
			_fileTypes[".eft"] = new FileFormat("lightning.png", PreviewType.HexView);

			// Sprite formats
			_fileTypes[".ibp"] = new FileFormat("picture.png", PreviewType.HexView);
			_fileTypes[".sprbin"] = new FileFormat("picture.png", PreviewType.HexView);
			_fileTypes[".actbin"] = new FileFormat("film.png", PreviewType.HexView);
			_fileTypes[".act"] = new FileFormat("film.png", PreviewType.HexView);
			_fileTypes[".spr"] = new FileFormat("picture.png", PreviewType.HexView);
			_fileTypes[".colmap"] = new FileFormat("color_swatch.png", PreviewType.HexView);
			_fileTypes[".dead"] = new FileFormat("film.png", PreviewType.HexView);

			// Animation timing
			_fileTypes[".xsmtime"] = new FileFormat("film.png", PreviewType.HexView);
			_fileTypes[".sklm"] = new FileFormat("brick.png", PreviewType.HexView);

			// Audio formats
			_fileTypes[".fsb"] = new FileFormat("sound.png", PreviewType.HexView);
			_fileTypes[".fev"] = new FileFormat("sound.png", PreviewType.HexView);
			_fileTypes[".fdp"] = new FileFormat("sound.png", PreviewType.HexView);
			_fileTypes[".mp3"] = new FileFormat("sound.png", PreviewType.HexView);
			_fileTypes[".snd"] = new FileFormat("sound.png", PreviewType.HexView);
			_fileTypes[".h"] = new FileFormat("page_white_code.png", PreviewType.Text, Lexer.Cpp);

			// World/Map additional formats
			_fileTypes[".3dzone"] = new FileFormat("world.png", PreviewType.HexView);
			_fileTypes[".imctree"] = new FileFormat("brick.png", PreviewType.HexView);
			_fileTypes[".bgcolmesh"] = new FileFormat("brick.png", PreviewType.HexView);
			_fileTypes[".wmove"] = new FileFormat("map.png", PreviewType.HexView);

			// Shader formats
			_fileTypes[".fxh"] = new FileFormat("page_white_code.png", PreviewType.Text, Lexer.Cpp);
			_fileTypes[".cam"] = new FileFormat("camera.png", PreviewType.HexView);

			// Script API
			_fileTypes[".export"] = new FileFormat("page_white_code.png", PreviewType.Text, Lexer.Null);

			// Misc binary
			_fileTypes[".lma"] = new FileFormat("lightbulb.png", PreviewType.HexView);
			_fileTypes[".bin"] = new FileFormat("page_white.png", PreviewType.HexView);
			_fileTypes[".db"] = new FileFormat("database.png", PreviewType.HexView);
			_fileTypes[".x"] = new FileFormat("brick.png", PreviewType.HexView);  // Legacy DirectX mesh
			_fileTypes[".max"] = new FileFormat("brick.png", PreviewType.HexView);  // 3ds Max scene
			_fileTypes[".jpeg"] = _fileTypes[".jpg"];

			// Prepare code preview
			TxtPreview.Dock = DockStyle.Fill;
			TxtPreview.Visible = false;
			TxtPreview.Margins[0].Width = 40;

			// Dock preview elements
			PnlImagePreview.Dock = DockStyle.Fill;
			LblPreview.Dock = DockStyle.Fill;
			GridPreview.Dock = DockStyle.Fill;

			// Initialize 3D viewer
			try
			{
				_modelViewer = new ModelViewerControl();
				_modelViewer.Dock = DockStyle.Fill;
				_modelViewer.Visible = false;
				_modelViewer.TexturesNeeded += ModelViewer_TexturesNeeded;
				SplFiles.Panel2.Controls.Add(_modelViewer);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Failed to initialize 3D viewer: {ex.Message}");
			}

			// Add Tools menu between File and ? (insert at index 1)
			var toolsMenu = new MenuItem("Tools");
			var testViewerItem = new MenuItem("Test 3D Viewer (Load Example XAC)");
			testViewerItem.Click += TestViewerItem_Click;
			toolsMenu.MenuItems.Add(testViewerItem);
			
			toolsMenu.MenuItems.Add(new MenuItem("-")); // Separator
			
			var loadAdditionalIpfItem = new MenuItem("Load Additional IPF...");
			loadAdditionalIpfItem.Click += LoadAdditionalIpf_Click;
			toolsMenu.MenuItems.Add(loadAdditionalIpfItem);
			
			var closeAdditionalIpfsItem = new MenuItem("Close All Additional IPFs");
			closeAdditionalIpfsItem.Click += CloseAdditionalIpfs_Click;
			toolsMenu.MenuItems.Add(closeAdditionalIpfsItem);
			
			toolsMenu.MenuItems.Add(new MenuItem("-")); // Separator
			
			var importIesItem = new MenuItem("Import File(s)...");
			importIesItem.Click += ImportIesFile_Click;
			toolsMenu.MenuItems.Add(importIesItem);
			
			// Insert Tools menu at index 1 (after File, before ?)
			mainMenu1.MenuItems.Add(1, toolsMenu);

			// Add Save/Load Session to File menu
			var saveSessionItem = new MenuItem("Save Session...");
			saveSessionItem.Click += SaveSession_Click;
			mainMenu1.MenuItems[0].MenuItems.Add(saveSessionItem);

			var loadSessionItem = new MenuItem("Load Session...");
			loadSessionItem.Click += LoadSession_Click;
			mainMenu1.MenuItems[0].MenuItems.Add(loadSessionItem);

			// Create context menu for file list
			_fileListContextMenu = new ContextMenuStrip();
			var applyTextureMenuItem = new ToolStripMenuItem("Apply as Texture to Body");
			applyTextureMenuItem.Name = "applyTexture";
			applyTextureMenuItem.Click += ApplyTextureToModel_Click;
			_fileListContextMenu.Items.Add(applyTextureMenuItem);
			
			var applyFaceTextureMenuItem = new ToolStripMenuItem("Apply as Texture to Face/Attachment");
			applyFaceTextureMenuItem.Name = "applyFaceTexture";
			applyFaceTextureMenuItem.Click += ApplyTextureToAttachment_Click;
			_fileListContextMenu.Items.Add(applyFaceTextureMenuItem);
			
			var applyAttachmentMenuItem = new ToolStripMenuItem("Apply as Attachment to 3D Model (Face/Hair)");
			applyAttachmentMenuItem.Name = "applyAttachment";
			applyAttachmentMenuItem.Click += ApplyAttachmentToModel_Click;
			_fileListContextMenu.Items.Add(applyAttachmentMenuItem);
			
			var applyAnimationMenuItem = new ToolStripMenuItem("Apply Animation to Model");
			applyAnimationMenuItem.Name = "applyAnimation";
			applyAnimationMenuItem.Click += ApplyAnimationToModel_Click;
			_fileListContextMenu.Items.Add(applyAnimationMenuItem);
			
			var showRequiredTexturesMenuItem = new ToolStripMenuItem("Show Required Textures");
			showRequiredTexturesMenuItem.Name = "showRequiredTextures";
			showRequiredTexturesMenuItem.Click += ShowRequiredTextures_Click;
			_fileListContextMenu.Items.Add(showRequiredTexturesMenuItem);

			// Add quick Edit and Discard actions to the file list context menu
			var editMenuItem = new ToolStripMenuItem("Edit");
			editMenuItem.Name = "edit";
			editMenuItem.Click += (s, ev) => { LstFiles_DoubleClick(LstFiles, EventArgs.Empty); };
			_fileListContextMenu.Items.Add(editMenuItem);

			var discardMenuItem = new ToolStripMenuItem("Discard Changes");
			discardMenuItem.Name = "discard";
			discardMenuItem.Click += (s, ev) => { DiscardChangesForSelectedFile(); };
			_fileListContextMenu.Items.Add(discardMenuItem);
			
			_fileListContextMenu.Items.Add(new ToolStripSeparator());
			
			var extractMenuItem = new ToolStripMenuItem("Extract Selected File(s)...");
			extractMenuItem.Name = "extract";
			extractMenuItem.Click += ExtractSelectedFiles_Click;
			_fileListContextMenu.Items.Add(extractMenuItem);
			
			var selectAllMenuItem = new ToolStripMenuItem("Select All Files in This Folder");
			selectAllMenuItem.Name = "selectAll";
			selectAllMenuItem.Click += SelectAllInFolder_Click;
			_fileListContextMenu.Items.Add(selectAllMenuItem);
			
			_fileListContextMenu.Items.Add(new ToolStripSeparator());
			
			var removeImportMenuItem = new ToolStripMenuItem("Remove Imported File");
			removeImportMenuItem.Name = "removeImport";
			removeImportMenuItem.Click += RemoveImportedFile_Click;
			_fileListContextMenu.Items.Add(removeImportMenuItem);
			
			_fileListContextMenu.Items.Add(new ToolStripSeparator());
			
			var deleteFileMenuItem = new ToolStripMenuItem("Delete File from IPF");
			deleteFileMenuItem.Name = "deleteFile";
			deleteFileMenuItem.Click += DeleteFileFromIpf_Click;
			_fileListContextMenu.Items.Add(deleteFileMenuItem);
			
			LstFiles.ContextMenuStrip = _fileListContextMenu;
			_fileListContextMenu.Opening += FileListContextMenu_Opening;
			
			// Create context menu for folder tree
			var folderContextMenu = new ContextMenuStrip();
			var extractFolderMenuItem = new ToolStripMenuItem("Extract This Folder...");
			extractFolderMenuItem.Name = "extractFolder";
			extractFolderMenuItem.Click += ExtractFolder_Click;
			folderContextMenu.Items.Add(extractFolderMenuItem);
			var extractSelectedFoldersMenuItem = new ToolStripMenuItem("Extract Selected Folders...");
			extractSelectedFoldersMenuItem.Name = "extractSelectedFolders";
			extractSelectedFoldersMenuItem.Click += ExtractSelectedFolders_Click;
			folderContextMenu.Items.Add(extractSelectedFoldersMenuItem);
			folderContextMenu.Opening += FolderContextMenu_Opening;
			TreeFolders.ContextMenuStrip = folderContextMenu;
			
			// Add mouse event handlers for tree multi-select
			TreeFolders.MouseDown += TreeFolders_MouseDown;
			TreeFolders.DrawMode = TreeViewDrawMode.OwnerDrawText;
			TreeFolders.DrawNode += TreeFolders_DrawNode;
			
			// Handle right-click to prevent preview change
			LstFiles.MouseDown += LstFiles_MouseDown;
			LstFiles.MouseUp += LstFiles_MouseUp;

			// Disable extract buttons by default
			BtnExtractPack.Enabled = false;
			BtnExtractFile.Enabled = false;

			// Hide empty lists
			SplMain.Visible = false;

			// Fix white border of tool strip
			toolStrip1.Renderer = new MySR();

			// Load settings
			BtnPreview.Checked = Properties.Settings.Default.Preview;
			SplFiles.Panel2Collapsed = !Properties.Settings.Default.Preview;

			// Reset/initialize preview elements
			ResetPreview();

			// Load files passed as arguments
			if (args.Length != 0)
			{
				var filePath = args[0];
				if (File.Exists(filePath))
					Open(filePath);
				else
					MessageBox.Show("File not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Called when dragging something on top of the form,
		/// checks if it's a dropable IPF file.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void FrmMain_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				var files = (string[])e.Data.GetData(DataFormats.FileDrop);
				var file = files[0];
				if (Path.GetExtension(file) == ".ipf")
					e.Effect = DragDropEffects.Copy;
				else if (Path.GetExtension(file) == ".ies")
					e.Effect = DragDropEffects.Copy;
			}
		}

		/// <summary>
		/// Called when file is dropped on the form, opens it.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void FrmMain_DragDrop(object sender, DragEventArgs e)
		{
			var files = (string[])e.Data.GetData(DataFormats.FileDrop);
			var file = files[0];
			Open(file);
		}

		/// <summary>
		/// Called when clicking Open, shows open dialog and opens the
		/// selected file.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtnOpen_Click(object sender, EventArgs e)
		{
			if (OfdIpfFile.ShowDialog() != DialogResult.OK)
				return;

			var fileNames = OfdIpfFile.FileNames;
			if (fileNames.Length == 0)
				return;
			
			// First file becomes the main IPF
			Open(fileNames[0]);
			
			// Additional files are loaded as additional IPFs
			if (fileNames.Length > 1)
			{
				int loadedCount = 0;
				for (int i = 1; i < fileNames.Length; i++)
				{
					try
					{
						var ipf = new Ipf(fileNames[i]);
						ipf.Load();
						_additionalIpfs.Add(ipf);
						
						// Build file dictionary with unique prefix
						var ipfId = $"IPF{_additionalIpfs.Count}";
						foreach (var ipfFile in ipf.Files)
						{
							_additionalFiles[$"{ipfId}:{ipfFile.FullPath}"] = ipfFile;
						}
						
						// Add IPF to tree view as a new root node
						var ipfName = Path.GetFileName(fileNames[i]);
						var rootNode = new TreeNode(ipfName)
						{
							ImageIndex = 3, // compress.png icon
							SelectedImageIndex = 3,
							Tag = $"{ipfId}:ROOT"
						};
						
						// Build folder structure for this IPF
						var folderNodes = new Dictionary<string, TreeNode>();
						foreach (var ipfFile in ipf.Files)
						{
							var parts = ipfFile.FullPath.Split('/');
							var currentPath = "";
							TreeNode parentNode = rootNode;
							
							for (int j = 0; j < parts.Length - 1; j++) // Skip the file name
							{
								currentPath += (j > 0 ? "/" : "") + parts[j];
								var folderKey = $"{ipfId}:{currentPath}";
								
								if (!folderNodes.ContainsKey(folderKey))
								{
									var folderNode = new TreeNode(parts[j])
									{
										ImageIndex = 2, // folder icon
										SelectedImageIndex = 2,
										Tag = folderKey
									};
									parentNode.Nodes.Add(folderNode);
									folderNodes[folderKey] = folderNode;
								}
								parentNode = folderNodes[folderKey];
							}
						}
						
						TreeFolders.Nodes.Add(rootNode);
						loadedCount++;
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"Error loading additional IPF {fileNames[i]}: {ex.Message}");
					}
				}
				
				// Silently loaded additional IPFs - no notification needed
			}
		}

		/// <summary>
		/// Opens given IPF file.
		/// </summary>
		/// <param name="filePath"></param>
		private void Open(string filePath)
		{
			// Check for unsaved changes before opening new file
			if (_pendingChanges.Count > 0 || _isEditingIes)
			{
				// Save current editing state
				if (_isEditingIes && !string.IsNullOrEmpty(_currentIesFilePath))
				{
					_pendingChanges[_currentIesFilePath] = TxtPreview.Text;
				}

				if (_pendingChanges.Count > 0)
				{
					var result = MessageBox.Show(
						$"You have unsaved changes to {_pendingChanges.Count} file(s).\n\nDo you want to save before opening a new file?",
						Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

					if (result == DialogResult.Yes)
					{
						MnuSave_Click(null, null);
						return;
					}
					else if (result == DialogResult.Cancel)
					{
						return;
					}
					// DialogResult.No - discard changes and continue
					_pendingChanges.Clear();
					_isEditingIes = false;
					_currentIesFilePath = null;
				}
			}

			if (Path.GetExtension(filePath) == ".ies")
			{
				this.ResetPreview();

				var iesData = File.ReadAllBytes(filePath);
				var iesFile = new IesFile(iesData);

				Invoke((MethodInvoker)delegate
				{
					GridPreview.SuspendDrawing();

					foreach (var iesColumn in iesFile.Columns)
						GridPreview.Columns.Add(iesColumn.Name, iesColumn.Name);

					foreach (var iesRow in iesFile.Rows)
					{
						var row = new DataGridViewRow();
						row.CreateCells(GridPreview);

						var i = 0;
						foreach (var iesColumn in iesFile.Columns)
							row.Cells[i++].Value = iesRow[iesColumn.Name];

						GridPreview.Rows.Add(row);
					}

					GridPreview.ResumeDrawing();

					GridPreview.Visible = true;
				});

				SplMain.Visible = true;

				return;
			}

			// Reset everything
			TreeFolders.Nodes.Clear();
			LstFiles.Items.Clear();
			ResetPreview();

			_folders.Clear();
			_files.Clear();

			LblVersion.Text = "";
			LblFileName.Text = "";

			// Open IPF
			try
			{
				_openedIpf = new Ipf(filePath);
				_openedIpf.Load();
			}
			catch (IOException)
			{
				_openedIpf = null;
				MessageBox.Show("Failed to open file, it's already in use.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// Create file list
			var paths = new List<string>();
			foreach (var ipfFile in _openedIpf.Files)
			{
				paths.Add(ipfFile.FullPath);
				_files.Add(ipfFile.FullPath, ipfFile);
			}

			// Create fil tree
			PopulateTreeView(TreeFolders, paths, '/');

			// Status info
			LblVersion.Text = "Version " + _openedIpf.Footer.NewVersion;
			LblFileName.Text = filePath;

			// Open first node if there only is one
			if (TreeFolders.Nodes.Count == 1)
			{
				TreeFolders.SelectedNode = TreeFolders.Nodes[0];
				TreeFolders.SelectedNode.Toggle();
			}

			// Show lists and enabled pack extract button
			BtnExtractPack.Enabled = true;
			SplMain.Visible = true;
		}

		/// <summary>
		/// Creates nodes in tree view, based on given paths.
		/// </summary>
		/// <param name="treeView"></param>
		/// <param name="paths"></param>
		/// <param name="pathSeparator"></param>
		private void PopulateTreeView(TreeView treeView, IEnumerable<string> paths, char pathSeparator)
		{
			var insertedPaths = new Dictionary<string, TreeNode>();

			treeView.BeginUpdate();
			treeView.Nodes.Clear();
			foreach (string path in paths)
			{
				var subPaths = path.Split(pathSeparator);
				var subPathAgg = "";

				for (int i = 0; i < subPaths.Length; ++i)
				{
					var subPath = subPaths[i];
					var parentPath = subPathAgg;

					subPathAgg += subPath + pathSeparator;

					if (i == subPaths.Length - 1)
					{
						if (!_folders.ContainsKey(parentPath))
							_folders.Add(parentPath, new List<string>());
						_folders[parentPath].Add(subPathAgg.Trim(pathSeparator));
						break;
					}

					if (!insertedPaths.ContainsKey(subPathAgg))
					{
						TreeNode node;
						if (!insertedPaths.TryGetValue(parentPath, out node))
						{
							node = treeView.Nodes.Add(subPathAgg, subPath);
							insertedPaths.Add(subPathAgg, node);
						}
						else
						{
							node = node.Nodes.Add(subPathAgg, subPath);
							insertedPaths.Add(subPathAgg, node);
						}
					}
				}
			}
			treeView.EndUpdate();
		}

		/// <summary>
		/// Called when (de)selecting in files list, shows preview.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void LstFiles_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Don't change preview on right-click (for context menu)
			if (_isRightClick)
				return;
			
			// Don't change preview when Ctrl is held (multi-select mode)
			if (Control.ModifierKeys.HasFlag(Keys.Control))
				return;
				
			// If currently editing, save the current XML to pending changes before switching
			if (_isEditingIes && !string.IsNullOrEmpty(_currentIesFilePath))
			{
				// Store current edits
				_pendingChanges[_currentIesFilePath] = TxtPreview.Text;
				_isEditingIes = false;
			}

			// Only reset preview if single selection (multi-select should not change preview)
			if (LstFiles.SelectedIndices.Count <= 1)
			{
				ResetPreview();
			}

			if (LstFiles.SelectedIndices.Count == 0)
			{
				BtnExtractFile.Enabled = false;
				BtnEditIes.Enabled = false;
				UpdatePendingChangesUI();
				return;
			}

			BtnExtractFile.Enabled = true;

			// Enable Edit IES button if selected file is an IES (single selection only)
			if (LstFiles.SelectedIndices.Count == 1)
			{
				var selected = LstFiles.SelectedItems[0];
				var fileName = (string)selected.Tag;
				var ext = Path.GetExtension(fileName).ToLowerInvariant();
				BtnEditIes.Enabled = (ext == ".ies");

				// Check if this file has pending changes - if so, just show the pending edits
				if (_pendingChanges.ContainsKey(fileName))
				{
					// Load the original XML so revert detection works correctly
					var originalXml = LoadOriginalIesXml(fileName) ?? _pendingChanges[fileName];
					
					_currentIesFilePath = fileName;
					_currentIesXml = originalXml;  // Store original so equality check detects changes
					_isEditingIes = true;
					
					ResetPreview();
					SetTextPreviewStyle(ScintillaNET.Lexer.Xml);
					TxtPreview.ReadOnly = false;
					TxtPreview.Text = _pendingChanges[fileName];  // Show pending edits in editor
					TxtPreview.Visible = true;
					
					TxtPreview.TextChanged -= TxtPreview_TextChanged;
					TxtPreview.TextChanged += TxtPreview_TextChanged;
					
					BtnEditIes.Enabled = false;
					UpdatePendingChangesUI();
					LblFileName.Text = fileName + " (Modified)";
					TxtPreview.Focus();
					return;
				}

				UpdatePendingChangesUI();

				if (BtnPreview.Checked)
					Preview();
			}
			else
			{
				// Multi-select: disable IES editing, don't preview
				BtnEditIes.Enabled = false;
				UpdatePendingChangesUI();
			}
		}

		/// <summary>
		/// Track right-click to prevent preview change
		/// </summary>
		private void LstFiles_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				_isRightClick = true;
				
				// Select the item under the cursor for context menu
				var hitTest = LstFiles.HitTest(e.Location);
				if (hitTest.Item != null)
				{
					hitTest.Item.Selected = true;
				}
			}
			else
			{
				_isRightClick = false;
			}
		}
		
		/// <summary>
		/// Reset right-click flag after mouse up
		/// </summary>
		private void LstFiles_MouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right)
			{
				// Show our context menu for the selected item
				if (LstFiles.SelectedItems.Count > 0 && _fileListContextMenu != null)
				{
					var filePath = (string)LstFiles.SelectedItems[0].Tag;
					_fileListContextMenu.Items[0].Enabled = true; // Edit
					_fileListContextMenu.Items[1].Enabled = _pendingChanges.ContainsKey(filePath) || _importedFiles.ContainsKey(filePath);
					_fileListContextMenu.Show(LstFiles, e.Location);
				}
				_isRightClick = false;
			}
			else
			{
				_isRightClick = false;
			}
		}

		/// <summary>
		/// Called when selected a node in the tree view,
		/// lists files in node's folder in file list.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TreeFolders_AfterSelect(object sender, TreeViewEventArgs e)
		{
			// Save current editing state before switching folders
			if (_isEditingIes && !string.IsNullOrEmpty(_currentIesFilePath))
			{
				_pendingChanges[_currentIesFilePath] = TxtPreview.Text;
				_isEditingIes = false;
			}

			var nodePath = e.Node.FullPath.Replace('\\', '/');
			var path = nodePath + '/';

			LstFiles.BeginUpdate();
			LstFiles.Items.Clear();

			// Check if this is an additional IPF node
			var nodeTag = e.Node.Tag?.ToString() ?? "";
			bool isAdditionalIpf = nodeTag.StartsWith("IPF") && nodeTag.Contains(":");
			
			if (isAdditionalIpf)
			{
				// Extract IPF ID and folder path
				var colonIndex = nodeTag.IndexOf(':');
				var ipfId = nodeTag.Substring(0, colonIndex);
				var folderPath = nodeTag.Substring(colonIndex + 1);
				
				// Find the corresponding IPF
				int ipfIndex = int.Parse(ipfId.Substring(3)) - 1; // IPF1 -> index 0
				if (ipfIndex >= 0 && ipfIndex < _additionalIpfs.Count)
				{
					var ipf = _additionalIpfs[ipfIndex];
					
					// List files from this IPF that match this folder
					foreach (var ipfFile in ipf.Files)
					{
						var filePath = ipfFile.FullPath;
						var fileFolder = Path.GetDirectoryName(filePath)?.Replace('\\', '/') ?? "";
						
						// Check if file is directly in this folder
						bool isInFolder = false;
						if (folderPath == "ROOT")
						{
							// Root level - show files with no folder
							isInFolder = !filePath.Contains("/");
						}
						else
						{
							isInFolder = fileFolder.Equals(folderPath, StringComparison.OrdinalIgnoreCase);
						}
						
						if (isInFolder)
						{
							var fileName = Path.GetFileName(filePath);
							var ext = Path.GetExtension(fileName).ToLowerInvariant();
							
							var lvi = LstFiles.Items.Add(fileName);
							lvi.Tag = $"{ipfId}:{ipfFile.FullPath}";
							
							FileFormat fileType;
							if (_fileTypes.TryGetValue(ext, out fileType))
								lvi.ImageKey = fileType.Icon;
							else
								lvi.ImageKey = "page_white.png";
						}
					}
				}
			}
			else
			{
				// Normal IPF file listing
				List<string> paths;
				if (_folders.TryGetValue(path, out paths))
				{
					foreach (var filePath in paths)
					{
						var fileName = Path.GetFileName(filePath);
						var ext = Path.GetExtension(fileName).ToLowerInvariant();

						// Show indicator for files with pending changes/status
						var displayName = fileName;
						if (_deletedFiles.Contains(filePath))
							displayName = "✕ " + fileName;
						else if (_pendingChanges.ContainsKey(filePath))
							displayName = "* " + fileName;
						else if (_importedFiles.ContainsKey(filePath))
							displayName = "+ " + fileName;

						var lvi = LstFiles.Items.Add(displayName);
						lvi.Tag = filePath;

						FileFormat fileType;
						if (_fileTypes.TryGetValue(ext, out fileType))
							lvi.ImageKey = fileType.Icon;
						else
							lvi.ImageKey = "page_white.png";

						// Highlight modified/imported/deleted files
						if (_deletedFiles.Contains(filePath))
							lvi.ForeColor = Color.Red;
						else if (_pendingChanges.ContainsKey(filePath))
							lvi.ForeColor = Color.DarkOrange;
						else if (_importedFiles.ContainsKey(filePath))
							lvi.ForeColor = Color.Green;
					}
				}

				// Also show imported files for this folder
				foreach (var kvp in _importedFiles)
				{
					var importedPath = kvp.Key;
					var importedFolder = Path.GetDirectoryName(importedPath)?.Replace('\\', '/') + "/";
					
					// Check if this imported file is in the current folder and not already in paths
					if (importedFolder == path && (paths == null || !paths.Contains(importedPath)))
					{
						var fileName = Path.GetFileName(importedPath);
						var ext = Path.GetExtension(fileName).ToLowerInvariant();

						var displayName = "+ " + fileName;
						var lvi = LstFiles.Items.Add(displayName);
						lvi.Tag = importedPath;

						FileFormat fileType;
						if (_fileTypes.TryGetValue(ext, out fileType))
							lvi.ImageKey = fileType.Icon;
						else
							lvi.ImageKey = "page_white.png";

						lvi.ForeColor = Color.Green;
					}
				}
			}

			LstFiles.EndUpdate();
			UpdatePendingChangesUI();
		}

		/// <summary>
		/// Shows preview for selected file.
		/// </summary>
		private void Preview()
		{
			if (LstFiles.SelectedIndices.Count == 0)
				return;

			var selected = LstFiles.SelectedItems[0];
			var fileTag = (string)selected.Tag;
			
			// Check if this is an imported file (temporary, not yet saved)
			byte[] importedData = null;
			bool isImported = _importedFiles.TryGetValue(fileTag, out importedData);
			
			// Check if this is an additional IPF file
			IpfFile ipfFile = null;
			string fileName;
			
			if (fileTag.StartsWith("IPF") && fileTag.Contains(":"))
			{
				// From additional IPF
				if (!_additionalFiles.TryGetValue(fileTag, out ipfFile))
					return;
				fileName = fileTag.Substring(fileTag.IndexOf(':') + 1);
			}
			else if (isImported)
			{
				// Imported file - use the data directly
				fileName = fileTag;
			}
			else
			{
				if (!_files.TryGetValue(fileTag, out ipfFile))
					return;
				fileName = fileTag;
			}
			
			var ext = Path.GetExtension(fileName).ToLowerInvariant();

			var previewType = PreviewType.None;
			var lexer = Lexer.Null;

			FileFormat fileType;
			if (_fileTypes.TryGetValue(ext, out fileType))
			{
				previewType = fileType.PreviewType;
				lexer = fileType.Lexer;
			}

			ThreadPool.QueueUserWorkItem(state =>
			{
				try
				{
					// Helper to get file data - either from imported files or from IPF
					Func<byte[]> getData = () => isImported ? importedData : ipfFile.GetData();

					switch (previewType)
					{
						case PreviewType.Text:
							// Prefer in-memory pending changes for this file so we don't overwrite
							// user edits when re-selecting the file.
							string text;
							if (_pendingChanges.TryGetValue(fileTag, out var pendingText))
							{
								text = pendingText;
							}
							else
							{
								var txtData = getData();
								try { text = Encoding.UTF8.GetString(txtData); }
								catch { text = Encoding.Default.GetString(txtData); }
							}

							SetTextPreviewStyle(lexer);

							Invoke((MethodInvoker)delegate
							{
								TxtPreview.ReadOnly = false;
								TxtPreview.Text = text;
								TxtPreview.ReadOnly = true;
								TxtPreview.Visible = true;
							});
							break;

						case PreviewType.Image:
							var imgData = getData();

							Invoke((MethodInvoker)delegate
							{
								using (var ms = new MemoryStream(imgData))
									ImgPreview.Image = Image.FromStream(ms);
								ImgPreview.Size = ImgPreview.Image.Size;
								PnlImagePreview.Visible = true;
							});
							break;

						case PreviewType.DdsImage:
							var ddsData = getData();

							DDSImage ddsImage = null;
							try
							{
								ddsImage = new DDSImage(ddsData);
							}
							catch (Exception ddsEx)
							{
								Invoke((MethodInvoker)delegate
								{
									LblPreview.Text = $"Preview failed: {ddsEx.Message}";
								});
								break;
							}

							Invoke((MethodInvoker)delegate
							{
								if (ddsImage != null && ddsImage.BitmapImage != null)
								{
									ImgPreview.Image = ddsImage.BitmapImage;
									ImgPreview.Size = ImgPreview.Image.Size;
									PnlImagePreview.Visible = true;
								}
								else
								{
									LblPreview.Text = "Preview failed: Unable to decode DDS image";
								}
							});
							break;

						case PreviewType.TgaImage:
							var tgaData = getData();

							TargaImage tgaImage = null;
							try
							{
								using (var ms = new MemoryStream(tgaData))
									tgaImage = new TargaImage(ms);
							}
							catch (Exception)
							{
								Invoke((MethodInvoker)delegate
								{
									LblPreview.Text = "Preview failed";
								});
								break;
							}

							Invoke((MethodInvoker)delegate
							{
								ImgPreview.Image = tgaImage.Image;
								ImgPreview.Size = ImgPreview.Image.Size;
								PnlImagePreview.Visible = true;
							});
							break;

						case PreviewType.IesTable:
							var iesData = getData();
							var iesFile = new IesFile(iesData);

							Invoke((MethodInvoker)delegate
							{
								GridPreview.SuspendDrawing();

								foreach (var iesColumn in iesFile.Columns)
									GridPreview.Columns.Add(iesColumn.Name, iesColumn.Name);

								foreach (var iesRow in iesFile.Rows)
								{
									var row = new DataGridViewRow();
									row.CreateCells(GridPreview);

									var i = 0;
									foreach (var iesColumn in iesFile.Columns)
										row.Cells[i++].Value = iesRow[iesColumn.Name];

									GridPreview.Rows.Add(row);
								}

								GridPreview.ResumeDrawing();

								GridPreview.Visible = true;
							});
							break;

						case PreviewType.TtfFont:
							var pfc = new PrivateFontCollection();

							try
							{
								var ttfData = getData();
								using (var ms = new MemoryStream(ttfData))
								{
									var fontdata = new byte[ms.Length];
									ms.Read(fontdata, 0, (int)ms.Length);

									unsafe
									{
										fixed (byte* pFontData = fontdata)
											pfc.AddMemoryFont((IntPtr)pFontData, fontdata.Length);
									}
								}
							}
							catch (Exception)
							{
								Invoke((MethodInvoker)delegate
								{
									LblPreview.Text = "Preview failed";
								});
								break;
							}

							var fontFamily = pfc.Families.First();
							var font = new Font(fontFamily, 18, FontStyle.Regular, GraphicsUnit.Pixel);
							var arialFont = new Font("Arial", 12);

							var fontInfo = "Name: " + fontFamily.Name;
							var example1 = "abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ\n1234567890.:,;'\" (!?) +-*/=";
							var example2 = "Lorem ipsum dolor sit amet.";

							var bmp = new Bitmap(600, 500);
							using (var graphics = Graphics.FromImage(bmp))
							{
								var infoHeight = graphics.MeasureString(fontInfo, arialFont).Height;
								var example1Height = graphics.MeasureString(example1, font).Height;

								graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
								graphics.FillRectangle(Brushes.White, new Rectangle(0, 0, bmp.Width, bmp.Height));

								graphics.DrawString(fontInfo, arialFont, Brushes.Black, new Point(0, 0));
								graphics.DrawString(example1, font, Brushes.Black, new PointF(0, infoHeight + 10));

								var point = new PointF(10, infoHeight + 10 + example1Height + 20);
								foreach (var size in new int[] { 12, 18, 24, 36, 48, 60, 72 })
								{
									font = new Font(fontFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);
									graphics.DrawString(example2, font, Brushes.Black, point);
									point.Y += font.Height + 5;
								}
							}

							Invoke((MethodInvoker)delegate
							{
								ImgPreview.Image = bmp;
								ImgPreview.Size = ImgPreview.Image.Size;
								PnlImagePreview.Visible = true;
							});
							break;

						case PreviewType.Model3D:
							var xacData = getData();
							try
							{
								var xacFile = XacFile.Load(xacData);
								
								// Build info text
								var info = new StringBuilder();
								info.AppendLine($"=== XAC Model: {fileName} ===");
								info.AppendLine($"Version: {xacFile.Header.MajorVersion}.{xacFile.Header.MinorVersion}");
								info.AppendLine($"Big Endian: {xacFile.Header.IsBigEndian}");
								info.AppendLine();
								
								if (xacFile.Metadata != null)
								{
									info.AppendLine("--- Metadata ---");
									info.AppendLine($"Actor Name: {xacFile.Metadata.ActorName}");
									info.AppendLine($"Source App: {xacFile.Metadata.SourceApp}");
									info.AppendLine($"Original File: {xacFile.Metadata.OriginalFileName}");
									info.AppendLine($"Export Date: {xacFile.Metadata.ExportDate}");
									info.AppendLine();
								}
								
								info.AppendLine($"--- Statistics ---");
								info.AppendLine($"Nodes (Bones): {xacFile.Nodes.Count}");
								info.AppendLine($"Materials: {xacFile.Materials.Count}");
								info.AppendLine($"Meshes: {xacFile.Meshes.Count}");
								info.AppendLine($"Chunks Parsed: {xacFile.RawChunks.Count}");
								info.AppendLine();
								
								// Show raw chunk info for debugging
								info.AppendLine("--- Raw Chunks ---");
								foreach (var chunk in xacFile.RawChunks)
								{
									var errInfo = string.IsNullOrEmpty(chunk.ParseError) ? "" : $" [ERROR: {chunk.ParseError}]";
									info.AppendLine($"  Type: {(int)chunk.Type} ({chunk.Type}), Version: {chunk.Version}, Size: {chunk.Length}{errInfo}");
								}
								info.AppendLine();
								
								if (xacFile.Nodes.Count > 0)
								{
									info.AppendLine("--- Bone Hierarchy ---");
									foreach (var node in xacFile.Nodes)
									{
										string indent = node.ParentNodeId < 0 ? "" : "  ";
										info.AppendLine($"{indent}{node.Name} (Parent: {node.ParentNodeId}, Children: {node.NumChildren})");
										info.AppendLine($"{indent}  Position: {node.Position}");
									}
									info.AppendLine();
								}
								
								if (xacFile.Materials.Count > 0)
								{
									info.AppendLine("--- Materials ---");
									foreach (var mat in xacFile.Materials)
									{
										info.AppendLine($"  {mat.Name}");
										info.AppendLine($"    Diffuse: {mat.DiffuseColor}, Opacity: {mat.Opacity}");
										foreach (var layer in mat.Layers)
											info.AppendLine($"    Texture: {layer.Texture}");
									}
									info.AppendLine();
								}
								
								if (xacFile.Meshes.Count > 0)
								{
									info.AppendLine("--- Meshes ---");
									foreach (var mesh in xacFile.Meshes)
									{
										info.AppendLine($"  Mesh (Node: {mesh.NodeIndex})");
										info.AppendLine($"    Vertices: {mesh.NumVertices}, Indices: {mesh.NumIndices}");
										info.AppendLine($"    SubMeshes: {mesh.NumSubMeshes}, Collision: {mesh.IsCollisionMesh}");
									}
									info.AppendLine();
								}
								
								if (xacFile.Properties != null)
								{
									info.AppendLine("--- Properties ---");
									info.AppendLine($"  Name: {xacFile.Properties.Name}");
									info.AppendLine($"  Shader: {xacFile.Properties.ShaderName}");
									info.AppendLine($"  FX File: {xacFile.Properties.FxFileName}");
									foreach (var prop in xacFile.Properties.StringProperties)
										info.AppendLine($"  {prop.Key} = {prop.Value}");
								}
								
								// Show any parse errors from chunks
								var errors = xacFile.RawChunks.Where(c => !string.IsNullOrEmpty(c.ParseError)).ToList();
								if (errors.Count > 0)
								{
									info.AppendLine();
									info.AppendLine("--- Parse Warnings ---");
									foreach (var chunk in errors)
										info.AppendLine($"  Chunk {chunk.Type}: {chunk.ParseError}");
								}

								Invoke((MethodInvoker)delegate
								{
									// Check if model has any mesh data
									bool hasMeshData = xacFile.Meshes.Count > 0;
									
									// Try to use 3D viewer if available and has mesh data
									if (_modelViewer != null && hasMeshData)
									{
										try
										{
											// Make visible first to trigger OpenGL initialization
											_modelViewer.Visible = true;
											_modelViewer.BringToFront();
											
											// Give OpenGL time to initialize (Load event fires on first paint)
											for (int i = 0; i < 10 && !_modelViewer.IsOpenGLReady; i++)
											{
												Application.DoEvents();
												System.Threading.Thread.Sleep(50);
											}
											
											if (_modelViewer.IsOpenGLReady)
											{
												_modelViewer.LoadXacModel(xacFile);
											}
											else
											{
												// OpenGL failed to init
												_modelViewer.Visible = false;
												ShowXacTextInfo(info.ToString(), "OpenGL failed to initialize (timeout)");
											}
										}
										catch (Exception glEx)
										{
											// Fall back to text view on OpenGL error
											_modelViewer.Visible = false;
											ShowXacTextInfo(info.ToString(), $"OpenGL Error: {glEx.Message}");
										}
									}
									else
									{
										// No mesh data or no viewer - show text info
										string reason = !hasMeshData ? "No mesh data in file (skeleton only)" : 
											(_modelViewer == null ? "Model viewer not initialized" : "OpenGL not available");
										ShowXacTextInfo(info.ToString(), reason);
									}
								});
							}
							catch (Exception ex)
							{
								Invoke((MethodInvoker)delegate
								{
									// Show hex view on parse failure
									var hexInfo = new StringBuilder();
									hexInfo.AppendLine($"XAC Parse Error: {ex.Message}");
									hexInfo.AppendLine($"File: {fileName}");
									hexInfo.AppendLine($"Size: {xacData.Length:N0} bytes");
									hexInfo.AppendLine();
									hexInfo.AppendLine("First 512 bytes:");
									hexInfo.AppendLine();
									
									var xacShowBytes = Math.Min(xacData.Length, 512);
									for (int i = 0; i < xacShowBytes; i += 16)
									{
										hexInfo.Append($"{i:X8}  ");
										var hexPart = new StringBuilder();
										var asciiPart = new StringBuilder();

										for (int j = 0; j < 16; j++)
										{
											if (i + j < xacShowBytes)
											{
												var b = xacData[i + j];
												hexPart.Append($"{b:X2} ");
												asciiPart.Append(b >= 32 && b < 127 ? (char)b : '.');
											}
											else
											{
												hexPart.Append("   ");
											}
											if (j == 7) hexPart.Append(" ");
										}
										hexInfo.AppendLine($"{hexPart} |{asciiPart}|");
									}
									
									TxtPreview.ReadOnly = false;
									TxtPreview.Text = hexInfo.ToString();
									TxtPreview.ReadOnly = true;
									TxtPreview.Visible = true;
								});
							}
							break;

						case PreviewType.Animation3D:
							var xsmData = ipfFile.GetData();
							try
							{
								var xsmFile = XsmFile.Load(xsmData);
								var animInfo = new StringBuilder();
								animInfo.AppendLine($"XSM Animation: {fileName}");
								animInfo.AppendLine($"Version: {xsmFile.Header.MajorVersion}.{xsmFile.Header.MinorVersion}");
								if (xsmFile.Metadata != null)
								{
									animInfo.AppendLine($"Motion Name: {xsmFile.Metadata.MotionName}");
									animInfo.AppendLine($"FPS: {xsmFile.Metadata.FPS}");
									animInfo.AppendLine($"Source: {xsmFile.Metadata.SourceApp}");
								}
								animInfo.AppendLine($"Motion Parts: {xsmFile.MotionParts.Count}");
								animInfo.AppendLine();
								foreach (var part in xsmFile.MotionParts)
								{
									animInfo.AppendLine($"  Part: {part.Name}");
									animInfo.AppendLine($"    Position Keys: {part.NumPositionKeys}");
									animInfo.AppendLine($"    Rotation Keys: {part.NumRotationKeys}");
									animInfo.AppendLine($"    Scale Keys: {part.NumScaleKeys}");
								}

								Invoke((MethodInvoker)delegate
								{
									TxtPreview.ReadOnly = false;
									TxtPreview.Text = animInfo.ToString();
									TxtPreview.ReadOnly = true;
									TxtPreview.Visible = true;
								});
							}
							catch (Exception ex)
							{
								Invoke((MethodInvoker)delegate
								{
									LblPreview.Text = $"XSM Parse Error: {ex.Message}";
								});
							}
							break;

						case PreviewType.HexView:
							var hexData = ipfFile.GetData();
							var hexView = new StringBuilder();
							hexView.AppendLine($"File: {fileName}");
							hexView.AppendLine($"Size: {hexData.Length:N0} bytes");
							hexView.AppendLine();

							// Show first 2KB in hex view
							var showBytes = Math.Min(hexData.Length, 2048);
							for (int i = 0; i < showBytes; i += 16)
							{
								hexView.Append($"{i:X8}  ");
								var hexPart = new StringBuilder();
								var asciiPart = new StringBuilder();

								for (int j = 0; j < 16; j++)
								{
									if (i + j < showBytes)
									{
										var b = hexData[i + j];
										hexPart.Append($"{b:X2} ");
										asciiPart.Append(b >= 32 && b < 127 ? (char)b : '.');
									}
									else
									{
										hexPart.Append("   ");
									}
									if (j == 7) hexPart.Append(" ");
								}
								hexView.AppendLine($"{hexPart} |{asciiPart}|");
							}

							if (hexData.Length > 2048)
								hexView.AppendLine($"\n... ({hexData.Length - 2048:N0} more bytes)");

							Invoke((MethodInvoker)delegate
							{
								TxtPreview.ReadOnly = false;
								TxtPreview.Text = hexView.ToString();
								TxtPreview.ReadOnly = true;
								TxtPreview.Visible = true;
							});
							break;

						default:
							Invoke((MethodInvoker)delegate
							{
								LblPreview.Text = "No Preview";
							});
							break;
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK);
				}
			});
		}

		/// <summary>
		/// Called when exit option is clicked, closes program.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtnExit_Click(object sender, EventArgs e)
		{
			Close();
		}

		/// <summary>
		/// Sets lexer and styles for text preview.
		/// </summary>
		/// <param name="lexer"></param>
		private void SetTextPreviewStyle(Lexer lexer)
		{
			Invoke((MethodInvoker)delegate
			{
				TxtPreview.StyleResetDefault();
				TxtPreview.Styles[Style.Default].Font = "Courier New";
				TxtPreview.Styles[Style.Default].Size = 10;
				TxtPreview.StyleClearAll();

				TxtPreview.Lexer = lexer;

				switch (lexer)
				{
					case Lexer.Xml:
						TxtPreview.Styles[Style.Xml.XmlStart].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Xml.XmlEnd].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Xml.TagEnd].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Xml.Tag].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Xml.TagEnd].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Xml.Attribute].ForeColor = Color.Red;
						TxtPreview.Styles[Style.Xml.DoubleString].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Xml.SingleString].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Xml.Comment].ForeColor = Color.Green;
						break;

					case Lexer.Lua:
						TxtPreview.SetKeywords(0, "and break do else elseif end false for function goto if in local nil not or repeat return then true until while");
						TxtPreview.SetKeywords(1, "_ENV _G _VERSION assert collectgarbage dofile error getfenv getmetatable ipairs load loadfile loadstring module next pairs pcall print rawequal rawget rawlen rawset require select setfenv setmetatable tonumber tostring type unpack xpcall string table math bit32 coroutine io os debug package __index __newindex __call __add __sub __mul __div __mod __pow __unm __concat __len __eq __lt __le __gc __mode");
						TxtPreview.SetKeywords(2, "byte char dump find format gmatch gsub len lower match rep reverse sub upper abs acos asin atan atan2 ceil cos cosh deg exp floor fmod frexp ldexp log log10 max min modf pow rad random randomseed sin sinh sqrt tan tanh arshift band bnot bor btest bxor extract lrotate lshift replace rrotate rshift shift string.byte string.char string.dump string.find string.format string.gmatch string.gsub string.len string.lower string.match string.rep string.reverse string.sub string.upper table.concat table.insert table.maxn table.pack table.remove table.sort table.unpack math.abs math.acos math.asin math.atan math.atan2 math.ceil math.cos math.cosh math.deg math.exp math.floor math.fmod math.frexp math.huge math.ldexp math.log math.log10 math.max math.min math.modf math.pi math.pow math.rad math.random math.randomseed math.sin math.sinh math.sqrt math.tan math.tanh bit32.arshift bit32.band bit32.bnot bit32.bor bit32.btest bit32.bxor bit32.extract bit32.lrotate bit32.lshift bit32.replace bit32.rrotate bit32.rshift");
						TxtPreview.SetKeywords(3, "close flush lines read seek setvbuf write clock date difftime execute exit getenv remove rename setlocale time tmpname coroutine.create coroutine.resume coroutine.running coroutine.status coroutine.wrap coroutine.yield io.close io.flush io.input io.lines io.open io.output io.popen io.read io.tmpfile io.type io.write io.stderr io.stdin io.stdout os.clock os.date os.difftime os.execute os.exit os.getenv os.remove os.rename os.setlocale os.time os.tmpname debug.debug debug.getfenv debug.gethook debug.getinfo debug.getlocal debug.getmetatable debug.getregistry debug.getupvalue debug.getuservalue debug.setfenv debug.sethook debug.setlocal debug.setmetatable debug.setupvalue debug.setuservalue debug.traceback debug.upvalueid debug.upvaluejoin package.cpath package.loaded package.loaders package.loadlib package.path package.preload package.seeall");

						TxtPreview.Styles[Style.Lua.Default].ForeColor = Color.Black;
						TxtPreview.Styles[Style.Lua.Comment].ForeColor = Color.Green;
						TxtPreview.Styles[Style.Lua.CommentLine].ForeColor = Color.Green;
						TxtPreview.Styles[Style.Lua.CommentDoc].ForeColor = Color.DarkSeaGreen;
						TxtPreview.Styles[Style.Lua.LiteralString].ForeColor = Color.Purple;
						TxtPreview.Styles[Style.Lua.Preprocessor].ForeColor = Color.Brown;
						TxtPreview.Styles[Style.Lua.Number].ForeColor = Color.Orange;
						TxtPreview.Styles[Style.Lua.String].ForeColor = Color.Gray;
						TxtPreview.Styles[Style.Lua.StringEol].ForeColor = Color.Gray;
						TxtPreview.Styles[Style.Lua.Character].ForeColor = Color.Gray;
						TxtPreview.Styles[Style.Lua.Operator].ForeColor = Color.DarkBlue;
						TxtPreview.Styles[Style.Lua.Word].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Lua.Word2].ForeColor = Color.CornflowerBlue;
						TxtPreview.Styles[Style.Lua.Word3].ForeColor = Color.Purple;
						TxtPreview.Styles[Style.Lua.Word4].ForeColor = Color.DarkBlue;
						break;

					case Lexer.Cpp:
						TxtPreview.SetKeywords(0, "alignof and and_eq bitand bitor break case catch compl const_cast continue default delete do dynamic_cast else false for goto if namespace new not not_eq nullptr operator or or_eq reinterpret_cast return sizeof static_assert static_cast switch this throw true try typedef typeid using while xor xor_eq NULL");
						TxtPreview.SetKeywords(1, "alignas asm auto bool char char16_t char32_t class const constexpr decltype double enum explicit export extern final float friend inline int long mutable noexcept override private protected public register short signed static struct template thread_local typename union unsigned virtual void volatile wchar_t");

						TxtPreview.Styles[Style.Cpp.Default].ForeColor = Color.Silver;
						TxtPreview.Styles[Style.Cpp.Comment].ForeColor = Color.FromArgb(0, 128, 0); // Green
						TxtPreview.Styles[Style.Cpp.CommentLine].ForeColor = Color.FromArgb(0, 128, 0); // Green
						TxtPreview.Styles[Style.Cpp.CommentLineDoc].ForeColor = Color.FromArgb(128, 128, 128); // Gray
						TxtPreview.Styles[Style.Cpp.Number].ForeColor = Color.Olive;
						TxtPreview.Styles[Style.Cpp.Word].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Cpp.Word2].ForeColor = Color.Blue;
						TxtPreview.Styles[Style.Cpp.String].ForeColor = Color.FromArgb(163, 21, 21); // Red
						TxtPreview.Styles[Style.Cpp.Character].ForeColor = Color.FromArgb(163, 21, 21); // Red
						TxtPreview.Styles[Style.Cpp.Verbatim].ForeColor = Color.FromArgb(163, 21, 21); // Red
						TxtPreview.Styles[Style.Cpp.StringEol].BackColor = Color.Pink;
						TxtPreview.Styles[Style.Cpp.Operator].ForeColor = Color.Purple;
						TxtPreview.Styles[Style.Cpp.Preprocessor].ForeColor = Color.Maroon;
						break;
				}
			});
		}

		/// <summary>
		/// Called when clicking About, shows About window.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtnAbout_Click(object sender, EventArgs e)
		{
			new FrmAbout().ShowDialog();
		}

		/// <summary>
		/// Resets preview, clearing all preview elements.
		/// </summary>
		private void ResetPreview()
		{
			TxtPreview.Visible = false;
			TxtPreview.Text = "";

			PnlImagePreview.Visible = false;
			ImgPreview.Image = null;

			GridPreview.Visible = false;
			GridPreview.Rows.Clear();
			GridPreview.Columns.Clear();

			// Hide 3D viewer
			if (_modelViewer != null)
			{
				_modelViewer.Clear();
				_modelViewer.Visible = false;
			}

			LblPreview.Text = "Preview";
		}

		/// <summary>
		/// Shows XAC model info as text (fallback when 3D viewer unavailable)
		/// </summary>
		private void ShowXacTextInfo(string info, string reason = null)
		{
			var sb = new StringBuilder();
			if (!string.IsNullOrEmpty(reason))
			{
				sb.AppendLine($"[3D Viewer Unavailable: {reason}]");
				sb.AppendLine();
			}
			sb.Append(info);
			
			TxtPreview.ReadOnly = false;
			TxtPreview.Text = sb.ToString();
			TxtPreview.ReadOnly = true;
			TxtPreview.Visible = true;
		}

		/// <summary>
		/// Test 3D viewer with example XAC file
		/// </summary>
		private void TestViewerItem_Click(object sender, EventArgs e)
		{
			// Look for example XAC file
			var examplePath = Path.Combine(Application.StartupPath, "..", "..", "plugin_examples", 
				"Blender249 .xac and .rsm", "Example", "npc_giltine_set.xac");
			
			if (!File.Exists(examplePath))
			{
				// Try alternate paths
				var altPaths = new[]
				{
					@"plugin_examples\Blender249 .xac and .rsm\Example\npc_giltine_set.xac",
					@"..\plugin_examples\Blender249 .xac and .rsm\Example\npc_giltine_set.xac",
					@"..\..\plugin_examples\Blender249 .xac and .rsm\Example\npc_giltine_set.xac"
				};
				
				foreach (var alt in altPaths)
				{
					var fullPath = Path.GetFullPath(Path.Combine(Application.StartupPath, alt));
					if (File.Exists(fullPath))
					{
						examplePath = fullPath;
						break;
					}
				}
			}
			
			if (!File.Exists(examplePath))
			{
				// Ask user to select a file
				using (var ofd = new OpenFileDialog())
				{
					ofd.Filter = "XAC Files (*.xac)|*.xac|All Files (*.*)|*.*";
					ofd.Title = "Select XAC file to test";
					if (ofd.ShowDialog() == DialogResult.OK)
						examplePath = ofd.FileName;
					else
						return;
				}
			}
			
			try
			{
				var data = File.ReadAllBytes(examplePath);
				var xacFile = XacFile.Load(data);
				
				var info = new StringBuilder();
				info.AppendLine($"=== Test XAC: {Path.GetFileName(examplePath)} ===");
				info.AppendLine($"File Size: {data.Length:N0} bytes");
				info.AppendLine($"Nodes: {xacFile.Nodes.Count}");
				info.AppendLine($"Materials: {xacFile.Materials.Count}");
				info.AppendLine($"Meshes: {xacFile.Meshes.Count}");
				info.AppendLine();
				
				foreach (var mesh in xacFile.Meshes)
				{
					info.AppendLine($"Mesh (Node {mesh.NodeIndex}):");
					info.AppendLine($"  Vertices: {mesh.NumVertices}");
					info.AppendLine($"  Indices: {mesh.NumIndices}");
					info.AppendLine($"  SubMeshes: {mesh.NumSubMeshes}");
					info.AppendLine($"  VertexAttributes: {mesh.VertexAttributes?.Count ?? 0}");
					
					if (mesh.VertexAttributes != null)
					{
						foreach (var attr in mesh.VertexAttributes)
						{
							var typeNames = new[] { "Position", "Normal", "Tangent", "UV", "Color32", "Influences", "Color128" };
							var typeName = attr.Type < typeNames.Length ? typeNames[attr.Type] : $"Type{attr.Type}";
							info.AppendLine($"    {typeName}: {attr.AttribSize} bytes/vertex, Data: {attr.Data?.Length ?? 0} bytes");
						}
					}
				}
				
				// Try to show in 3D viewer
				if (_modelViewer != null && xacFile.Meshes.Count > 0)
				{
					ResetPreview();
					_modelViewer.Visible = true;
					_modelViewer.BringToFront();
					
					// Wait for OpenGL
					for (int i = 0; i < 20 && !_modelViewer.IsOpenGLReady; i++)
					{
						Application.DoEvents();
						System.Threading.Thread.Sleep(50);
					}
					
					if (_modelViewer.IsOpenGLReady)
					{
						_modelViewer.LoadXacModel(xacFile);
						MessageBox.Show($"Model loaded!\n\n{info}", "Test 3D Viewer", 
							MessageBoxButtons.OK, MessageBoxIcon.Information);
					}
					else
					{
						_modelViewer.Visible = false;
						var errMsg = _modelViewer.InitializationError ?? "Unknown error";
						MessageBox.Show($"OpenGL failed to initialize.\nError: {errMsg}\n\n{info}", "Test 3D Viewer", 
							MessageBoxButtons.OK, MessageBoxIcon.Warning);
					}
				}
				else
				{
					MessageBox.Show($"No meshes found or viewer unavailable.\n\n{info}", "Test 3D Viewer", 
						MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error loading XAC:\n{ex.Message}\n\n{ex.StackTrace}", "Test 3D Viewer", 
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Called when the 3D model viewer needs textures
		/// </summary>
		private void ModelViewer_TexturesNeeded(object sender, EventArgs e)
		{
			// User should right-click to apply textures manually
		}
		
		/// <summary>
		/// Apply an attachment XAC (face, head, hair) to the current 3D model
		/// Supports multiple selection
		/// </summary>
		private void ApplyAttachmentToModel_Click(object sender, EventArgs e)
		{
			if (_modelViewer == null || !_modelViewer.HasModel)
			{
				MessageBox.Show("No 3D model is currently displayed. Please load a body model first.", 
					"Apply Attachment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			
			if (LstFiles.SelectedItems.Count == 0) return;
			
			// Collect all XAC files from selection
			var xacFileNames = new List<string>();
			var xacFileData = new List<byte[]>();
			
			foreach (ListViewItem selected in LstFiles.SelectedItems)
			{
				var fileTag = selected.Tag;
				string fileName = null;
				byte[] attachmentData = null;
				
				if (fileTag is string tagStr)
				{
					fileName = Path.GetFileName(tagStr).ToLowerInvariant();
					
					// Only process XAC files
					if (!fileName.EndsWith(".xac")) continue;
					
					// Get data from the file
					if (tagStr.StartsWith("IPF") && tagStr.Contains(":"))
					{
						if (_additionalFiles.TryGetValue(tagStr, out var additionalFile))
						{
							attachmentData = additionalFile.GetData();
						}
					}
					else if (_files.TryGetValue(tagStr, out var mainFile))
					{
						attachmentData = mainFile.GetData();
					}
					else if (File.Exists(tagStr))
					{
						attachmentData = File.ReadAllBytes(tagStr);
					}
				}
				
				if (attachmentData != null)
				{
					xacFileNames.Add(fileName);
					xacFileData.Add(attachmentData);
				}
			}
			
			if (xacFileNames.Count == 0)
			{
				MessageBox.Show("Please select one or more XAC files to use as attachments.", 
					"Apply Attachment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			
			try
			{
				// Get available bones
				var boneNames = _modelViewer.GetBoneNames();
				string targetBone = "Bip01 Head"; // Default for face/hair
				
				// If shift is held, let user choose bone
				if (Control.ModifierKeys == Keys.Shift)
				{
					var result = ShowBoneSelectDialog(boneNames.ToArray(), targetBone);
					if (result == null) return; // Cancelled
					targetBone = result;
				}
				
				int successCount = 0;
				var allTextureNames = new List<string>();
				
				for (int i = 0; i < xacFileNames.Count; i++)
				{
					var attachmentXac = FileFormats.XAC.XacFile.Load(xacFileData[i]);
					
					if (attachmentXac.Meshes == null || attachmentXac.Meshes.Count == 0)
						continue;
					
					if (_modelViewer.AddAttachmentMesh(attachmentXac, targetBone))
					{
						successCount++;
						
						// Collect texture names
						var textureNames = _modelViewer.AttachmentTextureNames;
						if (textureNames != null)
						{
							foreach (var tex in textureNames)
							{
								if (!allTextureNames.Contains(tex))
									allTextureNames.Add(tex);
							}
						}
					}
				}
				
				if (successCount > 0)
				{
					string msg = $"Added {successCount} attachment(s) to '{targetBone}'";
					if (allTextureNames.Count > 0)
					{
						msg += $"\n\nRequired texture(s):\n• {string.Join("\n• ", allTextureNames)}";
						msg += "\n\nSelect texture files and right-click → 'Apply as Texture to Face/Attachment'";
					}
					LblFileName.Text = $"Attachments added: {successCount} → {targetBone}";
					MessageBox.Show(msg, "Attachments Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					MessageBox.Show($"Failed to add attachments. Check that the model has bone '{targetBone}'.", 
						"Apply Attachment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to load attachment:\n{ex.Message}", 
					"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		
		/// <summary>
		/// Show a dialog to let user select a bone
		/// </summary>
		private string ShowBoneSelectDialog(string[] boneNames, string defaultSelection)
		{
			using (var form = new Form())
			{
				form.Text = "Select Target Bone";
				form.Width = 300;
				form.Height = 400;
				form.StartPosition = FormStartPosition.CenterParent;
				form.FormBorderStyle = FormBorderStyle.FixedDialog;
				form.MaximizeBox = false;
				form.MinimizeBox = false;
				
				var label = new Label() { Left = 10, Top = 10, Width = 260, Text = "Select the bone to attach to:" };
				var listBox = new ListBox() { Left = 10, Top = 35, Width = 260, Height = 280 };
				var btnOk = new Button() { Text = "OK", Left = 100, Top = 325, Width = 80, DialogResult = DialogResult.OK };
				var btnCancel = new Button() { Text = "Cancel", Left = 190, Top = 325, Width = 80, DialogResult = DialogResult.Cancel };
				
				foreach (var name in boneNames)
					listBox.Items.Add(name);
				
				// Select default or "Bip01 Head"
				if (!string.IsNullOrEmpty(defaultSelection) && listBox.Items.Contains(defaultSelection))
					listBox.SelectedItem = defaultSelection;
				else if (listBox.Items.Contains("Bip01 Head"))
					listBox.SelectedItem = "Bip01 Head";
				else if (listBox.Items.Count > 0)
					listBox.SelectedIndex = 0;
				
				form.Controls.Add(label);
				form.Controls.Add(listBox);
				form.Controls.Add(btnOk);
				form.Controls.Add(btnCancel);
				form.AcceptButton = btnOk;
				form.CancelButton = btnCancel;
				
				if (form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
				{
					return listBox.SelectedItem.ToString();
				}
				return null;
			}
		}
		
		/// <summary>
		/// Apply XSM animation to the currently loaded 3D model
		/// </summary>
		private void ApplyAnimationToModel_Click(object sender, EventArgs e)
		{
			if (_modelViewer == null || !_modelViewer.HasModel)
			{
				MessageBox.Show("No 3D model is currently displayed. Please load a body model first.", 
					"Apply Animation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			
			if (LstFiles.SelectedItems.Count == 0) return;
			
			var selected = LstFiles.SelectedItems[0];
			var fileTag = selected.Tag;
			
			// Check extension
			string filePath;
			if (fileTag is IpfFile ipfFile)
			{
				filePath = ipfFile.FullPath;
			}
			else if (fileTag is string tagStr)
			{
				filePath = tagStr;
			}
			else
			{
				return;
			}
			
			var ext = Path.GetExtension(filePath).ToLowerInvariant();
			if (ext != ".xsm")
			{
				MessageBox.Show("Please select an XSM animation file.", 
					"Apply Animation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			
			try
			{
				byte[] animationData = null;
				
				// Get data from the file
				if (fileTag is IpfFile ipf)
				{
					animationData = ipf.GetData();
				}
				else if (fileTag is string tagString)
				{
					// Check additional files first
					if (_additionalFiles.TryGetValue(tagString, out var additionalFile))
					{
						animationData = additionalFile.GetData();
					}
					else if (_files.TryGetValue(tagString, out var mainFile))
					{
						animationData = mainFile.GetData();
					}
				}
				
				if (animationData == null)
				{
					MessageBox.Show("Could not read animation file data.", 
						"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
				
				var xsm = FileFormats.XSM.XsmFile.Load(animationData);
				
				if (xsm.MotionParts == null || xsm.MotionParts.Count == 0)
				{
					MessageBox.Show("The selected file does not contain any animation data.", 
						"Apply Animation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
				
				_modelViewer.LoadAnimation(xsm);
				_modelViewer.PlayAnimation();
				
				var fileName = Path.GetFileName(filePath);
				var duration = _modelViewer.GetAnimationDuration();
				LblFileName.Text = $"Animation: {fileName} ({duration:F1}s)";
				
				MessageBox.Show(
					$"Animation loaded: {fileName}\n" +
					$"Duration: {duration:F2} seconds\n" +
					$"Motion parts: {xsm.MotionParts.Count}\n\n" +
					$"Animation is now playing. Use 'Stop Animation' in the 3D viewer context menu to stop.",
					"Animation Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to load animation:\n{ex.Message}", 
					"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		
		/// <summary>
		/// Show a dialog with the required textures for the currently loaded or selected model
		/// </summary>
		private void ShowRequiredTextures_Click(object sender, EventArgs e)
		{
			List<string> textures = new List<string>();
			string modelName = "Current Model";
			
			// First check if an XAC file is selected
			if (LstFiles.SelectedItems.Count > 0)
			{
				var selected = LstFiles.SelectedItems[0];
				var fileTag = selected.Tag;
				
				string filePath;
				if (fileTag is IpfFile ipfFile)
				{
					filePath = ipfFile.FullPath;
				}
				else if (fileTag is string tagStr)
				{
					filePath = tagStr;
				}
				else
				{
					filePath = "";
				}
				
				var ext = Path.GetExtension(filePath).ToLowerInvariant();
				
				if (ext == ".xac")
				{
					// Load the XAC and extract texture names
					try
					{
						byte[] xacData = null;
						
						if (fileTag is IpfFile ipf)
						{
							xacData = ipf.GetData();
						}
						else if (fileTag is string tagString)
						{
							if (_additionalFiles.TryGetValue(tagString, out var additionalFile))
							{
								xacData = additionalFile.GetData();
							}
							else if (_files.TryGetValue(tagString, out var mainFile))
							{
								xacData = mainFile.GetData();
							}
						}
						
						if (xacData != null)
						{
							var xac = FileFormats.XAC.XacFile.Load(xacData);
							textures = xac.GetAllDiffuseTextures();
							modelName = Path.GetFileName(filePath);
						}
					}
					catch (Exception ex)
					{
						MessageBox.Show($"Failed to read XAC file:\n{ex.Message}", 
							"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
						return;
					}
				}
			}
			
			// If no XAC selected, show textures from currently displayed model
			if (textures.Count == 0 && _modelViewer != null && _modelViewer.HasModel)
			{
				textures = _modelViewer.RequiredTextures.ToList();
				modelName = "Currently Displayed Model";
			}
			
			if (textures.Count == 0)
			{
				MessageBox.Show("No texture references found in the model.\n\n" +
					"Note: Some models may reference textures by material name rather than filename.", 
					"Required Textures", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			
			// Show a dialog with the texture list
			using (var form = new Form())
			{
				form.Text = $"Required Textures - {modelName}";
				form.Width = 450;
				form.Height = 400;
				form.StartPosition = FormStartPosition.CenterParent;
				form.FormBorderStyle = FormBorderStyle.Sizable;
				form.MinimizeBox = false;
				
				var label = new Label() 
				{ 
					Left = 10, Top = 10, Width = 410, 
					Text = $"The model references {textures.Count} texture(s):\n" +
						"Right-click on a matching DDS/TGA file and select 'Apply as Texture' to apply." 
				};
				label.Height = 40;
				
				var listBox = new ListBox() 
				{ 
					Left = 10, Top = 55, Width = 410, Height = 260,
					Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
					HorizontalScrollbar = true
				};
				
				foreach (var tex in textures.OrderBy(t => t))
					listBox.Items.Add(tex);
				
				var btnCopy = new Button() 
				{ 
					Text = "Copy to Clipboard", 
					Left = 10, Top = 325, Width = 120,
					Anchor = AnchorStyles.Bottom | AnchorStyles.Left
				};
				btnCopy.Click += (s, ev) => 
				{
					Clipboard.SetText(string.Join("\n", textures));
					MessageBox.Show("Texture list copied to clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
				};
				
				var btnClose = new Button() 
				{ 
					Text = "Close", 
					Left = 340, Top = 325, Width = 80,
					DialogResult = DialogResult.OK,
					Anchor = AnchorStyles.Bottom | AnchorStyles.Right
				};
				
				form.Controls.Add(label);
				form.Controls.Add(listBox);
				form.Controls.Add(btnCopy);
				form.Controls.Add(btnClose);
				form.AcceptButton = btnClose;
				
				form.ShowDialog();
			}
		}

		/// <summary>
		/// Load an additional IPF file (for textures, models, etc.)
		/// </summary>
		private void LoadAdditionalIpf_Click(object sender, EventArgs e)
		{
			using (var ofd = new OpenFileDialog())
			{
				ofd.Filter = "IPF Files (*.ipf)|*.ipf|All Files (*.*)|*.*";
				ofd.Title = "Select additional IPF file to load";
				ofd.Multiselect = true; // Allow selecting multiple IPFs at once
				
				if (ofd.ShowDialog() != DialogResult.OK) return;
				
				int loadedCount = 0;
				foreach (var fileName in ofd.FileNames)
				{
					try
					{
						// Load IPF
						var ipf = new Ipf(fileName);
						ipf.Load();
						_additionalIpfs.Add(ipf);
						
						// Build file dictionary with unique prefix
						var ipfId = $"IPF{_additionalIpfs.Count}";
						foreach (var ipfFile in ipf.Files)
						{
							_additionalFiles[$"{ipfId}:{ipfFile.FullPath}"] = ipfFile;
						}
						
						// Add IPF to tree view as a new root node
						var ipfName = System.IO.Path.GetFileName(fileName);
						var rootNode = new TreeNode(ipfName)
						{
							ImageIndex = 3, // compress.png icon
							SelectedImageIndex = 3,
							Tag = $"{ipfId}:ROOT"
						};
						
						// Build folder structure for this IPF
						var folderNodes = new Dictionary<string, TreeNode>();
						foreach (var ipfFile in ipf.Files)
						{
							var parts = ipfFile.FullPath.Split('/');
							var currentPath = "";
							TreeNode parentNode = rootNode;
							
							for (int i = 0; i < parts.Length - 1; i++) // Skip the file name
							{
								currentPath += (i > 0 ? "/" : "") + parts[i];
								var folderKey = $"{ipfId}:{currentPath}";
								
								if (!folderNodes.ContainsKey(folderKey))
								{
									var folderNode = new TreeNode(parts[i])
									{
										ImageIndex = 2, // folder icon
										SelectedImageIndex = 2,
										Tag = folderKey
									};
									parentNode.Nodes.Add(folderNode);
									folderNodes[folderKey] = folderNode;
								}
								parentNode = folderNodes[folderKey];
							}
						}
						
						TreeFolders.Nodes.Add(rootNode);
						loadedCount++;
					}
					catch (Exception ex)
					{
						MessageBox.Show($"Error loading {Path.GetFileName(fileName)}:\n{ex.Message}", "Error", 
							MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}
				
				if (loadedCount > 0)
				{
					MessageBox.Show($"Loaded {loadedCount} additional IPF(s).\n\nRight-click on a DDS/TGA file and select 'Apply as Texture to 3D Model' to apply it.", 
						"Load Additional IPF", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
		}
		
		/// <summary>
		/// Close all additional IPFs
		/// </summary>
		private void CloseAdditionalIpfs_Click(object sender, EventArgs e)
		{
			if (_additionalIpfs.Count == 0)
			{
				MessageBox.Show("No additional IPFs are loaded.", "Close Additional IPFs", 
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			
			// Remove additional IPF nodes from tree
			for (int i = TreeFolders.Nodes.Count - 1; i >= 0; i--)
			{
				var tag = TreeFolders.Nodes[i].Tag?.ToString() ?? "";
				if (tag.StartsWith("IPF") && tag.Contains(":"))
				{
					TreeFolders.Nodes.RemoveAt(i);
				}
			}
			
			_additionalIpfs.Clear();
			_additionalFiles.Clear();
			
			MessageBox.Show("All additional IPFs closed.", "Close Additional IPFs", 
				MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		/// <summary>
		/// Import any file into the current IPF
		/// </summary>
		private void ImportIesFile_Click(object sender, EventArgs e)
		{
			if (_openedIpf == null)
			{
				MessageBox.Show("Please open an IPF file first.", "Import File", 
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			// Get the current folder from tree selection for destination path
			string currentFolder = "";
			if (TreeFolders.SelectedNode != null)
			{
				currentFolder = TreeFolders.SelectedNode.FullPath.Replace("\\", "/");
				// Remove IPF prefix if from additional IPF
				if (currentFolder.StartsWith("IPF"))
				{
					var colonIndex = currentFolder.IndexOf(':');
					if (colonIndex > 0)
						currentFolder = currentFolder.Substring(colonIndex + 1).TrimStart('/');
				}
			}

			// If no folder selected, use the first pack name from the IPF
			if (string.IsNullOrEmpty(currentFolder))
			{
				currentFolder = _openedIpf.Files.FirstOrDefault()?.PackFileName ?? "data";
			}

			// Ask user for the file(s) to import
			using (var openDialog = new OpenFileDialog())
			{
				openDialog.Title = "Select file(s) to import";
				openDialog.Filter = "All files|*.*|IES files|*.ies|XML files|*.xml|DDS textures|*.dds|XAC models|*.xac|LUA scripts|*.lua";
				openDialog.Multiselect = true;

				if (openDialog.ShowDialog() != DialogResult.OK)
					return;

				int importedCount = 0;
				int skippedCount = 0;

				foreach (var filePath in openDialog.FileNames)
				{
					try
					{
						var fileName = Path.GetFileName(filePath);
						var extension = Path.GetExtension(filePath).ToLowerInvariant();
						byte[] fileBytes;

						if (extension == ".xml")
						{
							// Special handling: Convert XML to IES binary
							var xml = File.ReadAllText(filePath);
							var iesFile = IesFile.LoadFromXmlString(xml);
							fileBytes = iesFile.SaveToBytes();
							fileName = Path.GetFileNameWithoutExtension(fileName) + ".ies";
						}
						else
						{
							// Read file as raw bytes
							fileBytes = File.ReadAllBytes(filePath);
						}

						// Build destination path: currentFolder/filename
						var destPath = currentFolder + "/" + fileName;

						// Check if file already exists
						if (_files.ContainsKey(destPath) || _importedFiles.ContainsKey(destPath))
						{
							var result = MessageBox.Show(
								$"A file already exists at '{destPath}'.\nDo you want to replace it?",
								"File Exists", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
							
							if (result != DialogResult.Yes)
							{
								skippedCount++;
								continue;
							}
						}

						// Add to imported files (temporary until saved)
						_importedFiles[destPath] = fileBytes;

						// Add to file tree for visualization
						AddImportedFileToTree(destPath);

						// Refresh the file list if we're viewing the target folder
						if (TreeFolders.SelectedNode != null)
						{
							var selectedPath = TreeFolders.SelectedNode.FullPath.Replace("\\", "/");
							if (destPath.StartsWith(selectedPath + "/") || selectedPath == currentFolder)
							{
								// Refresh file list to show the imported file
								TreeFolders_AfterSelect(TreeFolders, new TreeViewEventArgs(TreeFolders.SelectedNode));
							}
						}

						importedCount++;
					}
					catch (Exception ex)
					{
						MessageBox.Show($"Failed to import '{Path.GetFileName(filePath)}':\n{ex.Message}",
							"Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
				}

				UpdatePendingChangesUI();

				// Show summary
				if (importedCount > 0)
				{
					var msg = $"Imported {importedCount} file(s) to '{currentFolder}'.";
					if (skippedCount > 0)
						msg += $"\n{skippedCount} file(s) were skipped.";
					msg += "\n\nFiles are temporarily added. Use 'Save IES' to save to IPF.";
					MessageBox.Show(msg, "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
		}

		/// <summary>
		/// Add an imported file to the tree view for visualization
		/// </summary>
		private void AddImportedFileToTree(string fullPath)
		{
			var parts = fullPath.Split('/');
			TreeNode currentNode = null;

			for (int i = 0; i < parts.Length; i++)
			{
				var part = parts[i];
				TreeNodeCollection nodes = currentNode == null ? TreeFolders.Nodes : currentNode.Nodes;

				// Find or create node
				TreeNode found = null;
				foreach (TreeNode node in nodes)
				{
					if (node.Text == part)
					{
						found = node;
						break;
					}
				}

				if (found == null)
				{
					found = new TreeNode(part);
					if (i < parts.Length - 1)
					{
						// Folder
						found.ImageKey = "folder";
						found.SelectedImageKey = "folder_open";
					}
					else
					{
						// File node - mark as imported
						found.ImageKey = "imported";
						found.ForeColor = Color.Green;
						found.Tag = "IMPORTED:" + fullPath;
					}
					nodes.Add(found);
				}
				else if (i == parts.Length - 1)
				{
					// Update existing node to show as imported
					found.ForeColor = Color.Green;
					found.Tag = "IMPORTED:" + fullPath;
				}

				currentNode = found;
			}

			// Add to folder list for display in file list
			var folderPath = string.Join("/", parts.Take(parts.Length - 1));
			if (!_folders.ContainsKey(folderPath))
				_folders[folderPath] = new List<string>();
			
			if (!_folders[folderPath].Contains(fullPath))
				_folders[folderPath].Add(fullPath);
		}
		
		/// <summary>
		/// Context menu opening - enable/disable items based on selection
		/// </summary>
		private void FileListContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (LstFiles.SelectedItems.Count == 0)
			{
				e.Cancel = true;
				return;
			}
			
			var selected = LstFiles.SelectedItems[0];
			var fileTag = selected.Tag;
			string filePath = "";
			
			if (fileTag is IpfFile ipf)
				filePath = ipf.FullPath;
			else if (fileTag is string s)
				filePath = s;
			
			var ext = Path.GetExtension(filePath).ToLowerInvariant();
			
			// Show "Apply as Texture" for image files when a model has been loaded
			var isTexture = ext == ".dds" || ext == ".tga" || ext == ".png" || ext == ".jpg" || ext == ".bmp";
			var isXac = ext == ".xac";
			var isXsm = ext == ".xsm";
			var hasModel = _modelViewer != null && _modelViewer.HasModel;
			var hasAttachments = _modelViewer != null && _modelViewer.HasAttachments;
			
			// Find items by Name so indices changes won't break visibility logic
			ToolStripItem applyTexture = _fileListContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "applyTexture");
			ToolStripItem applyFaceTexture = _fileListContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "applyFaceTexture");
			ToolStripItem applyAttachment = _fileListContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "applyAttachment");
			ToolStripItem applyAnimation = _fileListContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "applyAnimation");
			ToolStripItem showRequiredTextures = _fileListContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "showRequiredTextures");
			ToolStripItem extractItem = _fileListContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "extract");
			ToolStripItem removeImportItem = _fileListContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "removeImport");
			ToolStripItem deleteFileItem = _fileListContextMenu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "deleteFile");

			// Show "Apply as Texture" for image files when a model has been loaded
			if (applyTexture != null) { applyTexture.Visible = isTexture && hasModel; applyTexture.Enabled = isTexture && hasModel; }
			if (applyFaceTexture != null) { applyFaceTexture.Visible = isTexture && hasModel && hasAttachments; applyFaceTexture.Enabled = isTexture && hasModel && hasAttachments; }
			if (applyAttachment != null) { applyAttachment.Visible = isXac && hasModel; applyAttachment.Enabled = isXac && hasModel; }
			if (applyAnimation != null) { applyAnimation.Visible = isXsm && hasModel; applyAnimation.Enabled = isXsm && hasModel; }
			if (showRequiredTextures != null) { showRequiredTextures.Visible = isXac || (hasModel && !isTexture && !isXsm); showRequiredTextures.Enabled = isXac || hasModel; }

			// Extract is always available
			if (extractItem != null) { extractItem.Visible = true; extractItem.Enabled = true; extractItem.Text = LstFiles.SelectedItems.Count > 1 ? $"Extract {LstFiles.SelectedItems.Count} Selected File(s)..." : "Extract Selected File..."; }

			// Imported / extract / delete items
			var fileTagStr = filePath;
			var isImported = _importedFiles.ContainsKey(fileTagStr);
			if (removeImportItem != null) { removeImportItem.Visible = isImported; removeImportItem.Enabled = isImported; }
			var isMainIpfFile = _files.ContainsKey(fileTagStr) && !isImported;
			var isAlreadyDeleted = _deletedFiles.Contains(fileTagStr);
			if (deleteFileItem != null) { deleteFileItem.Visible = isMainIpfFile; deleteFileItem.Enabled = isMainIpfFile && !isAlreadyDeleted; deleteFileItem.Text = isAlreadyDeleted ? "Already marked for deletion" : "Delete File from IPF"; }
		}
		
		/// <summary>
		/// Remove an imported file from the import list
		/// </summary>
		private void RemoveImportedFile_Click(object sender, EventArgs e)
		{
			if (LstFiles.SelectedItems.Count == 0) return;
			
			var selected = LstFiles.SelectedItems[0];
			var fileTag = (string)selected.Tag;
			
			if (!_importedFiles.ContainsKey(fileTag))
				return;
			
			var result = MessageBox.Show(
				$"Remove imported file '{Path.GetFileName(fileTag)}'?\n\nThis file has not been saved to the IPF yet.",
				"Remove Imported File", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
			
			if (result != DialogResult.Yes)
				return;
			
			// Remove from imports
			_importedFiles.Remove(fileTag);
			
			// If currently editing this file, stop editing
			if (_currentIesFilePath == fileTag)
			{
				_pendingChanges.Remove(fileTag);
				_isEditingIes = false;
				_currentIesFilePath = null;
				ResetPreview();
			}
			
			// Remove from tree
			RemoveImportedFileFromTree(fileTag);
			
			// Refresh file list
			if (TreeFolders.SelectedNode != null)
				TreeFolders_AfterSelect(TreeFolders, new TreeViewEventArgs(TreeFolders.SelectedNode));
			
			UpdatePendingChangesUI();
		}

		/// <summary>
		/// Mark a file for deletion from the IPF
		/// </summary>
		private void DeleteFileFromIpf_Click(object sender, EventArgs e)
		{
			if (LstFiles.SelectedItems.Count == 0) return;
			
			var selected = LstFiles.SelectedItems[0];
			var fileTag = (string)selected.Tag;
			
			if (!_files.ContainsKey(fileTag))
				return;
			
			if (_deletedFiles.Contains(fileTag))
			{
				MessageBox.Show("This file is already marked for deletion.", "Delete File", 
					MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			
			var result = MessageBox.Show(
				$"Mark '{Path.GetFileName(fileTag)}' for deletion?\n\nThe file will be removed from the IPF when you save.",
				"Delete File", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			
			if (result != DialogResult.Yes)
				return;
			
			// Mark for deletion
			_deletedFiles.Add(fileTag);
			
			// Also remove from pending changes if it was modified
			_pendingChanges.Remove(fileTag);
			
			// If currently editing this file, stop editing
			if (_currentIesFilePath == fileTag)
			{
				_isEditingIes = false;
				_currentIesFilePath = null;
				ResetPreview();
			}
			
			// Refresh file list to show deletion indicator
			if (TreeFolders.SelectedNode != null)
				TreeFolders_AfterSelect(TreeFolders, new TreeViewEventArgs(TreeFolders.SelectedNode));
			
			UpdatePendingChangesUI();
		}
		
		/// <summary>
		/// Apply selected texture to the 3D model
		/// </summary>
		private void ApplyTextureToModel_Click(object sender, EventArgs e)
		{
			if (LstFiles.SelectedItems.Count == 0) return;
			if (_modelViewer == null) return;
			
			// Make sure the model viewer has a model loaded
			if (!_modelViewer.HasModel)
			{
				MessageBox.Show("No 3D model is loaded. Please load an XAC file first.", "No Model", 
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			
			var selected = LstFiles.SelectedItems[0];
			var fileTag = selected.Tag;
			
			// Get the file path and data
			string filePath = null;
			byte[] textureData = null;
			
			if (fileTag is string tagStr)
			{
				filePath = tagStr;
				// Try to find in additional files first (for loaded IPFs)
				if (tagStr.StartsWith("IPF") && tagStr.Contains(":"))
				{
					if (_additionalFiles.TryGetValue(tagStr, out var additionalFile))
					{
						textureData = additionalFile.GetData();
						filePath = tagStr.Substring(tagStr.IndexOf(':') + 1);
					}
				}
				else if (_files.TryGetValue(tagStr, out var mainFile))
				{
					textureData = mainFile.GetData();
				}
			}
			
			if (string.IsNullOrEmpty(filePath))
			{
				MessageBox.Show("Could not determine file path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			
			if (textureData == null)
			{
				MessageBox.Show($"Could not read file data for:\n{filePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			
			try
			{
				Bitmap bitmap = null;
				var ext = Path.GetExtension(filePath).ToLowerInvariant();
				
				if (ext == ".dds")
				{
					try
					{
						var dds = new DDSImage(textureData);
						bitmap = dds.BitmapImage;
					}
					catch (Exception ddsEx)
					{
						throw new Exception($"DDS decode error: {ddsEx.Message}");
					}
					if (bitmap == null)
					{
						throw new Exception("Unable to decode DDS image - unsupported format");
					}
				}
				else if (ext == ".tga")
				{
					using (var ms = new MemoryStream(textureData))
					{
						var tga = new TargaImage(ms);
						bitmap = new Bitmap(tga.Image);
					}
				}
				else if (ext == ".png" || ext == ".jpg" || ext == ".bmp")
				{
					using (var ms = new MemoryStream(textureData))
					{
						bitmap = new Bitmap(Image.FromStream(ms));
					}
				}
				
				if (bitmap != null)
				{
					// Ask which material to apply to (if multiple materials)
					int materialId = 0;
					if (_modelViewer.Materials.Count > 1)
					{
						var materialNames = _modelViewer.Materials.Select((m, i) => $"{i}: {m.Name}").ToArray();
						var result = ShowMaterialSelectDialog(materialNames);
						if (result < 0) return;
						materialId = result;
					}
					
					_modelViewer.LoadTexture(materialId, bitmap);
					bitmap.Dispose();
					
					// Show the model viewer with the new texture
					_modelViewer.Visible = true;
					_modelViewer.BringToFront();
					
					MessageBox.Show($"Texture applied to body!", "Apply Texture", 
						MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error loading texture:\n{ex.Message}", "Error", 
					MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}
		
		/// <summary>
		/// Apply selected texture to the face/attachment meshes only
		/// </summary>
		private void ApplyTextureToAttachment_Click(object sender, EventArgs e)
		{
			if (LstFiles.SelectedItems.Count == 0) return;
			if (_modelViewer == null || !_modelViewer.HasModel || !_modelViewer.HasAttachments)
			{
				MessageBox.Show("No attachments loaded. Add a face/hair first.", "No Attachments", 
					MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}
			
			int successCount = 0;
			int failCount = 0;
			string lastError = null;
			
			// Process all selected items (multi-select support)
			foreach (ListViewItem selected in LstFiles.SelectedItems)
			{
				var fileTag = selected.Tag;
				
				// Get the file path and data
				string filePath = null;
				byte[] textureData = null;
				
				if (fileTag is string tagStr)
				{
					filePath = tagStr;
					// Try to find in additional files first (for loaded IPFs like char_texture.ipf)
					if (tagStr.StartsWith("IPF") && tagStr.Contains(":"))
					{
						if (_additionalFiles.TryGetValue(tagStr, out var additionalFile))
						{
							textureData = additionalFile.GetData();
							// For display, use the path after the colon
							filePath = tagStr.Substring(tagStr.IndexOf(':') + 1);
						}
					}
					else if (_files.TryGetValue(tagStr, out var mainFile))
					{
						textureData = mainFile.GetData();
					}
					else if (File.Exists(tagStr))
					{
						// Fallback: read directly from filesystem for extracted/loose files
						textureData = File.ReadAllBytes(tagStr);
					}
				}
				
				if (string.IsNullOrEmpty(filePath) || textureData == null)
				{
					failCount++;
					lastError = $"Could not read: {filePath ?? "unknown"}";
					continue;
				}
				
				// Only process texture files
				var ext = Path.GetExtension(filePath).ToLowerInvariant();
				if (ext != ".dds" && ext != ".tga" && ext != ".png" && ext != ".jpg" && ext != ".bmp")
				{
					continue; // Skip non-texture files silently
				}
				
				try
				{
					Bitmap bitmap = null;
					
					if (ext == ".dds")
					{
						var dds = new DDSImage(textureData);
						bitmap = dds.BitmapImage;
						if (bitmap == null)
						{
							failCount++;
							lastError = $"DDS decode failed: {Path.GetFileName(filePath)}";
							continue;
						}
					}
					else if (ext == ".tga")
					{
						using (var ms = new MemoryStream(textureData))
						{
							var tga = new TargaImage(ms);
							bitmap = new Bitmap(tga.Image);
						}
					}
					else
					{
						using (var ms = new MemoryStream(textureData))
						{
							bitmap = new Bitmap(Image.FromStream(ms));
						}
					}
					
					if (bitmap != null)
					{
						string textureFileName = Path.GetFileName(filePath);
						_modelViewer.LoadAttachmentTexture(bitmap, textureFileName);
						successCount++;
						bitmap.Dispose();
					}
				}
				catch (Exception ex)
				{
					failCount++;
					lastError = $"{Path.GetFileName(filePath)}: {ex.Message}";
				}
			}
			
			if (successCount > 0)
			{
				_modelViewer.Visible = true;
				_modelViewer.BringToFront();
			}
			
			// Show summary if multiple files or if there were errors
			if (LstFiles.SelectedItems.Count > 1 || failCount > 0)
			{
				string msg = $"Applied {successCount} texture(s)";
				if (failCount > 0)
				{
					msg += $"\nFailed: {failCount}";
					if (lastError != null)
						msg += $"\nLast error: {lastError}";
				}
				MessageBox.Show(msg, "Texture Application", MessageBoxButtons.OK, 
					failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
			}
		}
		
		/// <summary>
		/// Show a dialog to select which material to apply texture to
		/// </summary>
		private int ShowMaterialSelectDialog(string[] materials)
		{
			using (var form = new Form())
			{
				form.Text = "Select Material";
				form.Size = new Size(300, 150);
				form.StartPosition = FormStartPosition.CenterParent;
				form.FormBorderStyle = FormBorderStyle.FixedDialog;
				form.MaximizeBox = false;
				form.MinimizeBox = false;
				
				var label = new Label { Text = "Select material to apply texture to:", Left = 10, Top = 10, Width = 270 };
				var combo = new ComboBox { Left = 10, Top = 35, Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
				combo.Items.AddRange(materials);
				combo.SelectedIndex = 0;
				
				var okBtn = new Button { Text = "OK", Left = 120, Top = 70, Width = 75, DialogResult = DialogResult.OK };
				var cancelBtn = new Button { Text = "Cancel", Left = 205, Top = 70, Width = 75, DialogResult = DialogResult.Cancel };
				
				form.Controls.AddRange(new Control[] { label, combo, okBtn, cancelBtn });
				form.AcceptButton = okBtn;
				form.CancelButton = cancelBtn;
				
				if (form.ShowDialog() == DialogResult.OK)
					return combo.SelectedIndex;
				return -1;
			}
		}

		/// <summary>
		/// Called when clicking Extrack Pack button, extracts current IPF
		/// file to selected destination.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtnExtractPack_Click(object sender, EventArgs e)
		{
			// Show dialog to select files/IPFs to extract
			var dlg = new FrmExtractDialog(_openedIpf, _files, _additionalIpfs, _additionalFiles);
			if (dlg.ShowDialog() != DialogResult.OK)
				return;

			if (dlg.SelectedFiles.Count == 0)
			{
				MessageBox.Show("No files selected.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			FbdExtractPack.Description = "Select folder to extract files to.";
			FbdExtractPack.ShowNewFolderButton = true;

			if (FbdExtractPack.ShowDialog() != DialogResult.OK)
				return;

			var extractPath = FbdExtractPack.SelectedPath;

			if (!Directory.Exists(extractPath))
			{
				MessageBox.Show("Directory not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// Group files by source IPF
			var filesByIpf = dlg.SelectedFiles.GroupBy(f => f.SourceIpfIndex).ToList();

			// If extracting from multiple IPFs, create per-IPF folders
			bool multipleIpfs = filesByIpf.Count > 1;
			
			var progressDlg = new FrmProgress(dlg.SelectedFiles.Count);
			progressDlg.StartPosition = FormStartPosition.CenterScreen;
			progressDlg.TopMost = true;
			progressDlg.Show(this);
			progressDlg.BringToFront();
			Application.DoEvents();
			
			int extractedCount = 0;

			foreach (var ipfGroup in filesByIpf)
			{
				var targetFolder = extractPath;
				if (multipleIpfs)
				{
					// Create a folder for this IPF
					string ipfName;
					if (ipfGroup.Key == -1)
					{
						ipfName = Path.GetFileNameWithoutExtension(_openedIpf.FilePath);
					}
					else
					{
						ipfName = Path.GetFileNameWithoutExtension(_additionalIpfs[ipfGroup.Key].FilePath);
					}
					targetFolder = Path.Combine(extractPath, ipfName);
					Directory.CreateDirectory(targetFolder);
				}

				// Extract all files from this IPF
				foreach (var item in ipfGroup)
				{
					if (progressDlg.Cancel)
						break;
						
					var fileTag = item.FileTag;
					var fileName = item.FileName;
					try
					{
						IpfFile ipfFile = null;
						if (fileTag.StartsWith("IPF") && fileTag.Contains(":"))
						{
							if (!_additionalFiles.TryGetValue(fileTag, out ipfFile))
								continue;
						}
						else if (!_files.TryGetValue(fileTag, out ipfFile))
						{
							continue;
						}

						// Create directories as needed
						var outputPath = Path.Combine(targetFolder, fileName);
						var outputDir = Path.GetDirectoryName(outputPath);
						Directory.CreateDirectory(outputDir);

						// Extract file
						var fileData = ipfFile.GetData();
						File.WriteAllBytes(outputPath, fileData);
						extractedCount++;
					}
					catch (Exception ex)
					{
						MessageBox.Show($"Error extracting {fileName}: {ex.Message}", Text, 
							MessageBoxButtons.OK, MessageBoxIcon.Error);
					}
					
					progressDlg.UpdateProgress(extractedCount);
					Application.DoEvents();
				}
				
				if (progressDlg.Cancel)
					break;
			}

			progressDlg.Close();
			MessageBox.Show($"Extracted {extractedCount} file(s) successfully.", Text, 
				MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		/// <summary>
		/// Called when clicking Extract File button, extracts selected
		/// file from IPF to selected destination.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtnExtractFile_Click(object sender, EventArgs e)
		{
			// Show dialog to select file(s) to extract
			var dlg = new FrmExtractDialog(_openedIpf, _files, _additionalIpfs, _additionalFiles);
			if (dlg.ShowDialog() != DialogResult.OK)
				return;

			if (dlg.SelectedFiles.Count == 0)
			{
				MessageBox.Show("No files selected.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// If only one file, use Save dialog; otherwise use folder dialog
			if (dlg.SelectedFiles.Count == 1)
			{
				var selectedFile = dlg.SelectedFiles[0];
				var fileTag = selectedFile.FileTag;
				var fileName = selectedFile.FileName;
				var displayName = Path.GetFileName(fileName);
				SavExtractFile.FileName = displayName;

				if (SavExtractFile.ShowDialog() != DialogResult.OK)
					return;

				try
				{
					IpfFile ipfFile = null;
					if (fileTag.StartsWith("IPF") && fileTag.Contains(":"))
					{
						if (!_additionalFiles.TryGetValue(fileTag, out ipfFile))
						{
							MessageBox.Show("File not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
							return;
						}
					}
					else if (!_files.TryGetValue(fileTag, out ipfFile))
					{
						MessageBox.Show("File not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
						return;
					}

					var fileData = ipfFile.GetData();
					File.WriteAllBytes(SavExtractFile.FileName, fileData);
					MessageBox.Show("File extracted successfully.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Error extracting file: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}
			else
			{
				// Multiple files - use folder browser
				FbdExtractPack.Description = "Select folder to extract files to.";
				FbdExtractPack.ShowNewFolderButton = true;

				if (FbdExtractPack.ShowDialog() != DialogResult.OK)
					return;

				var extractPath = FbdExtractPack.SelectedPath;

				if (!Directory.Exists(extractPath))
				{
					MessageBox.Show("Directory not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}

				// Group files by source IPF
				var filesByIpf = dlg.SelectedFiles.GroupBy(f => f.SourceIpfIndex).ToList();
				bool multipleIpfs = filesByIpf.Count() > 1;
				int extractedCount = 0;

				foreach (var ipfGroup in filesByIpf)
				{
					var targetFolder = extractPath;
					if (multipleIpfs)
					{
						string ipfName;
						if (ipfGroup.Key == -1)
						{
							ipfName = Path.GetFileNameWithoutExtension(_openedIpf.FilePath);
						}
						else
						{
							ipfName = Path.GetFileNameWithoutExtension(_additionalIpfs[ipfGroup.Key].FilePath);
						}
						targetFolder = Path.Combine(extractPath, ipfName);
						Directory.CreateDirectory(targetFolder);
					}

					foreach (var item in ipfGroup)
					{
						var fileTag = item.FileTag;
						var fileName = item.FileName;
						try
						{
							IpfFile ipfFile = null;
							if (fileTag.StartsWith("IPF") && fileTag.Contains(":"))
							{
								if (!_additionalFiles.TryGetValue(fileTag, out ipfFile))
									continue;
							}
							else if (!_files.TryGetValue(fileTag, out ipfFile))
							{
								continue;
							}

							var outputPath = Path.Combine(targetFolder, fileName);
							var outputDir = Path.GetDirectoryName(outputPath);
							Directory.CreateDirectory(outputDir);

							var fileData = ipfFile.GetData();
							File.WriteAllBytes(outputPath, fileData);
							extractedCount++;
						}
						catch (Exception ex)
						{
							MessageBox.Show($"Error extracting {fileName}: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
						}
					}
				}

				MessageBox.Show($"Extracted {extractedCount} file(s) successfully.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
		}

		/// <summary>
		/// Extracts an entire folder from the tree view.
		/// </summary>
		private void ExtractFolder_Click(object sender, EventArgs e)
		{
			if (TreeFolders.SelectedNode == null)
				return;

			FbdExtractPack.Description = "Select folder to extract files to.";
			FbdExtractPack.ShowNewFolderButton = true;

			if (FbdExtractPack.ShowDialog() != DialogResult.OK)
				return;

			var extractPath = FbdExtractPack.SelectedPath;
			if (!Directory.Exists(extractPath))
			{
				MessageBox.Show("Directory not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var fullPath = TreeFolders.SelectedNode.FullPath.Replace('\\', '/');
			var pathParts = fullPath.Split('/');
			var firstPart = pathParts[0];
			bool isAdditionalIpf = firstPart.EndsWith(".ipf");
			
			var filesToExtract = new List<IpfFile>();
			string ipfName;

			if (isAdditionalIpf)
			{
				// Additional IPF folder: "char_texture.ipf/misc/accessory" -> "misc/accessory"
				var pathWithoutIpf = string.Join("/", pathParts.Skip(1));
				var searchPath = pathWithoutIpf + "/";
				
				// Find IPF index
				int ipfIndex = -1;
				foreach (var kvp in _additionalFiles)
				{
					var colonIndex = kvp.Key.IndexOf(':');
					if (colonIndex > 0)
					{
						var filePath = kvp.Key.Substring(colonIndex + 1);
						if (filePath.StartsWith(searchPath) || filePath == pathWithoutIpf)
						{
							if (ipfIndex == -1)
								ipfIndex = int.Parse(kvp.Key.Substring(3, colonIndex - 3)) - 1;
							filesToExtract.Add(kvp.Value);
						}
					}
				}
				ipfName = ipfIndex >= 0 ? Path.GetFileNameWithoutExtension(_additionalIpfs[ipfIndex].FilePath) 
					: Path.GetFileNameWithoutExtension(_openedIpf.FilePath);
			}
			else
			{
				// Main IPF folder
				var searchPath = fullPath + "/";
				foreach (var kvp in _files)
				{
					var filePath = kvp.Key.Replace('\\', '/');
					if (filePath.StartsWith(searchPath) || filePath == fullPath)
						filesToExtract.Add(kvp.Value);
				}
				ipfName = Path.GetFileNameWithoutExtension(_openedIpf.FilePath);
			}

			if (filesToExtract.Count == 0)
			{
				MessageBox.Show("No files found in folder.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			var targetFolder = Path.Combine(extractPath, ipfName);
			Directory.CreateDirectory(targetFolder);

			var progressDlg = new FrmProgress(filesToExtract.Count);
			progressDlg.StartPosition = FormStartPosition.CenterScreen;
			progressDlg.TopMost = true;
			progressDlg.Show(this);
			progressDlg.BringToFront();
			Application.DoEvents();

			int extractedCount = 0;
			foreach (var ipfFile in filesToExtract)
			{
				if (progressDlg.Cancel) break;

				try
				{
					var outputPath = Path.Combine(targetFolder, ipfFile.Path);
					Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
					File.WriteAllBytes(outputPath, ipfFile.GetData());
					extractedCount++;
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Error extracting file: {ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}

				progressDlg.UpdateProgress(extractedCount);
				Application.DoEvents();
			}

			progressDlg.Close();
			MessageBox.Show($"Extracted {extractedCount} file(s).", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		/// <summary>
		/// Selects all files in the currently displayed folder in the file list.
		/// </summary>
		private void SelectAllInFolder_Click(object sender, EventArgs e)
		{
			LstFiles.BeginUpdate();
			foreach (ListViewItem item in LstFiles.Items)
			{
				item.Selected = true;
			}
			LstFiles.EndUpdate();
			LstFiles.Focus();
		}

		/// <summary>
		/// Handle tree view mouse down for multi-select (Ctrl+Click)
		/// </summary>
		private void TreeFolders_MouseDown(object sender, MouseEventArgs e)
		{
			var clickedNode = TreeFolders.GetNodeAt(e.X, e.Y);
			if (clickedNode != null)
			{
				// Right-click: if node not already selected, add it to selection
				if (e.Button == MouseButtons.Right)
				{
					if (!_selectedTreeNodes.Contains(clickedNode))
					{
						_selectedTreeNodes.Add(clickedNode);
						TreeFolders.Invalidate();
					}
					return; // Let context menu handle the rest
				}

				// Left-click with Ctrl: toggle selection
				if (Control.ModifierKeys == Keys.Control)
				{
					if (_selectedTreeNodes.Contains(clickedNode))
						_selectedTreeNodes.Remove(clickedNode);
					else
						_selectedTreeNodes.Add(clickedNode);
					TreeFolders.Invalidate();
				}
				else
				{
					// Single select - clear others
					_selectedTreeNodes.Clear();
					_selectedTreeNodes.Add(clickedNode);
					TreeFolders.SelectedNode = clickedNode;
				}
			}
		}

		/// <summary>
		/// Draw selected tree nodes with highlight
		/// </summary>
		private void TreeFolders_DrawNode(object sender, DrawTreeNodeEventArgs e)
		{
			if (_selectedTreeNodes.Contains(e.Node))
			{
				// Draw with selection background
				e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
				TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.TreeView.Font, e.Bounds, SystemColors.HighlightText, TextFormatFlags.VerticalCenter);
			}
			else
			{
				// Draw normally
				e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
				TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.TreeView.Font, e.Bounds, SystemColors.WindowText, TextFormatFlags.VerticalCenter);
			}
		}

		/// <summary>
		/// Update folder context menu based on selection
		/// </summary>
		private void FolderContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
		{
			var menu = sender as ContextMenuStrip;
			if (menu == null) return;

			var extractFolderItem = menu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "extractFolder");
			var extractSelectedItem = menu.Items.Cast<ToolStripItem>().FirstOrDefault(i => i.Name == "extractSelectedFolders");
			
			// Show "Extract This Folder" for single selection, "Extract Selected Folders" for multiple
			if (_selectedTreeNodes.Count > 1)
			{
				if (extractFolderItem != null) extractFolderItem.Visible = false;
				if (extractSelectedItem != null)
				{
					extractSelectedItem.Visible = true;
					extractSelectedItem.Text = $"Extract {_selectedTreeNodes.Count} Selected Folders...";
				}
			}
			else
			{
				if (extractFolderItem != null) extractFolderItem.Visible = true;
				if (extractSelectedItem != null) extractSelectedItem.Visible = false;
			}
		}

		/// <summary>
		/// Extract multiple selected folders
		/// </summary>
		private void ExtractSelectedFolders_Click(object sender, EventArgs e)
		{
			if (_selectedTreeNodes.Count == 0)
			{
				MessageBox.Show("No folders selected.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			FbdExtractPack.Description = "Select folder to extract files to.";
			FbdExtractPack.ShowNewFolderButton = true;

			if (FbdExtractPack.ShowDialog() != DialogResult.OK)
				return;

			var extractPath = FbdExtractPack.SelectedPath;
			if (!Directory.Exists(extractPath))
			{
				MessageBox.Show("Directory not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// Collect files with source IPF tracking: (filePath, IpfFile, ipfIndex)
			var filesToExtract = new List<Tuple<string, IpfFile, int>>();
			
			foreach (var node in _selectedTreeNodes)
			{
				var tag = node.Tag as string;
				
				// Check if this is an additional IPF folder (has Tag like "IPF1:path")
				if (!string.IsNullOrEmpty(tag) && tag.Contains(":") && tag.StartsWith("IPF"))
				{
					// Additional IPF folder: extract IPF index and path from Tag
					var colonIndex = tag.IndexOf(':');
					var ipfId = int.Parse(tag.Substring(3, colonIndex - 3)) - 1; // "IPF1:" -> 0
					var folderPath = tag.Substring(colonIndex + 1); // "misc/accessory"
					var searchPath = folderPath + "/";
					
					// Find all files in this folder from this specific IPF
					foreach (var kvp in _additionalFiles)
					{
						var fileColonIndex = kvp.Key.IndexOf(':');
						if (fileColonIndex <= 0) continue;
						
						var fileIpfId = int.Parse(kvp.Key.Substring(3, fileColonIndex - 3)) - 1;
						if (fileIpfId != ipfId) continue; // Only files from same IPF
						
						var filePath = kvp.Key.Substring(fileColonIndex + 1);
						if (filePath.StartsWith(searchPath) || filePath == folderPath)
						{
							filesToExtract.Add(new Tuple<string, IpfFile, int>(filePath, kvp.Value, ipfId));
						}
					}
				}
				else
				{
					// Main IPF folder - use node.Name which contains the path
					var folderPath = node.Name.Replace('\\', '/');
					var searchPath = folderPath;
					if (!searchPath.EndsWith("/"))
						searchPath += "/";
					
					foreach (var kvp in _files)
					{
						var filePath = kvp.Key.Replace('\\', '/');
						if (filePath.StartsWith(searchPath) || filePath == folderPath.TrimEnd('/'))
							filesToExtract.Add(new Tuple<string, IpfFile, int>(filePath, kvp.Value, -1));
					}
				}
			}

			if (filesToExtract.Count == 0)
			{
				MessageBox.Show("No files found in selected folders.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			// Group by IPF and extract
			var filesByIpf = filesToExtract.GroupBy(f => f.Item3).ToList();
			
			var progressDlg = new FrmProgress(filesToExtract.Count);
			progressDlg.StartPosition = FormStartPosition.CenterScreen;
			progressDlg.TopMost = true;
			progressDlg.Show(this);
			progressDlg.BringToFront();
			Application.DoEvents();

			int extractedCount = 0;
			foreach (var ipfGroup in filesByIpf)
			{
				string ipfName = ipfGroup.Key == -1 
					? Path.GetFileNameWithoutExtension(_openedIpf.FilePath)
					: Path.GetFileNameWithoutExtension(_additionalIpfs[ipfGroup.Key].FilePath);
				
				var targetFolder = Path.Combine(extractPath, ipfName);
				Directory.CreateDirectory(targetFolder);

				foreach (var item in ipfGroup)
				{
					if (progressDlg.Cancel) break;
					
					try
					{
						var outputPath = Path.Combine(targetFolder, item.Item2.Path);
						Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
						File.WriteAllBytes(outputPath, item.Item2.GetData());
						extractedCount++;
					}
					catch (Exception ex)
					{
						MessageBox.Show($"Error extracting {item.Item1}: {ex.Message}", Text,
							MessageBoxButtons.OK, MessageBoxIcon.Error);
					}

					progressDlg.UpdateProgress(extractedCount);
					Application.DoEvents();
				}
			}

			progressDlg.Close();
			MessageBox.Show($"Extracted {extractedCount} file(s) from {_selectedTreeNodes.Count} folder(s).", Text,
				MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		/// <summary>
		/// Extracts files selected in the main file list.
		/// </summary>
		private void ExtractSelectedFiles_Click(object sender, EventArgs e)
		{
			if (LstFiles.SelectedItems.Count == 0)
			{
				MessageBox.Show("No files selected.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			FbdExtractPack.Description = "Select folder to extract files to.";
			FbdExtractPack.ShowNewFolderButton = true;

			if (FbdExtractPack.ShowDialog() != DialogResult.OK)
				return;

			var extractPath = FbdExtractPack.SelectedPath;

			if (!Directory.Exists(extractPath))
			{
				MessageBox.Show("Directory not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			// Create IPF-named folder
			var ipfFolderName = Path.GetFileNameWithoutExtension(_openedIpf.FilePath);
			var ipfExtractPath = Path.Combine(extractPath, ipfFolderName);
			Directory.CreateDirectory(ipfExtractPath);

			var progressDlg = new FrmProgress(LstFiles.SelectedItems.Count);
			progressDlg.StartPosition = FormStartPosition.CenterScreen;
			progressDlg.TopMost = true;
			progressDlg.Show(this);
			progressDlg.BringToFront();
			Application.DoEvents();

			int extractedCount = 0;
			foreach (ListViewItem item in LstFiles.SelectedItems)
			{
				if (progressDlg.Cancel)
					break;

				var fileTag = item.Tag.ToString();
				var fileName = item.Text;
				try
				{
					IpfFile ipfFile = null;
					if (fileTag.StartsWith("IPF") && fileTag.Contains(":"))
					{
						if (!_additionalFiles.TryGetValue(fileTag, out ipfFile))
							continue;
					}
					else if (!_files.TryGetValue(fileTag, out ipfFile))
					{
						continue;
					}

					// Create directories as needed
					var outputPath = Path.Combine(ipfExtractPath, ipfFile.Path);
					var outputDir = Path.GetDirectoryName(outputPath);
					Directory.CreateDirectory(outputDir);

					// Extract file
					var fileData = ipfFile.GetData();
					File.WriteAllBytes(outputPath, fileData);
					extractedCount++;
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Error extracting {fileName}: {ex.Message}", Text,
						MessageBoxButtons.OK, MessageBoxIcon.Error);
				}

				progressDlg.UpdateProgress(extractedCount);
				Application.DoEvents();
			}

			progressDlg.Close();
			MessageBox.Show($"Extracted {extractedCount} file(s) successfully to '{ipfExtractPath}'.", Text,
				MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		/// <summary>
		/// Called when clicking Extract Client button, extracts selected
		/// TOS client to selected destination.
		/// </summary>
		/// <remarks>
		/// Loads data first, followed by patch, to get the latest version
		/// of all files found.
		/// </remarks>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtnExtractClient_Click(object sender, EventArgs e)
		{
			FbdExtractPack.Description = "Select TOS folder.";
			FbdExtractPack.ShowNewFolderButton = false;

			if (FbdExtractPack.ShowDialog() != DialogResult.OK)
				return;

			var tosPath = FbdExtractPack.SelectedPath;
			var dataPath = Path.Combine(tosPath, "data");
			var patchPath = Path.Combine(tosPath, "patch");
			var releasePath = Path.Combine(tosPath, "release");

			if (!Directory.Exists(dataPath) || !Directory.Exists(patchPath) || !Directory.Exists(releasePath))
			{
				MessageBox.Show("Please select the TOS folder that contains 'data', 'patch', and 'release'.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			FbdExtractPack.Description = "Select folder to extract to.";
			FbdExtractPack.ShowNewFolderButton = true;

			if (FbdExtractPack.ShowDialog() != DialogResult.OK)
				return;

			var extractPath = FbdExtractPack.SelectedPath;

			if (!Directory.Exists(tosPath))
			{
				MessageBox.Show("Directory not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var col = new IpfCollection(tosPath);
			ExtractFiles(col.Files.Values, extractPath);
		}

		/// <summary>
		/// Called when clicking preview button, toggles preview panel.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtnPreview_Click(object sender, EventArgs e)
		{
			Properties.Settings.Default.Preview = BtnPreview.Checked;
			SplFiles.Panel2Collapsed = !Properties.Settings.Default.Preview;

			if (BtnPreview.Checked)
				Preview();
		}

		/// <summary>
		/// Called when program is closed, saves settings and warns about unsaved changes.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
		{
			// Check for unsaved changes
			if (_pendingChanges.Count > 0 || _isEditingIes)
			{
				// Save current editing state
				if (_isEditingIes && !string.IsNullOrEmpty(_currentIesFilePath))
				{
					_pendingChanges[_currentIesFilePath] = TxtPreview.Text;
				}

				if (_pendingChanges.Count > 0)
				{
					var result = MessageBox.Show(
						$"You have unsaved changes to {_pendingChanges.Count} file(s).\n\nDo you want to save before closing?",
						Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

					if (result == DialogResult.Yes)
					{
						// Show save menu - but for now just cancel close and let user save manually
						e.Cancel = true;
						MnuSave_Click(null, null);
						return;
					}
					else if (result == DialogResult.Cancel)
					{
						e.Cancel = true;
						return;
					}
					// DialogResult.No - continue closing without saving
				}
			}

			Properties.Settings.Default.Save();
		}

		/// <summary>
		/// Extracts given files to extractPath.
		/// </summary>
		/// <param name="ipfFiles"></param>
		/// <param name="extractPath"></param>
		private void ExtractFiles(IEnumerable<IpfFile> ipfFiles, string extractPath)
		{
			// Warm up
			var timer = Stopwatch.StartNew();

			var count = ipfFiles.Count();
			var frmProgress = new FrmProgress(count);

			// Run in thread, so it doesn't block the UI thread
			ThreadPool.QueueUserWorkItem(state =>
			{
				// Actually start timer
				timer.Restart();

				var canceled = false;

				// Extract files in parallel for performance
				var i = 0;
				Parallel.ForEach(ipfFiles, ipfFile =>
				{
					// Just return if cancel was clicked, this way we get
					// to after the ForEach in an instant, even if it
					// technically doesn't `break;`.
					if (canceled = frmProgress.Cancel)
						return;

					var filePath = Path.Combine(extractPath, ipfFile.FullPath);

					// Create folder if it doesn't exist yet
					var parent = Path.GetDirectoryName(filePath);
					if (!Directory.Exists(parent))
						Directory.CreateDirectory(parent);

					// Extract file
					var data = ipfFile.GetData();
					File.WriteAllBytes(filePath, data);

					// Update progress bar
					Invoke((MethodInvoker)delegate
					{
						if (frmProgress.Handle != IntPtr.Zero)
							frmProgress.UpdateProgress(++i);
					});
				});

				// Stop timer
				timer.Stop();

				// Close progress window and show result
				Invoke((MethodInvoker)delegate
				{
					frmProgress.Close();
					MessageBox.Show(canceled ? "Canceled." : "Done (" + timer.Elapsed + ").", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				});
			});

			// Show progress window after the thread was started, as it
			// blocks the main window.
			frmProgress.ShowDialog();
		}

		#region IES Editing

		/// <summary>
		/// Updates the UI to reflect pending changes count.
		/// </summary>
		private void UpdatePendingChangesUI()
		{
			int totalChanges = _pendingChanges.Count + _importedFiles.Count + _deletedFiles.Count;
			
			if (totalChanges > 0)
			{
				// Build a description of changes for the menu
				var parts = new List<string>();
				if (_pendingChanges.Count > 0) parts.Add($"{_pendingChanges.Count} modified");
				if (_importedFiles.Count > 0) parts.Add($"{_importedFiles.Count} imported");
				if (_deletedFiles.Count > 0) parts.Add($"{_deletedFiles.Count} deleted");
				
				MnuSave.Text = $"Save ({string.Join(", ", parts)})";
				MnuSave.Enabled = true;
				MnuSaveAs.Enabled = true;
				BtnCancelEdit.Enabled = true;
			}
			else
			{
				MnuSave.Text = "Save";
				MnuSave.Enabled = false;
				MnuSaveAs.Enabled = false;
				BtnCancelEdit.Enabled = _isEditingIes;
			}
		}

		/// <summary>
		/// Shows the IES editor with the given XML content.
		/// </summary>
		private void ShowIesEditor(string filePath, string xml)
		{
			_currentIesFilePath = filePath;
			_currentIesXml = xml;
			_isEditingIes = true;

			ResetPreview();
			SetTextPreviewStyle(ScintillaNET.Lexer.Xml);

			TxtPreview.ReadOnly = false;
			TxtPreview.Text = xml;
			TxtPreview.Visible = true;
			
			// Subscribe to text changes for real-time indicator updates
			TxtPreview.TextChanged -= TxtPreview_TextChanged;
			TxtPreview.TextChanged += TxtPreview_TextChanged;

			BtnEditIes.Enabled = false;
			UpdatePendingChangesUI();

			// Show indicator that this file has pending changes
			var hasChanges = _pendingChanges.ContainsKey(filePath);
			LblFileName.Text = filePath + (hasChanges ? " (Modified)" : " (Editing)");

			// Focus the text editor so user can start typing immediately
			TxtPreview.Focus();
		}

		/// <summary>
		/// Called when text changes in the IES editor. Updates file list indicator in real-time.
		/// </summary>
		private void TxtPreview_TextChanged(object sender, EventArgs e)
		{
			if (!_isEditingIes || string.IsNullOrEmpty(_currentIesFilePath))
				return;

			// If the current text equals the original loaded content, remove pending change
			var currentText = TxtPreview.Text ?? string.Empty;
			var originalText = _currentIesXml ?? string.Empty;

			if (currentText.Equals(originalText, StringComparison.Ordinal))
			{
				if (_pendingChanges.ContainsKey(_currentIesFilePath))
				{
					_pendingChanges.Remove(_currentIesFilePath);
					UpdateFileListIndicator(_currentIesFilePath);
				}
			}
			else
			{
				// Store the current edit to pending changes
				_pendingChanges[_currentIesFilePath] = currentText;
				UpdateFileListIndicator(_currentIesFilePath);
			}

			// Update UI status
			UpdatePendingChangesUI();
		}

		/// <summary>
		/// Updates the visual indicator (*, +, ×) for a specific file in the file list.
		/// </summary>
		private void UpdateFileListIndicator(string filePath)
		{
			// Find the item in the file list
			foreach (ListViewItem item in LstFiles.Items)
			{
				if ((string)item.Tag == filePath)
				{
					// Get the file name without any existing indicator
					var displayText = Path.GetFileName(filePath);

					// Determine the new display text with indicator
					string newDisplayText = displayText;
					if (_deletedFiles.Contains(filePath))
						newDisplayText = "✕ " + displayText;
					else if (_pendingChanges.ContainsKey(filePath))
						newDisplayText = "* " + displayText;
					else if (_importedFiles.ContainsKey(filePath))
						newDisplayText = "+ " + displayText;

					// Update if text changed
					if (item.Text != newDisplayText)
					{
						item.Text = newDisplayText;

						// Update color
						if (_deletedFiles.Contains(filePath))
							item.ForeColor = Color.Red;
						else if (_pendingChanges.ContainsKey(filePath))
							item.ForeColor = Color.DarkOrange;
						else if (_importedFiles.ContainsKey(filePath))
							item.ForeColor = Color.Green;
						else
							item.ForeColor = SystemColors.WindowText;
					}
					break;
				}
			}
		}

		/// <summary>
		/// Double-click handler for files list - open editor if file is editable.
		/// </summary>
		private void LstFiles_DoubleClick(object sender, EventArgs e)
		{
			if (LstFiles.SelectedItems.Count == 0)
				return;

			var item = LstFiles.SelectedItems[0];
			var filePath = (string)item.Tag;
			var ext = Path.GetExtension(filePath).ToLowerInvariant();

			// If it's an IES specifically, keep existing flow
			if (ext == ".ies")
			{
				BtnEditIes_Click(this, EventArgs.Empty);
				return;
			}

			// If this is a text-like format, open generic text editor
			FileFormat fileType;
			if (_fileTypes.TryGetValue(ext, out fileType) && fileType.PreviewType == PreviewType.Text)
			{
				OpenTextEditorForFile(filePath);
			}
		}

        

		/// <summary>
		/// Discards changes for the currently selected file (used by context menu).
		/// </summary>
		private void DiscardChangesForSelectedFile()
		{
			if (LstFiles.SelectedItems.Count == 0)
				return;

			var item = LstFiles.SelectedItems[0];
			var filePath = (string)item.Tag;

			// If this is the currently edited file, set current path so existing method works
			_currentIesFilePath = filePath;
			_isEditingIes = _pendingChanges.ContainsKey(filePath) || _isEditingIes;

			DiscardCurrentChanges();
		}

		/// <summary>
		/// Opens a generic text editor for a non-IES text-like file.
		/// Loads content into TxtPreview and enables live pending-change tracking.
		/// </summary>
		private void OpenTextEditorForFile(string filePath)
		{
			byte[] data = null;

			// Imported file?
			if (_importedFiles.TryGetValue(filePath, out var imported))
			{
				data = imported;
			}
			else if (filePath.StartsWith("IPF") && filePath.Contains(":"))
			{
				if (_additionalFiles.TryGetValue(filePath, out var addFile))
					data = addFile.GetData();
				else
					return;
			}
			else if (_files.TryGetValue(filePath, out var ipfFile))
			{
				data = ipfFile.GetData();
			}
			else
			{
				return;
			}

			string text;
			try { text = System.Text.Encoding.UTF8.GetString(data); }
			catch { text = System.Text.Encoding.Default.GetString(data); }

			// Prepare editor
			_currentIesFilePath = filePath;
			_currentIesXml = text;
			_isEditingIes = true;

			ResetPreview();
			SetTextPreviewStyle(ScintillaNET.Lexer.Null);

			TxtPreview.ReadOnly = false;
			TxtPreview.Text = text;
			TxtPreview.Visible = true;

			TxtPreview.TextChanged -= TxtPreview_TextChanged;
			TxtPreview.TextChanged += TxtPreview_TextChanged;

			BtnEditIes.Enabled = false;
			UpdatePendingChangesUI();

			LblFileName.Text = filePath + (_pendingChanges.ContainsKey(filePath) ? " (Modified)" : " (Editing)");

			TxtPreview.Focus();
		}

		/// <summary>
		/// Called when clicking Edit IES button, opens IES as XML for editing.
		/// </summary>
		private void BtnEditIes_Click(object sender, EventArgs e)
		{
			if (LstFiles.SelectedIndices.Count == 0)
				return;

			var selected = LstFiles.SelectedItems[0];
			var fileName = (string)selected.Tag;
			var ext = Path.GetExtension(fileName).ToLowerInvariant();

			if (ext != ".ies")
				return;

			// Check if we already have pending changes for this file
			if (_pendingChanges.ContainsKey(fileName))
			{
				// Load original xml and show editor preserving pending edits
				var originalXml = LoadOriginalIesXml(fileName);
				if (originalXml == null)
				{
					ShowIesEditor(fileName, _pendingChanges[fileName]);
					return;
				}
				ShowIesEditor(fileName, originalXml);
				TxtPreview.Text = _pendingChanges[fileName];
				return;
			}

			byte[] iesData;
			
			// Check if this is an imported file
			if (_importedFiles.TryGetValue(fileName, out var importedData))
			{
				iesData = importedData;
			}
			// Check if from additional IPF
			else if (fileName.StartsWith("IPF") && fileName.Contains(":"))
			{
				if (_additionalFiles.TryGetValue(fileName, out var additionalFile))
				{
					iesData = additionalFile.GetData();
				}
				else
				{
					MessageBox.Show("File not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
					return;
				}
			}
			// Normal IPF file
			else if (_files.TryGetValue(fileName, out var ipfFile))
			{
				iesData = ipfFile.GetData();
			}
			else
			{
				MessageBox.Show("File not found.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			try
			{
				var iesFile = new IesFile(iesData);
				var xml = iesFile.GetXml();

				ShowIesEditor(fileName, xml);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to load IES file: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Loads the original IES XML for the given file path (from IPF/additional/imported),
		/// or returns null on failure.
		/// </summary>
		private string LoadOriginalIesXml(string fileName)
		{
			try
			{
				byte[] iesData = null;
				if (_importedFiles.TryGetValue(fileName, out var importedData))
				{
					iesData = importedData;
				}
				else if (fileName.StartsWith("IPF") && fileName.Contains(":"))
				{
					if (_additionalFiles.TryGetValue(fileName, out var additionalFile))
						iesData = additionalFile.GetData();
					else
						return null;
				}
				else if (_files.TryGetValue(fileName, out var ipfFile))
				{
					iesData = ipfFile.GetData();
				}
				else
				{
					return null;
				}

				var iesFile = new IesFile(iesData);
				return iesFile.GetXml();
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Save menu click - saves to IPF (overwrite)
		/// </summary>
		private void MnuSave_Click(object sender, EventArgs e)
		{
			// Save current editing state first
			if (_isEditingIes && !string.IsNullOrEmpty(_currentIesFilePath))
			{
				_pendingChanges[_currentIesFilePath] = TxtPreview.Text;
			}

			if (_pendingChanges.Count == 0 && _importedFiles.Count == 0 && _deletedFiles.Count == 0)
			{
				MessageBox.Show("No changes to save.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			SaveAllToIpf(true);
		}

		/// <summary>
		/// Save As menu click - saves to new IPF
		/// </summary>
		private void MnuSaveAs_Click(object sender, EventArgs e)
		{
			// Save current editing state first
			if (_isEditingIes && !string.IsNullOrEmpty(_currentIesFilePath))
			{
				_pendingChanges[_currentIesFilePath] = TxtPreview.Text;
			}

			if (_pendingChanges.Count == 0 && _importedFiles.Count == 0 && _deletedFiles.Count == 0)
			{
				MessageBox.Show("No changes to save.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			SaveAllToIpf(false);
		}

		/// <summary>
		/// Import File menu click - imports files into the current IPF
		/// </summary>
		private void MnuImportFile_Click(object sender, EventArgs e)
		{
			ImportIesFile_Click(sender, e);
		}

		/// <summary>
		/// Discard Changes menu click - discards all pending changes
		/// </summary>
		private void MnuDiscardChanges_Click(object sender, EventArgs e)
		{
			int total = _pendingChanges.Count + _importedFiles.Count + _deletedFiles.Count;
			if (total == 0)
			{
				MessageBox.Show("No changes to discard.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			var result = MessageBox.Show(
				$"Discard ALL pending changes?\n\n" +
				$"• {_pendingChanges.Count} modified file(s)\n" +
				$"• {_importedFiles.Count} imported file(s)\n" +
				$"• {_deletedFiles.Count} deleted file(s)\n\n" +
				"This cannot be undone.",
				Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

			if (result != DialogResult.Yes)
				return;

			// Remove imported file nodes from tree
			RemoveImportedNodesFromTree();

			_pendingChanges.Clear();
			_importedFiles.Clear();
			_deletedFiles.Clear();
			_isEditingIes = false;
			_currentIesFilePath = null;

			UpdatePendingChangesUI();

			// Refresh file list to remove strikethrough/coloring
			if (TreeFolders.SelectedNode != null)
				TreeFolders_AfterSelect(TreeFolders, new TreeViewEventArgs(TreeFolders.SelectedNode));

			// Refresh preview
			ResetPreview();
			if (BtnPreview.Checked)
				Preview();

			if (_openedIpf != null)
				LblFileName.Text = _openedIpf.FilePath;
		}

		/// <summary>
		/// Save the edited IES as an IES binary file.
		/// </summary>
		private void SaveAsIesFile()
		{
			if (string.IsNullOrEmpty(_currentIesFilePath))
				return;

			try
			{
				// Get current XML (either from editor or pending changes)
				var xml = _isEditingIes ? TxtPreview.Text : _pendingChanges[_currentIesFilePath];
				var newIesFile = IesFile.LoadFromXmlString(xml);

				// Get IES binary data
				var iesBytes = newIesFile.SaveToBytes();

				// Show save dialog
				var fileName = Path.GetFileName(_currentIesFilePath);
				SavExtractFile.FileName = fileName;
				SavExtractFile.Filter = "IES files|*.ies|All files|*.*";

				if (SavExtractFile.ShowDialog() != DialogResult.OK)
					return;

				var savePath = SavExtractFile.FileName;
				File.WriteAllBytes(savePath, iesBytes);

				MessageBox.Show("IES file saved successfully!", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to save IES file: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Save the edited IES as an XML file.
		/// </summary>
		private void SaveAsXmlFile()
		{
			if (string.IsNullOrEmpty(_currentIesFilePath))
				return;

			try
			{
				var xml = _isEditingIes ? TxtPreview.Text : _pendingChanges[_currentIesFilePath];

				// Show save dialog
				var fileName = Path.GetFileNameWithoutExtension(_currentIesFilePath) + ".xml";
				SavExtractFile.FileName = fileName;
				SavExtractFile.Filter = "XML files|*.xml|All files|*.*";

				if (SavExtractFile.ShowDialog() != DialogResult.OK)
					return;

				var savePath = SavExtractFile.FileName;
				File.WriteAllText(savePath, xml, Encoding.UTF8);

				MessageBox.Show("XML file saved successfully!", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to save XML file: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Save all pending IES changes into an IPF file.
		/// </summary>
		private void SaveAllToIpf(bool overwrite)
		{
			if (_openedIpf == null)
			{
				MessageBox.Show("No IPF file is currently open.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
				return;
			}

			if (_pendingChanges.Count == 0 && _importedFiles.Count == 0 && _deletedFiles.Count == 0)
			{
				MessageBox.Show("No pending changes, imported files, or deletions to save.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}

			try
			{
				// Convert all pending XML changes to IES binary data
				var updatedFiles = new Dictionary<string, byte[]>();
				foreach (var kvp in _pendingChanges)
				{
					var iesFile = IesFile.LoadFromXmlString(kvp.Value);
					updatedFiles[kvp.Key] = iesFile.SaveToBytes();
				}

				string savePath;

				if (overwrite)
				{
					// Confirm overwrite
					var result = MessageBox.Show(
						$"This will overwrite the current IPF file:\n{_openedIpf.FilePath}\n\nAre you sure?",
						Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
					
					if (result != DialogResult.Yes)
						return;

					// Create temp file, then replace original
					savePath = _openedIpf.FilePath + ".tmp";
				}
				else
				{
					// Show save dialog for new IPF
					var originalIpfName = Path.GetFileName(_openedIpf.FilePath);
					var suggestedName = Path.GetFileNameWithoutExtension(originalIpfName) + "_modified.ipf";
					
					SavExtractFile.FileName = suggestedName;
					SavExtractFile.Filter = "IPF files|*.ipf|All files|*.*";

					if (SavExtractFile.ShowDialog() != DialogResult.OK)
						return;

					savePath = SavExtractFile.FileName;

					// Can't save over the currently open file when using "Save As"
					if (Path.GetFullPath(savePath).Equals(Path.GetFullPath(_openedIpf.FilePath), StringComparison.OrdinalIgnoreCase))
					{
						MessageBox.Show("Cannot overwrite the currently open IPF file using 'Save As'. Use 'Save (overwrite)' instead.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
						return;
					}
				}

				// Save IPF with updated files, new imported files, and deleted files
				_openedIpf.SaveWithUpdatedAndNewFiles(savePath, updatedFiles, 
					_importedFiles.Count > 0 ? _importedFiles : null,
					_deletedFiles.Count > 0 ? _deletedFiles : null);

				int totalUpdated = updatedFiles.Count;
				int totalImported = _importedFiles.Count;
				int totalDeleted = _deletedFiles.Count;

				if (overwrite)
				{
					// Close current IPF, replace with temp, reopen
					var originalPath = _openedIpf.FilePath;
					_openedIpf.Close();
					
					// Replace original with temp
					File.Delete(originalPath);
					File.Move(savePath, originalPath);

					// Clear pending changes, imported files, and deletions, then reopen
					_pendingChanges.Clear();
					_importedFiles.Clear();
					_deletedFiles.Clear();
					_isEditingIes = false;
					_currentIesFilePath = null;
					
					Open(originalPath);
					
					var msg = $"IPF file saved successfully!\n\n";
					if (totalUpdated > 0) msg += $"{totalUpdated} file(s) were updated.\n";
					if (totalImported > 0) msg += $"{totalImported} file(s) were imported.\n";
					if (totalDeleted > 0) msg += $"{totalDeleted} file(s) were deleted.";
					MessageBox.Show(msg.TrimEnd(), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				else
				{
					var msg = $"IPF file saved successfully!\n\n";
					if (totalUpdated > 0) msg += $"{totalUpdated} file(s) were updated.\n";
					if (totalImported > 0) msg += $"{totalImported} file(s) were imported.\n";
					if (totalDeleted > 0) msg += $"{totalDeleted} file(s) were deleted.";
					MessageBox.Show(msg.TrimEnd(), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

					// Ask if user wants to open the new IPF
					var result = MessageBox.Show("Do you want to open the newly created IPF file?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
					if (result == DialogResult.Yes)
					{
						_pendingChanges.Clear();
						_importedFiles.Clear();
						_deletedFiles.Clear();
						_isEditingIes = false;
						_currentIesFilePath = null;
						_openedIpf.Close();
						Open(savePath);
					}
				}

				UpdatePendingChangesUI();
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed to save IPF file: " + ex.Message + "\n\n" + ex.StackTrace, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Called when clicking Cancel button, shows options for canceling.
		/// </summary>
		private void BtnCancelEdit_Click(object sender, EventArgs e)
		{
			if (_pendingChanges.Count == 0 && _importedFiles.Count == 0 && !_isEditingIes)
				return;

			// Create context menu with cancel options
			var menu = new ContextMenuStrip();

			if (_isEditingIes && !string.IsNullOrEmpty(_currentIesFilePath))
			{
				menu.Items.Add("Discard current file changes", null, (s, ev) => DiscardCurrentChanges());
			}

			if (_pendingChanges.Count > 0)
			{
				menu.Items.Add($"Discard ALL modified files ({_pendingChanges.Count})", null, (s, ev) => DiscardAllChanges());
			}

			if (_importedFiles.Count > 0)
			{
				menu.Items.Add($"Discard ALL imported files ({_importedFiles.Count})", null, (s, ev) => DiscardAllImports());
			}

			if (_pendingChanges.Count > 0 || _importedFiles.Count > 0)
			{
				menu.Items.Add(new ToolStripSeparator());
				menu.Items.Add("Discard EVERYTHING", null, (s, ev) => DiscardEverything());
			}

			// Show menu below the button
			var btn = BtnCancelEdit;
			menu.Show(toolStrip1, btn.Bounds.Left, btn.Bounds.Bottom);
		}

		/// <summary>
		/// Discards changes for the current file only.
		/// </summary>
		private void DiscardCurrentChanges()
		{
			if (string.IsNullOrEmpty(_currentIesFilePath))
				return;

			// Check if this is an imported file (not yet in the original IPF)
			bool isImportedFile = _importedFiles.ContainsKey(_currentIesFilePath);
			
			string message = isImportedFile
				? $"Discard imported file '{Path.GetFileName(_currentIesFilePath)}'?\n\nThis will remove the file completely."
				: $"Discard changes to '{Path.GetFileName(_currentIesFilePath)}'?";

			var result = MessageBox.Show(message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
			if (result != DialogResult.Yes)
				return;

		_pendingChanges.Remove(_currentIesFilePath);
		
		// If it's an imported file, also remove from imports and tree
		if (isImportedFile)
		{
			_importedFiles.Remove(_currentIesFilePath);
			RemoveImportedFileFromTree(_currentIesFilePath);
		}
		
		_isEditingIes = false;
		_currentIesFilePath = null;

		UpdatePendingChangesUI();

		// Refresh file list - directly update the file item to remove the indicator
		foreach (ListViewItem item in LstFiles.Items)
		{
			var filePath = (string)item.Tag;
			if (_deletedFiles.Contains(filePath))
			{
				item.Text = "✕ " + Path.GetFileName(filePath);
				item.ForeColor = Color.Red;
			}
			else if (_pendingChanges.ContainsKey(filePath))
			{
				item.Text = "* " + Path.GetFileName(filePath);
				item.ForeColor = Color.DarkOrange;
			}
			else if (_importedFiles.ContainsKey(filePath))
			{
				item.Text = "+ " + Path.GetFileName(filePath);
				item.ForeColor = Color.Green;
			}
			else
			{
				item.Text = Path.GetFileName(filePath);
				item.ForeColor = SystemColors.WindowText;
			}
		}

		// Also refresh via TreeFolders in case needed
		if (TreeFolders.SelectedNode != null)
			TreeFolders_AfterSelect(TreeFolders, new TreeViewEventArgs(TreeFolders.SelectedNode));			ResetPreview();
			if (BtnPreview.Checked)
				Preview();
			
			if (_openedIpf != null)
				LblFileName.Text = _openedIpf.FilePath;
		}

		/// <summary>
		/// Discards all pending changes.
		/// </summary>
		private void DiscardAllChanges()
		{
			var result = MessageBox.Show($"Discard ALL pending changes ({_pendingChanges.Count} file(s))?\n\nThis cannot be undone.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			if (result != DialogResult.Yes)
				return;

			_pendingChanges.Clear();
			_isEditingIes = false;
			_currentIesFilePath = null;

			UpdatePendingChangesUI();

		// Refresh preview
		ResetPreview();
		if (BtnPreview.Checked)
			Preview();
			
			if (_openedIpf != null)
				LblFileName.Text = _openedIpf.FilePath;
		}

		/// <summary>
		/// Discards all imported files.
		/// </summary>
		private void DiscardAllImports()
		{
			var result = MessageBox.Show($"Discard ALL imported files ({_importedFiles.Count})?\n\nThis cannot be undone.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			if (result != DialogResult.Yes)
				return;

			// Remove imported file nodes from tree
			RemoveImportedNodesFromTree();
			
			_importedFiles.Clear();
			UpdatePendingChangesUI();
		}

		/// <summary>
		/// Discards everything - all changes and imports.
		/// </summary>
		private void DiscardEverything()
		{
			int total = _pendingChanges.Count + _importedFiles.Count;
			var result = MessageBox.Show($"Discard ALL changes and imports ({total} total)?\n\nThis cannot be undone.", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			if (result != DialogResult.Yes)
				return;

			// Remove imported file nodes from tree
			RemoveImportedNodesFromTree();

			_pendingChanges.Clear();
			_importedFiles.Clear();
			_isEditingIes = false;
			_currentIesFilePath = null;

			UpdatePendingChangesUI();

			// Refresh preview
			ResetPreview();
			if (BtnPreview.Checked)
				Preview();
			
			if (_openedIpf != null)
				LblFileName.Text = _openedIpf.FilePath;
		}

		/// <summary>
		/// Removes imported file nodes from the tree view.
		/// </summary>
		private void RemoveImportedNodesFromTree()
		{
			var nodesToRemove = new List<TreeNode>();
			FindImportedNodes(TreeFolders.Nodes, nodesToRemove);

			foreach (var node in nodesToRemove)
			{
				node.Remove();
			}

			// Clean up folder entries
			foreach (var path in _importedFiles.Keys)
			{
				var parts = path.Split('/');
				var folderPath = string.Join("/", parts.Take(parts.Length - 1));
				if (_folders.ContainsKey(folderPath))
				{
					_folders[folderPath].Remove(path);
				}
			}
		}

		/// <summary>
		/// Recursively finds nodes marked as imported.
		/// </summary>
		private void FindImportedNodes(TreeNodeCollection nodes, List<TreeNode> results)
		{
			foreach (TreeNode node in nodes)
			{
				var tag = node.Tag?.ToString() ?? "";
				if (tag.StartsWith("IMPORTED:"))
				{
					results.Add(node);
				}
				else
				{
					FindImportedNodes(node.Nodes, results);
				}
			}
		}

		/// <summary>
		/// Removes a single imported file from the tree view.
		/// </summary>
		private void RemoveImportedFileFromTree(string filePath)
		{
			var nodesToRemove = new List<TreeNode>();
			FindImportedNodeByPath(TreeFolders.Nodes, filePath, nodesToRemove);

			foreach (var node in nodesToRemove)
			{
				node.Remove();
			}

			// Clean up folder entry
			var parts = filePath.Split('/');
			var folderPath = string.Join("/", parts.Take(parts.Length - 1));
			if (_folders.ContainsKey(folderPath + "/"))
			{
				_folders[folderPath + "/"].Remove(filePath);
			}
		}

		/// <summary>
		/// Recursively finds a specific imported node by path.
		/// </summary>
		private void FindImportedNodeByPath(TreeNodeCollection nodes, string filePath, List<TreeNode> results)
		{
			foreach (TreeNode node in nodes)
			{
				var tag = node.Tag?.ToString() ?? "";
				if (tag == "IMPORTED:" + filePath)
				{
					results.Add(node);
				}
				else
				{
					FindImportedNodeByPath(node.Nodes, filePath, results);
				}
			}
		}

		#endregion

		// Event handler for Save Session menu item
		private void SaveSession_Click(object sender, EventArgs e)
		{
			// TODO: Implement session save logic
			MessageBox.Show("Save Session clicked. (Not yet implemented)", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		// Event handler for Load Session menu item
		private void LoadSession_Click(object sender, EventArgs e)
		{
			// TODO: Implement session load logic
			MessageBox.Show("Load Session clicked. (Not yet implemented)", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
	}
}
