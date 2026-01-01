# TODO - IPF Browser Extended Development Roadmap

## In Progress

### Session Management System
- [ ] Implement `SaveSession_Click` event handler
  - Serialize current model state to `.session` file (JSON)
  - Save mesh list, positions, rotations, and texture mappings
  - Include camera position and zoom level
- [ ] Implement `LoadSession_Click` event handler
  - Deserialize `.session` file and restore viewer state
  - Reload all meshes and textures from IPF or file paths
  - Restore camera position and mesh selections
- [ ] Create session file format (JSON schema)

### Mesh Position Editing
- [ ] Add mesh position editor to Settings tab
  - NumericUpDown controls for X, Y, Z position adjustment
  - Rotation editor for mesh orientation
  - Real-time preview of changes in 3D viewer
- [ ] Persistence of mesh positions to session file
- [ ] Undo/redo functionality for mesh edits

## High Priority Fixes

### Build Errors
- [ ] Fix event handler method placement in `FrmMain.cs`
  - Methods `SaveSession_Click` and `LoadSession_Click` not compiling
  - Need to move methods inside `FrmMain` class definition
- [ ] Resolve missing assembly references
  - `System.Resources.Extensions` not found
  - Update project file references or NuGet packages

### 3D Model Rendering
- [ ] Fix face/hair mesh layering
  - Face mesh displaying incorrectly in front of hair
  - Billboard orientation needs adjustment
  - UV coordinates for sprite sheets may need standardization
- [ ] Head model assembly
  - Hair should form proper 3D-looking layer around face
  - Both components should align at anchor points
  - Test with various character models

### Mesh Selection & UI
- [ ] Make mesh controller panel visible and interactive
  - Panel docked to right of viewport but not rendering
  - ListBox for mesh selection not responding to clicks
  - Need to verify control hierarchy and event binding
- [ ] Improve mesh selection feedback
  - Highlight selected mesh in 3D viewer
  - Update ListBox selection when clicking on mesh
  - Color-code mesh selection in viewer

### Camera Controls
- [ ] Restore camera rotation and pan functionality
  - Mouse drag rotation not working
  - Camera lock toggle (right-click) needs implementation
- [ ] Improve camera navigation
  - Add keyboard shortcuts for camera movement
  - Implement smooth camera transitions

## Medium Priority Features

### Animation Support
- [ ] Load and display XSM animation files
  - Parse XSM binary format
  - Apply skeletal animation to mesh
  - Create animation playback controls (play, pause, speed)
- [ ] Animation blending
  - Support multiple simultaneous animations
  - Fade between animation states

### Texture Management
- [ ] Improved texture loading
  - Load textures from multiple IPF sources
  - Support for DDS, TGA, PNG, JPG formats
  - Proper texture coordinate mapping
- [ ] Texture replacement workflow
  - Drag-and-drop texture application
  - Preview texture changes before applying
  - Batch texture replacement

### File Format Support
- [ ] XSM file improvements
  - Complete animation skeleton parsing
  - Morph target support
- [ ] XAC mesh enhancements
  - Collision mesh visualization
  - LOD (Level of Detail) support

## Low Priority Enhancements

### User Experience
- [ ] Mesh standardization analysis
  - Document face/hair mesh file structure
  - Identify common positioning patterns
  - Create standardization guidelines
- [ ] Extended context menus
  - Add "Copy path" for selected meshes
  - Add "Show in folder" for extracted files
- [ ] Keyboard shortcuts
  - Add shortcuts for mesh rotation adjustment
  - Quick save/load session hotkeys

### Developer Features
- [ ] Debug mode
  - Wireframe rendering with bone display
  - Mesh bounds visualization
  - Coordinate system display
- [ ] Logging system
  - Detailed logs for model loading
  - Animation parsing debug info
  - Texture resolution logs

## Known Issues

1. **Build Compilation** - SaveSession_Click and LoadSession_Click methods not properly defined in class scope
2. **Face/Hair Alignment** - Hair mesh displays sideways, face misaligned with proper layering
3. **Mesh Controller Panel** - Panel is not visible to user despite being defined in Designer
4. **Missing Assembly** - System.Resources.Extensions assembly resolution issue
5. **Camera Controls** - Rotation and pan not functioning correctly
6. **Mesh Selection** - Multi-select with Ctrl+Click not implemented

## Testing Checklist

- [ ] Build solution without errors
- [ ] Load XAC model file (body model)
- [ ] Display face/hair components with proper layering
- [ ] Test camera rotation with mouse drag
- [ ] Test camera lock toggle with right-click
- [ ] Save session to file
- [ ] Load session from file
- [ ] Verify mesh positions restored correctly
- [ ] Test with multiple character models
- [ ] Verify texture loading from IPF
- [ ] Test animation loading and playback

## Notes

### Architecture
- Main viewer control: `ModelViewerControl.cs`
- UI form: `FrmMain.cs` with Designer file `FrmMain.Designer.cs`
- File formats: Located in `FileFormats/` directory
- XAC models: `FileFormats/IPF/Ipf.cs` and related files

### Dependencies
- ScintillaNET for text editing
- DirectX/SharpDX for 3D rendering (if used)
- Windows Forms for UI

### Session File Format (Planned)
```json
{
  "ipfPath": "path/to/model.ipf",
  "meshes": [
    {
      "name": "face",
      "filePath": "char/c_women_face_v1_01.xac",
      "position": { "x": 0, "y": 0, "z": 0 },
      "rotation": { "x": 0, "y": 0, "z": 0 },
      "selected": true
    },
    {
      "name": "hair_long_01",
      "filePath": "char/c_women_hair_long_01.xac",
      "position": { "x": 0, "y": 0.1, "z": 0 },
      "rotation": { "x": 0, "y": 0, "z": 0 },
      "selected": false
    }
  ],
  "camera": {
    "position": { "x": 2, "y": 2, "z": 2 },
    "zoom": 1.0,
    "locked": false
  },
  "textures": {
    "face": "char/c_women_face_v1_01_d.dds",
    "hair": "char/c_women_hair_long_01_d.dds"
  }
}
```
