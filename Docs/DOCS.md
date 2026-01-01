# IPF Browser Extended - Documentation Index

Welcome to IPF Browser Extended! This is your complete guide to getting started with the application.

## 🚀 Get Started in 30 Seconds

**Just want to run it?**
1. Download the latest [Release](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/releases)
2. Extract the ZIP
3. Run `IPF Browser.exe`

**Have .NET Framework 4.5+?** ✓ You're good to go!

**Don't have it?** → [Install .NET Framework](https://www.microsoft.com/en-us/download/details.aspx?id=30653)

---

## 📚 Documentation Guide

### I Want To...

**Run the application:**
- ⭐ [**Quick Start Guide** (5 min)](QUICKSTART.md) - Fastest way to get running
- 📖 [**Full Installation Guide**](INSTALL.md) - Detailed steps with troubleshooting

**Build from source (develop):**
- ⭐ [**Quick Start Guide**](QUICKSTART.md) - Developer section
- 📖 [**Full Installation Guide**](INSTALL.md) - Build from source section
- 🛠️ [**SETUP.bat or SETUP.ps1**](SETUP.bat) - Automated build script
- 🏗️ [**AI Coding Instructions**](.github/copilot-instructions.md) - Architecture reference

**Understand the code:**
- 🏗️ [**.github/copilot-instructions.md**](.github/copilot-instructions.md) - Complete architecture guide
- 🔗 [**DEPENDENCIES.md**](DEPENDENCIES.md) - Technical dependency reference
- 📝 [**TOS_FILE_FORMATS.md**](TOS_FILE_FORMATS.md) - Game file format documentation
- 📋 [**TODO.md**](TODO.md) - Known issues and work items

