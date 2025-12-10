# Quick Start Guide

## For End Users (Just Want to Run It)

### Step 1: Check Prerequisites
- Windows 7 or later ✓
- .NET Framework 4.5+ installed
  - Check: Settings → Apps → Apps & features → "Turn Windows features on or off"
  - If missing: [Download .NET Framework](https://www.microsoft.com/en-us/download/details.aspx?id=30653)

### Step 2: Get the Application
**Option A: Download Pre-built Release (Easiest)**
1. Go to [Releases](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/releases)
2. Download the latest ZIP file
3. Extract to your desired folder
4. Double-click `IPF Browser.exe`

**Option B: Clone and Build from Source**
```bash
git clone https://github.com/DeveloperNoShinigami/IPFBrowser-Extended.git
cd IPFBrowser-Extended
dotnet build IPFBrowser.sln --configuration Release
cd IPFBrowser/bin/Release
"IPF Browser.exe"
```

### Step 3: Use the Application
1. Click **File → Open** (or press Ctrl+O)
2. Select an IPF file from your Tree of Savior client folder
3. Browse files in the left panel
4. Click to preview files

## For Developers (Building & Contributing)

### Step 1: Setup
```bash
# Clone repository
git clone https://github.com/DeveloperNoShinigami/IPFBrowser-Extended.git
cd IPFBrowser-Extended

# Install/restore dependencies
dotnet restore IPFBrowser.sln
```

### Step 2: Build Options

**Quick Build & Run (Windows)**
```bash
SETUP.bat
# Select option 1 for Debug build
```

**or (PowerShell)**
```powershell
powershell -ExecutionPolicy Bypass -File SETUP.ps1
```

**or (Manual dotnet)**
```bash
dotnet build IPFBrowser.sln --configuration Debug
cd IPFBrowser/bin/Debug
"IPF Browser.exe"
```

### Step 3: Make Changes & Test
- Edit code in Visual Studio 2022 or your preferred editor
- Rebuild: `dotnet build IPFBrowser.sln --configuration Debug`
- Run: `IPFBrowser/bin/Debug/IPF Browser.exe`
- Check logs: `IPFBrowser/bin/Debug/ErrorLogs/` for errors
- Read architecture: `.github/copilot-instructions.md`

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `.NET Framework not found` | Install from [Microsoft downloads](https://www.microsoft.com/en-us/download/details.aspx?id=30653) |
| `IPF Browser.exe not found` | Build first: `dotnet build IPFBrowser.sln --configuration Debug` |
| `System.Numerics.Vectors` error | Already fixed in recent builds; update to latest version |
| App crashes on startup | Check `ErrorLogs/` folder for error details |
| 3D models not rendering | Update graphics drivers (need OpenGL 2.0+) |

## Next Steps

- **Learn Features:** Read [README.md](README.md)
- **System Requirements:** See [INSTALL.md](INSTALL.md)
- **Architecture:** Check [.github/copilot-instructions.md](.github/copilot-instructions.md)
- **File Formats:** Read [TOS_FILE_FORMATS.md](TOS_FILE_FORMATS.md)
- **Report Issues:** Go to [GitHub Issues](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/issues)

## Common Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+O** | Open IPF file |
| **Ctrl+S** | Save changes |
| **Ctrl+Shift+S** | Save As |
| **Ctrl+E** | Edit IES file |
| **Ctrl+I** | Import file |

## Need Help?

1. Check [INSTALL.md](INSTALL.md) for detailed troubleshooting
2. Review error logs in `ErrorLogs/` folder
3. Open an issue on [GitHub](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/issues)
4. Include your error log and system info (Windows version, .NET version)
