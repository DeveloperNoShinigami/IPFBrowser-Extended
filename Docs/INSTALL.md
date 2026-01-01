# IPF Browser Extended - Installation & Setup Guide

## Prerequisites

### For End Users (Running the Pre-built Executable)
- **Windows 7 or later** (Windows 10/11 recommended)
- **.NET Framework 4.5 or later** (Usually already installed on Windows 7+)
  - [Download .NET Framework](https://www.microsoft.com/en-us/download/details.aspx?id=30653)
  - Check your version: Settings → Apps → Apps & features → "Turn Windows features on or off" → .NET Framework
- **Approximately 50-100 MB free disk space** for the application and dependencies

### For Developers (Building from Source)
- **Windows 7 or later** (Windows 10/11 recommended)
- **.NET Framework 4.5 or later** (same as end users)
- **Visual Studio 2022** or later (with C# workload)
  - OR: **Visual Studio 2019 Update 16.10+** (minimal requirement)
  - OR: **MSBuild 17.0+** with .NET SDK installed
- **Git** (for cloning the repository)

## Installation Methods

### Method 1: Run Pre-built Executable (Easiest)

1. Download the latest release from the [Releases page](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/releases)
2. Extract the ZIP file to your desired location
3. Double-click `IPF Browser.exe` to launch
4. If you get an error about missing assemblies, see Troubleshooting section below

### Method 2: Build from Source

#### Step 1: Clone the Repository
```bash
git clone https://github.com/DeveloperNoShinigami/IPFBrowser-Extended.git
cd IPFBrowser-Extended
```

#### Step 2: Restore Dependencies
```bash
dotnet restore IPFBrowser.sln
```

#### Step 3: Build the Project
**Option A: Using dotnet CLI (Recommended)**
```bash
dotnet build IPFBrowser.sln --configuration Debug
```

**Option B: Using Visual Studio 2022**
1. Open `IPFBrowser.sln` in Visual Studio
2. Go to Build → Build Solution (Ctrl+Shift+B)
3. Wait for the build to complete

**Option C: Using MSBuild**
```bash
msbuild IPFBrowser.sln /p:Configuration=Debug
```

#### Step 4: Run the Application
The executable will be created at:
```
IPFBrowser/bin/Debug/IPF Browser.exe
```

Double-click it to launch, or run from command line:
```bash
cd IPFBrowser/bin/Debug
"IPF Browser.exe"
```

## Dependencies

### Core Framework
- **.NET Framework 4.5** - Base runtime (included with Windows 7 SP1+)

### NuGet Packages (Automatically Installed)
- **System.Resources.Extensions** (v4.7.0) - Binary resource serialization
- System.Memory
- System.Runtime.CompilerServices.Unsafe
- System.Numerics.Vectors

### Local Libraries (Included in Repository)
- **OpenTK** (net20 build) - OpenGL 3D graphics rendering
- **ScintillaNET** (v3.5.6) - Syntax-highlighted text editor
- **RibbonWinForms** - Ribbon UI controls
- **Ookii.Dialogs** - File/folder dialogs

All dependencies are either included in the `Libs/` folder or automatically restored via NuGet.

## Troubleshooting

### "Could not load file or assembly 'System.Numerics.Vectors'"
**Solution:** This was a known issue that has been fixed in recent versions.
- If you're using an older build, update to the latest version
- **For pre-built users:** Delete the `ErrorLogs/` folder and try again
- **For developers:** Run `dotnet clean` then `dotnet build` again

### ".NET Framework 4.5 or later is required"
- Install .NET Framework 4.5 or later from [Microsoft Downloads](https://www.microsoft.com/en-us/download/)
- Restart your computer after installation
- Try running the application again

### "Could not find file 'IPF Browser.exe' after building"
- Ensure the build completed without errors (check Visual Studio output)
- Navigate to `IPFBrowser/bin/Debug/` folder
- If the file doesn't exist, try:
  ```bash
  dotnet clean IPFBrowser.sln
  dotnet build IPFBrowser.sln --configuration Debug
  ```

### Application crashes on startup
1. Check `ErrorLogs/` folder (created in same directory as executable) for detailed error messages
2. Verify your .NET Framework installation is correct
3. Try running from the Release folder instead: `IPFBrowser/bin/Release/IPF Browser.exe`
4. Report the issue with the error log content on the [Issues page](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/issues)

### 3D viewer not rendering models
- Ensure your graphics driver is up to date (OpenGL 2.0+)
- Check `ErrorLogs/model_debug.log` for rendering-specific errors
- Try disabling hardware acceleration: Settings → Graphics (if available in future versions)

## System Requirements Summary

| Requirement | Minimum | Recommended |
|---|---|---|
| **OS** | Windows 7 SP1 | Windows 10/11 |
| **.NET Framework** | 4.5 | 4.8+ |
| **RAM** | 512 MB | 2 GB+ |
| **Disk Space** | 100 MB | 500 MB+ |
| **GPU** | DirectX 9 compatible | OpenGL 3.0+ |
| **Processor** | Dual-core 2.0 GHz | Quad-core 2.5 GHz+ |

## Building for Release

To create an optimized release build:

```bash
dotnet build IPFBrowser.sln --configuration Release
```

The executable will be at: `IPFBrowser/bin/Release/IPF Browser.exe`

For distribution, copy the entire `Release` folder contents, which includes all necessary dependencies.

## Development Setup

For developers wanting to contribute or modify the application:

1. Follow "Build from Source" steps above
2. Open `IPFBrowser.sln` in Visual Studio 2022
3. Install any recommended extensions (C#, NuGet Package Manager)
4. Read [`.github/copilot-instructions.md`](.github/copilot-instructions.md) for architecture details
5. Read `TODO.md` for known issues and planned features
6. See [`CONTRIBUTING.md`](CONTRIBUTING.md) for contribution guidelines

## Getting Help

- **Bug Reports:** Open an issue on [GitHub Issues](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/issues)
- **Build Issues:** Provide the error log from `ErrorLogs/` folder
- **Feature Requests:** Discuss on the Issues page
- **Documentation:** Check [`TOS_FILE_FORMATS.md`](TOS_FILE_FORMATS.md) for file format details

## Next Steps

Once installed and running:
1. Open an IPF file (Ctrl+O) or drag one onto the window
2. Browse files in the left panel
3. Click files to preview them
4. See [README.md](README.md) for feature documentation
