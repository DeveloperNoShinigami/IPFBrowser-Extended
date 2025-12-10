# Changelog

All notable changes to IPF Browser Extended will be documented in this file.

## [Unreleased]

### Added
- **3D Model Viewer** - Advanced 3D rendering for XAC model files with:
  - XAC mesh file parsing and rendering
  - DDS texture loading and application
  - Multi-mesh support (in development)
  - Mesh selection with mouse controls (in development)
  - Mesh controller panel for UI management (in development)
  - Camera controls: orbit rotation, pan, and zoom
  - Camera lock toggle with right-click
  - Wireframe rendering mode
  - Billboarded sprite sheets support for face/hair layers
  - Alpha blending for transparency

- **Animation Support** (In Development)
  - XSM animation file format parsing
  - Animation application to 3D models
  - Skeletal animation structure support

- **Session Management** (In Development)
  - Save session menu option to preserve viewer state
  - Load session menu option to restore previous state
  - Planned session file format (.session JSON files)
  - Mesh position persistence

- **File Format Support** (Extended)
  - XAC file format (.xac) - 3D model meshes
  - XSM file format (.xsm) - skeletal animations

- **UI Enhancements**
  - Tools menu with 3D viewer testing
  - Load Additional IPF functionality
  - Multiple IPF file support for cross-referencing
  - Mesh controller panel with ListBox for selection

### Fixed
- **Build System Issues**
  - Resolved System.Resources.Extensions assembly dependency
  - Fixed GenerateResourceUsePreserializedResources configuration
  - Added proper NuGet package references for binary resources
  - Debug and Release builds now compile successfully

- **File Management**
  - Improved IPF file loading and parsing
  - Enhanced error logging for file operations

### In Development
- Mesh selection functionality (click and multi-select)
- Mesh controller panel visibility and interactivity
- Face/hair mesh layering alignment
- Animation playback and blending
- Session save/load functionality
- Mesh position editing UI

### Known Issues
- Mesh controller panel not visible to user in some configurations
- Face/hair mesh alignment needs refinement
- Mesh selection highlighting not fully implemented
- Animation playback UI not yet complete

## [Previous Releases]

### Original IPF Browser Features
- Open and extract IPF files from Tree of Savior
- Text file preview with syntax highlighting
- Image file preview (DDS, TGA)
- IES file editing with XML support
- Import files into IPF archives
- Save and Save As functionality
- Multiple file format support

---

## Development Timeline

### Session 1 - Initial 3D Viewer Implementation
- Created ModelViewerControl.cs for 3D rendering
- Implemented XAC file format parsing
- Added texture loading and DDS support
- Set up camera controls (orbit, zoom, pan)
- Created mesh rendering pipeline
- Implemented wireframe mode

### Session 2 - Animation and Multi-Mesh Support
- Added XSM file format support for animations
- Implemented face/hair layering system
- Created mesh selection logic using dot product
- Added camera lock toggle
- Enhanced mesh rendering with proper layering

### Session 3 - UI Integration and Session Management
- Added Tools menu to main form
- Integrated 3D viewer into main window
- Created mesh controller panel
- Added Save/Load session menu options
- Implemented multi-IPF support
- Fixed resource file compilation issues

### Session 4 - Build System Fixes and Documentation
- Fixed System.Resources.Extensions dependency
- Configured GenerateResourceUsePreserializedResources
- Added NuGet package for binary resources
- Created comprehensive TODO.md with roadmap
- Updated README with current feature status
- Documented all known issues and next steps
- Both Debug and Release builds now compile successfully

---

## Technical Details

### Architecture Changes
- Added `Viewer3D/` directory for 3D rendering components
- Extended file format support with XAC and XSM parsers
- Implemented billboard rendering for 2D sprites in 3D space
- Multi-IPF support allows texture/model reuse across packages

### Dependencies Added
- System.Resources.Extensions (4.7.0) - for binary resource handling

### Build Configuration
- Debug: Full symbols, no optimization, for development
- Release: Optimized, pdb-only symbols, for distribution
- Both configurations support preser serialized resources for binary assets

---

## Future Roadmap

### High Priority
1. Complete mesh selection and highlight system
2. Fix face/hair mesh layering and alignment
3. Implement session save/load functionality
4. Finish animation playback UI and controls
5. Resolve mesh controller panel visibility

### Medium Priority
1. Add mesh position editing in Settings tab
2. Implement mesh standardization analysis
3. Add texture batch replacement
4. Extended animation support (blending, morphs)

### Low Priority
1. Debug visualization mode (bones, bounds, coords)
2. Advanced developer logging
3. Keyboard shortcuts for mesh manipulation
4. Extended file format support

---

## Contributors

- **BlueLotuscoding** - Extended development, 3D viewer implementation, session management
- **exec (exectails)** - Original IPF Browser creator
- **Fatcow** - Toolbar icons (CC BY 3.0)
- **Mark James / famfamfam** - File icons (CC BY 3.0)
