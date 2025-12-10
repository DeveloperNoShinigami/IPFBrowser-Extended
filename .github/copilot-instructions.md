# IPF Browser Extended - AI Coding Agent Instructions

**Framework:** Inspired by [BMad Method](https://github.com/bmad-code-org/BMAD-METHOD) - Scale-adaptive problem-solving with structured analysis, design, and validation phases.

## Project Overview
IPF Browser Extended is a C# Windows Forms application for viewing, editing, and managing IPF (interactive package file) archives from Tree of Savior. The app extends basic IPF browsing with advanced features: file editing (IES database XML), texture/model preview, and a 3D viewer for XAC model files with skeletal animations (XSM).

## Architecture & Component Patterns

### Core Components
- **FrmMain.cs** (3690 lines) - Main UI form; orchestrates all features. Holds state for:
  - `_openedIpf` - Currently loaded IPF archive
  - `_folders` / `_files` - Directory tree and file dictionary from IPF
  - `_pendingChanges` (Dictionary<string, string>) - Modified IES XML before save
  - `_importedFiles` (Dictionary<string, byte[]>) - New files staged for IPF
  - `_deletedFiles` (HashSet<string>) - Files marked for removal
  - `_additionalIpfs` - Auxiliary IPFs loaded for cross-referencing
  - `_modelViewer` - 3D viewer control instance

- **FileFormats/** - Modular format parsers:
  - `IPF/Ipf.cs` - IPF archive reader (supports iCBT1/2, kTOS, iTOS, current versions)
  - `IPF/IpfCollection.cs` - TOS client multi-IPF loader (data/patch/release folders)
  - `XAC/XacFile.cs` - EMotionFX 3D actor model parser (~644 lines; handles meshes, materials, skeletal data)
  - `XSM/XsmFile.cs` - EMotionFX skeletal animation parser
  - `IES/IesFile.cs` - Database table editor (XML conversion)
  - `DDS/DDSImage.cs`, `TGA/TargaImage.cs` - Texture readers
  - `FileFormat.cs` - Maps extensions → preview types (Text, Image, DdsImage, Model3D, etc.)

- **Viewer3D/ModelViewerControl.cs** (2650 lines) - OpenGL-based 3D viewer:
  - Uses OpenTK (net20 build) for rendering
  - `RenderMesh` - OpenGL vertex/index buffers for models
  - Camera: orbit rotation, pan, zoom; toggleable lock via right-click
  - Texture pipeline: loads DDS from IPF or file system
  - Supports attachments/multi-mesh with bone transforms
  - Debug logging to `ErrorLogs/model_debug.log`

### Data Flow: Edit → Save
1. User edits IES as XML (FrmMain.cs) → stored in `_pendingChanges`
2. User imports files → stored in `_importedFiles`
3. User marks files for deletion → added to `_deletedFiles`
4. Save (Ctrl+S) or SaveAs (Ctrl+Shift+S) writes all changes back to IPF

### File Type Registration
In `FrmMain` constructor, extensions are mapped to `FileFormat` objects with preview types:
```csharp
_fileTypes[".ies"] = new FileFormat("table.png", PreviewType.IesTable);
_fileTypes[".lua"] = new FileFormat("page_white_code.png", PreviewType.Text, Lexer.Lua);
_fileTypes[".xac"] = new FileFormat("model.png", PreviewType.Model3D);
```
Preview rendering logic is in `ShowPreview()` method (~1000s of lines); dispatch by `PreviewType` enum.

## Build & Development

### Prerequisites for Developers
- **Windows 7 or later** (Windows 10/11 recommended)
- **.NET Framework 4.5+** - Check: Settings → Apps → Apps & features → "Turn Windows features on or off"
- **Visual Studio 2022** or **Visual Studio 2019 Update 16.10+** (with C# workload)
  - Alternative: **dotnet CLI 6.0+** for command-line builds
- **Git** for cloning the repository

### Build System
- **Solution:** `IPFBrowser.sln` (Visual Studio 2022 compatible)
- **Framework:** .NET Framework 4.5
- **Build tasks available:**
  - `dotnet build IPFBrowser.sln --configuration Debug` (preferred)
  - `msbuild IPFBrowser.sln /p:Configuration=Debug` (legacy)
- **Convenience scripts included:**
  - `SETUP.bat` - Interactive setup menu (Windows Command Prompt)
  - `SETUP.ps1` - Interactive setup script (PowerShell)
- **Key project settings:**
  - `AllowUnsafeBlocks=true` - Unsafe pointers used for graphics/binary parsing
  - `GenerateResourceUsePreserializedResources=true` - Required for resource handling
  - Output: `bin\Debug\IPF Browser.exe`

### Quick Start for Developers
```bash
# Clone and setup
git clone https://github.com/DeveloperNoShinigami/IPFBrowser-Extended.git
cd IPFBrowser-Extended

# Build & run (one command)
dotnet build IPFBrowser.sln --configuration Debug
cd IPFBrowser/bin/Debug
"IPF Browser.exe"
```

Or use the included scripts:
```bash
# Windows Command Prompt
SETUP.bat
# Then select option 1

# PowerShell
powershell -ExecutionPolicy Bypass -File SETUP.ps1
```

### Dependencies (NuGet + Local)
- **OpenTK** (`net20`) - OpenGL wrapper for 3D rendering
- **ScintillaNET** (`3.5.6`) - Syntax-highlighted text editor for code/XML preview
- **RibbonWinForms** - Ribbon UI control (in `Libs/`)
- **System.Resources.Extensions** (`4.7.0`) - Binary resource serialization
- **Ookii.Dialogs** - File dialogs (local in Libs/)

### Common Build Issues
- `System.Resources.Extensions` mismatch → ensure `GenerateResourceUsePreserializedResources=true` in .csproj
- OpenTK GLControl initialization → check `ModelViewerControl._glInitialized`; errors logged to `ErrorLogs/model_debug.log`
- Designer/resx conflicts → rebuild or clean `obj/Debug`

## Developer Workflows

### Adding File Format Support
1. Create new parser in `FileFormats/<FormatName>/` (e.g., `FileFormats/MyFormat/MyFormatFile.cs`)
2. Inherit from `FileFormat` base class or create standalone parser
3. In `FrmMain` constructor, register extension:
   ```csharp
   _fileTypes[".myext"] = new FileFormat("icon.png", PreviewType.YourType, Lexer.YourLexer);
   ```
4. Add preview logic to `ShowPreview()` method, dispatch on `PreviewType`
5. If binary format, ensure binary reader properly handles endianness (XAC/XSM patterns available as reference)

### Editing IES Files: Full Lifecycle
**Editing Flow:**
1. User selects IES → `ShowPreview()` calls `IesFile.Load()`
2. Parse binary: header (row/col counts) → columns → rows
3. Convert to XML via `ToXml()` for ScintillaNET display
4. Store XML in `_pendingChanges[fullPath]`
5. User edits in editor → track with `_currentIesXml`
6. Save: convert XML back to binary via `FromXml()`
7. Add binary to `_importedFiles[fullPath]` (marks for import on IPF save)
8. On final save, `SaveIPF()` repacks all changes into output IPF

**Common Edits:**
- Adding rows: serialize new row → insert in XML → binary conversion auto-handles offsets
- Changing column types: modify column definition in XML; validation occurs on FromXml()
- Bulk updates: use string regex on XML before conversion to binary

### Debugging 3D Viewer
- Enable wireframe: `_modelViewer.ShowWireframe = true`
- Check camera state: `_cameraPosition`, `_cameraYaw`, `_cameraPitch`
- Mesh bounds calculation: `_modelBounds`, `_modelSize` (used for auto-zoom)
- Texture loading: inspect `_textureIds` dictionary and `_materials` list
- View debug log: `ErrorLogs/model_debug.log` (created by `ModelViewerControl.Log()`)

### XAC File Format Deep Dive
**Binary Structure:** EMotionFX actor format with chunks (HEADER, MOTIONS, MATERIALS, MESHES, NODES, PROPERTIES)
- Each chunk has 12-byte header: magic (4B), size (4B), version (4B)
- Materials stored as array: diffuse/specular colors, texture names, shader info
- Meshes: vertices (float3 pos, float3 normal, float2 uv), indices (uint16/32), material ID references
- Nodes: skeleton/bone structure with parent indices, transforms
- Properties: key-value pairs for attachments, texture overrides, LOD data

**Texture Resolution:** `GetAllDiffuseTextures()` scans both parsed PROPERTIES chunks AND raw binary data (TOS-specific FX materials store DiffuseTex in non-standard format). Fallback scanning essential for complex models.

### XSM Animation Format
**Binary Structure:** Similar header to XAC; chunks contain motion data
- `MotionParts` list: bone name → keyframe data (position, rotation, scale per frame)
- Read as: chunk type → motion count → for each motion: bone name length, name, keyframe count, then per-frame transforms
- Used by ModelViewerControl to apply skeletal deformation to XAC meshes

### IES Table Editing Workflow
1. User selects IES file → `ShowPreview()` dispatches to `PreviewType.IesTable`
2. Parse binary with `IesFile.cs`: reads header (row/column counts), then column definitions, row data
3. Convert to XML via `IesFile.ToXml()` for editing in ScintillaNET
4. Store modified XML in `_pendingChanges[filePath]` (not file system)
5. On save, convert XML back to binary via `IesFile.FromXml()` → added to `_importedFiles` for repacking into IPF

### Texture Pipeline (DDS/TGA Loading)
1. Material references texture by name (e.g., "face_makeup.dds")
2. Lookup: check IPF `_additionalFiles` first (patches), then main `_files`
3. Extract binary data from IPF via `IpfFile.Extract()`
4. Load with `DDSImage.Load()` or `TargaImage.Load()` → Bitmap
5. Convert to OpenGL texture: `GL.GenTexture()`, bind, set pixels with `GL.TexImage2D()`
6. Store ID in `_textureIds[materialId]` for render-time binding
**Fallback:** If not in IPF, scan file system near IPF or in `textures/` subdirs

### Session Management (In Development)
- Planned `.session` JSON files to save 3D viewer state
- Methods `SaveSession_Click` and `LoadSession_Click` need implementation in FrmMain
- Must serialize: mesh positions, rotations, texture mappings, camera position
- TODO items in `TODO.md` track event handler placement errors

### Multiple IPF Support
- `_additionalIpfs` list stores auxiliary IPF instances
- Used for loading textures/models from patch IPFs when editing base data
- File lookup: first check `_additionalFiles`, fallback to main `_files`
- Useful for cross-referencing client data structure

## Project Conventions

### Code Organization
- Classes per file; namespace mirrors folder path (e.g., `FileFormats.IPF.Ipf`)
- Form designer files (`*.Designer.cs`) auto-generated; edit only in designer UI or via replace patterns
- Resource files (`.resx`) track images/icons; ScintillaNET lexers defined in code
- Large files (FrmMain, ModelViewerControl) organized with `#region` blocks

### Error Handling
- File operations wrapped in try-catch; errors written to `ErrorLogs/` with timestamps
- `Program.cs` registers global exception handler on startup
- 3D rendering errors non-fatal; logged but fallback to wireframe or plain display

### UI State Management
- Color-coded file list indicators:
  - Blue `*` = Modified IES
  - Green `+` = Imported file
  - Red `×` = Marked for deletion
- Status bar shows pending changes count
- Right-click context menu on file list prevents preview change (flag: `_isRightClick`)

### Keyboard Shortcuts (from code, not exhaustive)
- **Ctrl+O** - Open IPF
- **Ctrl+S** - Save changes (overwrite)
- **Ctrl+Shift+S** - Save As
- **Ctrl+E** - Edit IES
- **Ctrl+I** - Import file(s)
- **Right-Click in 3D Viewer** - Toggle camera lock

## Key Files & Patterns

| File | Purpose | Key Methods/Properties |
|------|---------|------------------------|
| `FrmMain.cs` | Main UI orchestration | `OpenIPF()`, `ShowPreview()`, `SaveIPF()`, `ImportFiles()` |
| `FileFormats/IPF/Ipf.cs` | IPF archive reading | `Load()`, `Extract()`, `Repack()` methods |
| `Viewer3D/ModelViewerControl.cs` | 3D rendering pipeline | `LoadModel()`, `RenderFrame()`, `ApplyTexture()` |
| `FileFormats/XAC/XacFile.cs` | Model parsing | `GetAllDiffuseTextures()`, mesh/material iteration |
| `FileFormats/XSM/XsmFile.cs` | Animation parsing | `MotionParts` list, skeletal structure |
| `FileFormats/IES/IesFile.cs` | Database XML | Conversion between binary/XML formats |

## Testing & Validation
- Manual testing workflow:
  1. Build solution (dotnet build)
  2. Launch `bin\Debug\IPF Browser.exe`
  3. Open sample IPF via Ctrl+O or drag-drop
  4. Test file preview (text, image, 3D)
  5. Edit IES, import files, verify status indicators
  6. Save and inspect output IPF with external tool
- No automated unit tests currently; integration tests via manual file operations

## Context-Gathering & Analysis Phase

Before implementing, answer these questions to scope work accurately:

1. **Is this a new feature or a bug fix?**
   - Features: extend core dictionaries (`_pendingChanges`, `_importedFiles`, `_deletedFiles`) + add UI handlers
   - Bugfix: check ErrorLogs for stack traces; identify which component failed

2. **Which component(s) are affected?**
   - UI/FrmMain → check event handlers, state dictionaries, visual indicators
   - File parsing (IPF/XAC/XSM/IES) → validate binary structure, endianness, chunk headers
   - 3D rendering (ModelViewerControl) → isolate shader/texture/transform issues
   - Texture pipeline → trace IPF → extract → load → bind flow

3. **What data flows through the system?**
   - User action → FrmMain event → state update (one of the 4 dictionaries) → visual refresh
   - For file edits: binary read → XML conversion → user edit → binary write back
   - For 3D: binary load → mesh/material creation → GPU texture upload → render loop

4. **Can you reproduce in isolation?**
   - Test with minimal IPF (single file, simple format)
   - Use debugger/ErrorLogs to capture exact error
   - Check if issue is format-specific or universal

## Problem-Solving Methodology

When tackling issues or features, follow this scale-adaptive approach:

### Quick Diagnosis Phase
1. **Identify scope** - Is this UI/state management, file format parsing, or 3D rendering?
2. **Locate symptoms** - Check ErrorLogs/ for relevant timestamps, test reproducibility
3. **Review context** - Read related classes (same component family), scan TODOs for known issues

### Focused Solution Design
**For UI/State Issues:**
- Map state flow: which dictionary (`_pendingChanges`, `_importedFiles`, `_deletedFiles`, `_files`) is affected?
- Check event handlers: ensure `_isRightClick` or other flags aren't blocking logic
- Verify visual indicators: blue/green/red markers update correctly?

**For File Format Problems:**
- Validate binary header (magic number, version)
- Trace parsing step-by-step: read offset → expected data → verify length
- Test with simpler files first (pure XAC vs attachment-laden)
- Use debug dumps: write intermediate binary states to ErrorLogs/ for inspection

**For 3D Rendering Issues:**
- Isolate: render wireframe → render with texture → render with bones → render with animations
- Check transforms: bone matrix application, vertex skinning math
- Verify UV coordinates: texture tiling/stretching often indicates UV issues, not texture loading

### Implementation Checkpoints
- Build often (dotnet build) — catch resource serialization issues early
- Test preview dispatch: does `PreviewType` enum match `ShowPreview()` handler?
- Validate state consistency: if adding to `_pendingChanges`, remove from `_importedFiles`
- Session management: ensure serialization roundtrip (save → load → verify equality)

## Active Development Notes
- **ipf-editing-feature branch** - Current active development
- **Focus areas:** Session management system, mesh position editing, face/hair layering in 3D viewer
- **Known issues in TODO.md:** Build errors (SaveSession/LoadSession methods), wireframe toggle, mesh controller panel visibility
- **Texture loading:** Prefers DDS from IPF; falls back to file system
- **Session format:** Planned as JSON; must include camera state, mesh transforms, texture assignments, file paths
- **Mesh attachment refs:** Parent skeleton bones must be precomputed; stored in `_parentBoneTransforms` dictionary
