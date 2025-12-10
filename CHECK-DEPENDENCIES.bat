@echo off
REM IPF Browser Extended - Dependency Checker & Installer
REM This script verifies that all required dependencies are installed
REM and provides links/instructions for missing components

setlocal enabledelayedexpansion

echo.
echo ========================================
echo Dependency Checker - IPF Browser Extended
echo ========================================
echo.

set missing=0

REM Check Windows Version
echo Checking Windows version...
for /f "tokens=2 delims==" %%a in ('wmic os get version /value') do set WIN_VERSION=%%a
if "%WIN_VERSION%"=="" (
    echo   ✗ Unable to determine Windows version
    set missing=1
) else (
    echo   ✓ Windows %WIN_VERSION%
)
echo.

REM Check .NET Framework 4.5
echo Checking .NET Framework 4.5+...
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Version >nul 2>&1
if errorlevel 1 (
    echo   ✗ NOT FOUND
    echo.
    echo   ACTION REQUIRED: Install .NET Framework 4.5+
    echo   Download from: https://www.microsoft.com/en-us/download/details.aspx?id=30653
    echo   After installation, restart your computer.
    echo.
    set missing=1
) else (
    for /f "tokens=2 delims==" %%a in ('reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Version') do (
        echo   ✓ Found version %%a
    )
)
echo.

REM Check Visual Studio (optional for users)
echo Checking Visual Studio installation (optional for developers)...
if exist "C:\Program Files\Microsoft Visual Studio\2022\*" (
    echo   ✓ Visual Studio 2022 detected
) else if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\*" (
    echo   ✓ Visual Studio 2019 detected
) else (
    echo   ⓘ Visual Studio not detected (only needed for development)
    echo     For users: Pre-built executable doesn't require Visual Studio
    echo     For developers: Download from https://visualstudio.microsoft.com/
)
echo.

REM Check dotnet CLI (optional)
echo Checking dotnet CLI (optional)...
where dotnet >nul 2>&1
if errorlevel 1 (
    echo   ⓘ dotnet CLI not found
    echo     For developers: Download from https://dotnet.microsoft.com/download
    echo     For users: Pre-built executable doesn't require this
) else (
    for /f %%a in ('dotnet --version') do (
        echo   ✓ dotnet CLI version %%a
    )
)
echo.

REM Check Git (optional)
echo Checking Git installation (optional)...
where git >nul 2>&1
if errorlevel 1 (
    echo   ⓘ Git not found
    echo     Only needed if cloning from GitHub
    echo     Download from: https://git-scm.com/
) else (
    for /f %%a in ('git --version') do (
        echo   ✓ %%a
    )
)
echo.

REM Summary
echo ========================================
if %missing% equ 0 (
    echo ✓ All required dependencies are installed!
    echo.
    echo You can now:
    echo  1. Download the pre-built release from GitHub
    echo  2. Or clone and build from source:
    echo     git clone https://github.com/DeveloperNoShinigami/IPFBrowser-Extended.git
    echo     cd IPFBrowser-Extended
    echo     SETUP.bat
) else (
    echo ✗ Missing required dependencies - see above for installation links
    echo.
    echo After installing the missing components:
    echo  1. Restart your computer
    echo  2. Run this script again to verify
)
echo ========================================
echo.

REM Offer to open documentation
set /p help="View full installation guide? (Y/n): "
if /i "%help%"=="Y" (
    if exist "INSTALL.md" (
        start notepad INSTALL.md
    ) else (
        start https://github.com/DeveloperNoShinigami/IPFBrowser-Extended/blob/main/INSTALL.md
    )
)

pause
