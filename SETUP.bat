@echo off
REM IPF Browser Extended - Setup and Launch Script
REM This script helps setup and run IPF Browser Extended on Windows

setlocal enabledelayedexpansion

echo.
echo ========================================
echo IPF Browser Extended - Setup
echo ========================================
echo.

REM Check if we're in the right directory
if not exist "IPFBrowser.sln" (
    echo Error: IPFBrowser.sln not found!
    echo Please run this script from the root project directory.
    echo.
    pause
    exit /b 1
)

REM Check .NET Framework
echo Checking .NET Framework installation...
reg query "HKLM\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" /v Version >nul 2>&1
if errorlevel 1 (
    echo.
    echo WARNING: .NET Framework 4.5+ may not be installed!
    echo Please install from: https://www.microsoft.com/en-us/download/details.aspx?id=30653
    echo.
    pause
)

REM Offer build or run options
echo.
echo What would you like to do?
echo 1 - Build Debug version (recommended for development)
echo 2 - Build Release version (optimized, smaller file size)
echo 3 - Run existing Debug build
echo 4 - Run existing Release build
echo 5 - Clean and rebuild Debug
echo 6 - Exit
echo.

set /p choice="Enter your choice (1-6): "

if "%choice%"=="1" (
    goto build_debug
) else if "%choice%"=="2" (
    goto build_release
) else if "%choice%"=="3" (
    goto run_debug
) else if "%choice%"=="4" (
    goto run_release
) else if "%choice%"=="5" (
    goto clean_build_debug
) else if "%choice%"=="6" (
    goto end
) else (
    echo Invalid choice. Please run the script again.
    pause
    exit /b 1
)

:build_debug
echo.
echo Building Debug configuration...
echo.
dotnet build IPFBrowser.sln --configuration Debug
if errorlevel 1 (
    echo.
    echo Build failed! Please check the error messages above.
    pause
    exit /b 1
)
echo.
echo Build completed successfully!
echo Launching application...
timeout /t 2 /nobreak
goto run_debug

:build_release
echo.
echo Building Release configuration...
echo.
dotnet build IPFBrowser.sln --configuration Release
if errorlevel 1 (
    echo.
    echo Build failed! Please check the error messages above.
    pause
    exit /b 1
)
echo.
echo Build completed successfully!
echo Launching application...
timeout /t 2 /nobreak
goto run_release

:run_debug
echo.
echo Launching Debug build...
if exist "IPFBrowser\bin\Debug\IPF Browser.exe" (
    start "" "IPFBrowser\bin\Debug\IPF Browser.exe"
    echo Application launched!
    exit /b 0
) else (
    echo Error: IPF Browser.exe not found in IPFBrowser\bin\Debug\
    echo Did you build the project first? Choose option 1 or 5.
    pause
    exit /b 1
)

:run_release
echo.
echo Launching Release build...
if exist "IPFBrowser\bin\Release\IPF Browser.exe" (
    start "" "IPFBrowser\bin\Release\IPF Browser.exe"
    echo Application launched!
    exit /b 0
) else (
    echo Error: IPF Browser.exe not found in IPFBrowser\bin\Release\
    echo Did you build the project first? Choose option 2.
    pause
    exit /b 1
)

:clean_build_debug
echo.
echo Cleaning build artifacts...
dotnet clean IPFBrowser.sln
echo.
echo Building Debug configuration...
dotnet build IPFBrowser.sln --configuration Debug
if errorlevel 1 (
    echo.
    echo Build failed! Please check the error messages above.
    pause
    exit /b 1
)
echo.
echo Clean build completed successfully!
echo Launching application...
timeout /t 2 /nobreak
goto run_debug

:end
echo.
echo Setup cancelled. For manual instructions, see INSTALL.md
echo.
exit /b 0
