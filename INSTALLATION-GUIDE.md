# Installation Documentation Summary

This directory now contains comprehensive documentation for installing and running IPF Browser Extended.

## Quick Navigation

### For End Users
Start with one of these:
- **[QUICKSTART.md](QUICKSTART.md)** - 5-minute guide to get running immediately
- **[INSTALL.md](INSTALL.md)** - Detailed installation with troubleshooting
- **CHECK-DEPENDENCIES.bat** - Run to verify your system is ready

### For Developers
- **[.github/copilot-instructions.md](.github/copilot-instructions.md)** - Architecture & development patterns
- **[INSTALL.md](INSTALL.md)** - Build from source section
- **[DEPENDENCIES.md](DEPENDENCIES.md)** - Complete dependency reference
- **SETUP.bat** or **SETUP.ps1** - Automated build & launch

## File Descriptions

| File | Purpose | For Whom |
|------|---------|----------|
| **QUICKSTART.md** | Get running in 5 minutes | Everyone |
| **INSTALL.md** | Complete installation guide + troubleshooting | Everyone |
| **DEPENDENCIES.md** | Technical dependency reference | Developers |
| **SETUP.bat** | Interactive setup menu (Command Prompt) | Windows users |
| **SETUP.ps1** | Interactive setup menu (PowerShell) | Modern Windows users |
| **CHECK-DEPENDENCIES.bat** | Verify system requirements | Everyone (trouble?) |
| **README.md** | Feature documentation | Everyone |
| **.github/copilot-instructions.md** | Code architecture | Developers |

## Installation Overview

### For Running Pre-built Executable
```
1. Ensure .NET Framework 4.5+ installed
2. Download release ZIP from GitHub
3. Extract and run IPF Browser.exe
```

### For Building from Source
```
1. Clone: git clone https://...
2. Run: SETUP.bat (or SETUP.ps1)
3. Select option 1 (Build Debug)
4. App launches automatically
```

## System Requirements

| Component | Minimum | Recommended |
|---|---|---|
| **OS** | Windows 7 SP1 | Windows 10/11 |
| **.NET** | 4.5 | 4.8+ |
| **RAM** | 512 MB | 2 GB |
| **Disk** | 100 MB | 500 MB |

## Key Improvements Made

✅ **Added INSTALL.md** - Comprehensive guide (500+ lines)
  - Prerequisites breakdown
  - 3 installation methods
  - Build instructions
  - Dependency details
  - Troubleshooting section
  - System requirements table

✅ **Added QUICKSTART.md** - Fast reference guide
  - 2-minute summary for users
  - 3-step developer setup
  - Troubleshooting table
  - Keyboard shortcuts

✅ **Added DEPENDENCIES.md** - Technical reference
  - All runtime dependencies listed
  - Binding redirects documented
  - Graphics requirements
  - Distribution information

✅ **Added SETUP.bat** - Automated Windows setup
  - Interactive menu
  - Check .NET Framework
  - Build or run application
  - Auto-launch after build

✅ **Added SETUP.ps1** - Modern PowerShell version
  - Same functionality as SETUP.bat
  - Better error messages
  - Color-coded output
  - Command-line parameters

✅ **Added CHECK-DEPENDENCIES.bat** - Pre-flight checker
  - Verify .NET Framework installed
  - Check Visual Studio/dotnet CLI
  - Provide download links
  - Helpful guidance

✅ **Updated README.md** - Links to guides
  - Quick start button at top
  - Links to INSTALL.md
  - Quick requirements table

✅ **Updated .github/copilot-instructions.md**
  - Added developer prerequisites
  - Added quick start script instructions
  - Clarified build system options

## First-Time User Experience

### New User Path:
1. Read **QUICKSTART.md** (2 min)
2. Run **CHECK-DEPENDENCIES.bat** (1 min)
3. Download release OR run **SETUP.bat** (2-5 min)
4. Launch application!

### Developer Path:
1. Read **QUICKSTART.md** (2 min)
2. Run **SETUP.ps1** or **SETUP.bat** (2 min)
3. Select build option (2-3 min compile)
4. Start coding with **.github/copilot-instructions.md** as reference

## Troubleshooting Coverage

All common issues now documented with solutions:
- .NET Framework installation
- Build errors
- Missing assemblies
- Graphics driver issues
- Path/location problems

See **INSTALL.md** → Troubleshooting section for full list.

## Next Steps for Users

After successfully running the application:
1. Open a Tree of Savior IPF file
2. Try preview features (3D models, IES editing, etc.)
3. Read **README.md** for complete feature documentation
4. Check **QUICKSTART.md** for keyboard shortcuts

## Next Steps for Developers

After building from source:
1. Read **.github/copilot-instructions.md** for architecture
2. Check **TODO.md** for work items
3. Review **TOS_FILE_FORMATS.md** for file format details
4. Check **DEPENDENCIES.md** if adding new libraries

## Distribution

To share the application with others:
1. Build Release: `dotnet build IPFBrowser.sln --configuration Release`
2. Copy `IPFBrowser/bin/Release/` folder contents
3. Users only need Windows 7+ with .NET Framework 4.5+
4. Provide link to **QUICKSTART.md** for setup

## Questions or Issues?

- **Setup problems?** See **INSTALL.md** Troubleshooting
- **Can't find a feature?** Read **README.md**
- **Want to contribute?** Read **.github/copilot-instructions.md**
- **Report bugs:** GitHub Issues with error log from `ErrorLogs/` folder