**Check my system:**
- ✅ [**CHECK-DEPENDENCIES.bat**](CHECK-DEPENDENCIES.bat) - Verify prerequisites are installed
- 📊 [**INSTALL.md → System Requirements**](INSTALL.md#system-requirements-summary) - Detailed specs

**Use the features:**
- 🎯 [**README.md**](README.md) - Feature documentation
- ⌨️ [**QUICKSTART.md → Keyboard Shortcuts**](QUICKSTART.md#common-keyboard-shortcuts) - All shortcuts

**Troubleshoot issues:**
- 🆘 [**INSTALL.md → Troubleshooting**](INSTALL.md#troubleshooting) - Common problems & solutions
- 📋 [**QUICKSTART.md → Troubleshooting**](QUICKSTART.md#troubleshooting) - Quick reference table

**Set up development environment:**
- 🛠️ Use **SETUP.bat** or **SETUP.ps1** to build automatically
- 👉 Read [**.github/copilot-instructions.md**](.github/copilot-instructions.md) for code patterns
- 📚 Check [**INSTALL.md → Development Setup**](INSTALL.md#development-setup) for IDE configuration

---

## 📄 File Reference

### For Everyone
| File | Purpose | Length |
|------|---------|--------|
| **QUICKSTART.md** | 5-min getting started | 2 pages |
| **INSTALL.md** | Complete installation + troubleshooting | 8 pages |
| **README.md** | Feature documentation | 3 pages |
| **CHANGELOG.md** | Version history | Latest changes |
| **LICENSE** | Legal information | MIT License |

### For Developers
| File | Purpose | Length |
|------|---------|--------|
| **.github/copilot-instructions.md** | Architecture & patterns | 8 pages |
| **DEPENDENCIES.md** | Technical dependency reference | 5 pages |
| **TOS_FILE_FORMATS.md** | Game file format specs | Detailed specs |
| **TODO.md** | Known issues & roadmap | Work items |

### Automation Scripts
| File | Purpose | Windows |
|------|---------|---------|
| **SETUP.bat** | Interactive build menu | ✅ CMD |
| **SETUP.ps1** | Interactive build menu | ✅ PowerShell |
| **CHECK-DEPENDENCIES.bat** | Verify prerequisites | ✅ CMD |

---

## 🎯 Common Tasks

### Running for the First Time
1. Read **QUICKSTART.md** (2 min)
2. Run **CHECK-DEPENDENCIES.bat** (verify .NET Framework)
3. Download release ZIP OR build from source with **SETUP.bat**
4. Launch `IPF Browser.exe`

### Setting Up for Development
1. Clone: `git clone https://github.com/DeveloperNoShinigami/IPFBrowser-Extended.git`
2. Run **SETUP.bat** → choose option 1
3. Read **.github/copilot-instructions.md** for code structure
4. Check **TODO.md** for issues to work on

### Troubleshooting a Problem
1. Check the appropriate troubleshooting section:
   - General issues → [INSTALL.md Troubleshooting](INSTALL.md#troubleshooting)
   - Quick reference → [QUICKSTART.md Troubleshooting](QUICKSTART.md#troubleshooting)
2. Review error log in `ErrorLogs/` folder
3. Check [DEPENDENCIES.md](DEPENDENCIES.md) for assembly issues
4. Report issue with error log → [GitHub Issues](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/issues)

### Building a Release Package
1. Build Release: `dotnet build IPFBrowser.sln --configuration Release`
2. Copy contents of `IPFBrowser/bin/Release/`
3. User only needs: Windows 7+ with .NET Framework 4.5+
4. Include link to **QUICKSTART.md** in distribution

### Contributing Code
1. Read **.github/copilot-instructions.md** - know the architecture
2. Read **TODO.md** - find work items
3. Check **DEPENDENCIES.md** if adding libraries
4. Build/test with **SETUP.bat**
5. Submit pull request

---

## 🔧 System Requirements

### Minimum (Users)
- **OS:** Windows 7 SP1 or later
- **.NET:** Framework 4.5+
- **RAM:** 512 MB
- **Disk:** 100 MB

### Recommended (Developers)
- **OS:** Windows 10 or 11
- **.NET:** Framework 4.8+ and dotnet CLI
- **IDE:** Visual Studio 2022
- **RAM:** 4+ GB
- **Disk:** 2+ GB

---

## ❓ FAQ

**Q: Do I need to install anything to run it?**
A: Just .NET Framework 4.5+ (pre-installed on most Windows systems). See [INSTALL.md](INSTALL.md).

**Q: Can I build from source?**
A: Yes! Follow [QUICKSTART.md](QUICKSTART.md) or use **SETUP.bat**.

**Q: Where are the error logs?**
A: In `ErrorLogs/` folder next to the .exe file. Share these when reporting bugs.

**Q: How do I edit IES files?**
A: Open an IPF, select an .ies file, press Ctrl+E or use Edit menu. See [README.md](README.md).

**Q: Can I preview 3D models?**
A: Yes! Open XAC files. See [README.md → 3D Models](README.md#preview).

**Q: What's the architecture?**
A: Read [.github/copilot-instructions.md](.github/copilot-instructions.md).

**Q: How do I contribute?**
A: See **.github/copilot-instructions.md** and [INSTALL.md → Development Setup](INSTALL.md#development-setup).

---

## 📞 Getting Help

| Problem | Solution |
|---------|----------|
| Can't get it running? | [INSTALL.md Troubleshooting](INSTALL.md#troubleshooting) |
| Build errors? | [QUICKSTART.md Troubleshooting](QUICKSTART.md#troubleshooting) |
| Missing dependencies? | Run **CHECK-DEPENDENCIES.bat** |
| Found a bug? | [Report on GitHub](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/issues) with error log |
| Want a feature? | [Request on GitHub Issues](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/issues) |
| Architecture questions? | Read [.github/copilot-instructions.md](.github/copilot-instructions.md) |
| File format details? | See [TOS_FILE_FORMATS.md](TOS_FILE_FORMATS.md) |

---

## 🎓 Learning Path

### New User
```
QUICKSTART.md → CHECK-DEPENDENCIES.bat → Download Release → Run!
```

### New Developer
```
QUICKSTART.md (dev section) → SETUP.bat → Read .github/copilot-instructions.md → Code!
```

### Contributing
```
Clone → Read .github/copilot-instructions.md → Check TODO.md → SETUP.bat → Code → PR
```

### Troubleshooting
```
Error? → Check INSTALL.md/QUICKSTART.md troubleshooting → Review ErrorLogs/ → Report issue
```

---

## 📦 Version Info

- **Latest Release:** See [Releases](https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/releases)
- **Current Branch:** `ipf-editing-feature` (development)
- **Stability:** Production-ready with active development
- **License:** [MIT License](LICENSE)

---

**Ready to get started?** → **[QUICKSTART.md](QUICKSTART.md)** ⭐
