# IPF Browser Extended

An extended version of IPF Browser with advanced features for viewing, editing, and managing IPF archive files from Tree of Savior.

## Features

### File Management
- **Open IPF files** - Reads any IPF files (iCBT1, iCBT2, kTOS, iTOS, and current versions)
- **Open multiple IPFs** - Load additional IPF files alongside the main one for cross-referencing
- **Extract files** - Extract single files, all files in one IPF, or an entire client's data
- **Drag & drop support** - Drag IPF files onto the window to open them

### IPF Editing
- **Import files** - Import any file type (IES, DDS, XAC, LUA, etc.) into an IPF
- **Edit IES files** - Edit IES database files as XML with syntax highlighting
- **Delete files** - Mark files for deletion from the IPF
- **Save changes** - Save all modifications (edits, imports, deletions) to the IPF
  - **Save (Ctrl+S)** - Overwrites the current IPF
  - **Save As (Ctrl+Shift+S)** - Saves to a new IPF file
- **Discard changes** - Discard all pending modifications

### Preview
- **Text files** - Preview with syntax highlighting (XML, LUA, JSON, etc.)
- **Image files** - Preview DDS, TGA, and other image formats
- **IES files** - Preview as formatted XML or data grid
- **3D Models** - Preview XAC model files with:
  - Orbit camera controls (mouse drag to rotate)
  - Zoom (mouse wheel)
  - Wireframe toggle
  - Texture loading from IPF or external files
  - Auto-load textures from open IPF
  - Anisotropic filtering for texture quality

### 3D Model Viewer (In Development)
- **View 3D models** - Display XAC body model files with full mesh rendering
- **Texture application** - Apply DDS textures to 3D models from IPF or external files
- **Multi-mesh support** - View multiple mesh components (in development)
  - Mesh selection - Click to select individual meshes, Ctrl+Click for multi-select
  - Mesh controller panel - Right-side panel for managing selected meshes
- **Camera controls** - Pan, rotate, and lock camera position
- **Face/Hair layering** - Billboard rendering for character head components (partly working)
- **Animation support** - Apply XSM animations to 3D models (in development)

### Session Management (In Development)
- **Save session** - Save current model, textures, and mesh state
- **Load session** - Restore previous viewer state from saved session file
- **Mesh position editing** - Adjust and save mesh positions within a session

### User Interface
- **File menu** - Open, Save, Save As, Exit
- **Edit menu** - Edit IES (Ctrl+E), Import File (Ctrl+I), Discard All Changes
- **Context menus** - Right-click for file-specific actions
- **Visual indicators** - Color-coded file status:
  - Blue with `*` prefix: Modified files
  - Green with `+` prefix: Imported files  
  - Red with `×` prefix: Files marked for deletion
- **Status bar** - Shows pending changes count

### Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| Ctrl+O | Open IPF file |
| Ctrl+S | Save changes (overwrite) |
| Ctrl+Shift+S | Save As (new file) |
| Ctrl+E | Edit selected IES file |
| Ctrl+I | Import file(s) |
| Right-Click (3D Viewer) | Toggle camera lock (when over 3D model) |

## Building

Open `IPFBrowser.sln` in Visual Studio 2022 and build in Debug or Release mode.

## Requirements

- .NET Framework 4.5 or later
- Windows 7 or later

## License

See [LICENSE](LICENSE) file for details.

## Credits

- **Original IPF Browser** by [exec (exectails)](https://github.com/exectails/IPFBrowser)
- **Extended features** by [BlueLotuscoding](https://github.com/DeveloperNoShinigami)
- **Toolbar Icons** by Fatcow ([CC BY 3.0](http://creativecommons.org/licenses/by/3.0/))
- **File Icons** by Mark James / famfamfam ([CC BY 3.0](http://creativecommons.org/licenses/by/3.0/))
