# IPF Browser Extended - Dependency Reference

## Runtime Dependencies

### Required for All Users
| Dependency | Version | Source | Purpose |
|---|---|---|---|
| **.NET Framework** | 4.5+ | Windows built-in / Microsoft | Core runtime environment |
| **Windows OS** | 7 SP1+ | - | Operating system |

### Included with Application (bin folder)
| Component | Version | Location | Purpose |
|---|---|---|---|
| **System.Resources.Extensions** | 4.7.0 | `bin/Debug` or `bin/Release` | Binary resource deserialization |
| **System.Memory** | 4.0.1.2+ | `bin/Debug` or `bin/Release` | Memory allocation utilities |
| **System.Numerics.Vectors** | 4.1.4.0+ | `bin/Debug` or `bin/Release` | Vector math library (for System.Resources.Extensions) |
| **System.Runtime.CompilerServices.Unsafe** | 6.0.0+ | `bin/Debug` or `bin/Release` | Low-level memory operations |
| **OpenTK** | net20 build | `Libs/OpenTK/lib/net20/` | OpenGL graphics rendering |
| **OpenTK.GLControl** | net20 build | `Libs/OpenTK/lib/net20/` | Windows Forms OpenGL control |
| **ScintillaNET** | 3.5.6 | `Libs/jacobslusser.ScintillaNET.3.5.6/` | Syntax-highlighted text editor |
| **RibbonWinForms** | Latest | `Libs/RibbonWinForms/` | Ribbon UI control |
| **Ookii.Dialogs** | Latest | `Libs/` | File/folder selection dialogs |

## Build-Time Dependencies

### Required for Developers
| Tool | Version | Purpose |
|---|---|---|
| **Visual Studio** | 2019 Update 16.10+ or 2022 | C# IDE and compiler |
| **dotnet CLI** | 6.0+ | Command-line build system |
| **Git** | Latest | Version control |
| **MSBuild** | 17.0+ | Build system (usually with Visual Studio) |

### NuGet Packages (Restored during build)
```xml
<!-- packages.config -->
<packages>
  <package id="System.Resources.Extensions" version="4.7.0" targetFramework="net472" />
</packages>
```

All other dependencies are either:
1. Part of .NET Framework 4.5
2. Included in `Libs/` folder (local references)

## Binding Redirects (app.config)

The application configures runtime binding redirects for version compatibility:

```xml
<bindingRedirect name="System.Resources.Extensions" from="0.0.0.0-4.0.1.0" to="4.0.1.0" />
<bindingRedirect name="System.Numerics.Vectors" from="0.0.0.0-4.1.4.0" to="4.1.4.0" />
<bindingRedirect name="System.Memory" from="0.0.0.0-4.0.1.2" to="4.0.1.2" />
<bindingRedirect name="System.Runtime.CompilerServices.Unsafe" from="0.0.0.0-6.0.0.0" to="6.0.0.0" />
```

These ensure that different versions of the same library can be loaded at runtime.

## Project Configuration

### Target Framework
```xml
<TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
```

### Build Features
```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<GenerateResourceUsePreserializedResources>true</GenerateResourceUsePreserializedResources>
```

The project uses:
- **Unsafe code blocks** - For graphics buffer manipulation with pointers
- **Preserialized resources** - For binary resource loading (requires System.Resources.Extensions)

## Graphics Requirements

### Minimum
- **DirectX 9** compatible GPU
- **OpenGL 1.4** support

### Recommended
- **OpenGL 2.0+** for full 3D model rendering
- **GLSL 1.2+** shader support
- Modern GPU drivers (updated within last 2 years)

## File Size Information

| Component | Size |
|---|---|
| Application executable | ~300 KB |
| System.Resources.Extensions.dll | ~50 KB |
| OpenTK.dll | ~200 KB |
| OpenTK.GLControl.dll | ~50 KB |
| ScintillaNET.dll | ~1.5 MB |
| Other libraries | ~2 MB |
| **Total (all assemblies)** | ~4-5 MB |

## Installation Verification

### Check .NET Framework
```powershell
# PowerShell
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" | Select Version
```

```cmd
REM Command Prompt
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Version
```

### Check Runtime Compatibility
Run the application and check `ErrorLogs/Startup_*.txt` for any assembly loading errors.

## Troubleshooting Missing Dependencies

### Problem: "Could not load assembly XYZ"

**Solution Steps:**
1. Check `ErrorLogs/` folder for detailed error messages
2. Verify all `.dll` files exist in `bin/Debug` or `bin/Release`
3. Rebuild the project: `dotnet clean && dotnet build`
4. Update Visual Studio/build tools
5. Delete `obj/` and `bin/` folders, rebuild completely

### Problem: "Could not find file or assembly 'System.Numerics.Vectors'"

**This was a known issue now fixed by:**
- Adding binding redirect to `app.config`
- Adding post-build copy command to `.csproj`
- Ensure you're using the latest version from the repository

### Problem: Graphics library not loading (3D viewer black)

**Solution:**
1. Update GPU drivers to latest version
2. Ensure OpenGL 2.0+ support
3. Check `ErrorLogs/model_debug.log` for rendering errors
4. Try Release build instead of Debug

## Updating Dependencies

### Update NuGet Package
```bash
nuget update packages.config -RepositoryPath packages
# or
dotnet add package System.Resources.Extensions --version 4.7.0
```

### Update OpenTK (from source)
The OpenTK libraries in `Libs/` are pinned to net20 build for .NET Framework 4.5 compatibility.
Only update if necessary for bug fixes or features.

## Distributing the Application

When creating a release build for distribution:

1. Use Release configuration:
   ```bash
   dotnet build IPFBrowser.sln --configuration Release
   ```

2. Copy the entire `Release` folder contents:
   - `IPF Browser.exe`
   - All `.dll` files
   - `IPF Browser.exe.config`
   - Any `.xml` documentation files

3. No additional installation required - users just need:
   - Windows 7 SP1 or later
   - .NET Framework 4.5 or later

## License & Attribution

All dependencies are used under their respective licenses:
- **System.Resources.Extensions** - MIT License
- **OpenTK** - MIT License  
- **ScintillaNET** - MIT License
- **RibbonWinForms** - Licensed under project terms
- **Ookii.Dialogs** - Licensed under project terms

See [LICENSE](LICENSE) file for details.
