#!/usr/bin/env pwsh
# IPF Browser Extended - Setup and Launch Script
# This script helps setup and run IPF Browser Extended on Windows
# Run with: powershell -ExecutionPolicy Bypass -File SETUP.ps1

param(
    [ValidateSet('build-debug', 'build-release', 'run-debug', 'run-release', 'clean-build', 'interactive')]
    [string]$Action = 'interactive',
    
    [switch]$NoLaunch
)

function Write-Header {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "IPF Browser Extended - Setup" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-DotnetFramework {
    $path = "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"
    if (Test-Path $path) {
        $version = (Get-ItemProperty $path).Version
        Write-Host "✓ .NET Framework $version detected" -ForegroundColor Green
        return $true
    }
    Write-Host "⚠ .NET Framework 4.5+ not detected" -ForegroundColor Yellow
    Write-Host "  Install from: https://www.microsoft.com/en-us/download/details.aspx?id=30653" -ForegroundColor Yellow
    return $false
}

function Test-DotnetCli {
    try {
        $version = & dotnet --version
        Write-Host "✓ dotnet CLI version $version detected" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ dotnet CLI not found" -ForegroundColor Red
        Write-Host "  Install from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
        return $false
    }
}

function Invoke-Build {
    param([string]$Configuration)
    
    Write-Host ""
    Write-Host "Building $Configuration configuration..." -ForegroundColor Cyan
    Write-Host ""
    
    & dotnet build IPFBrowser.sln --configuration $Configuration
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "✗ Build failed!" -ForegroundColor Red
        return $false
    }
    
    Write-Host ""
    Write-Host "✓ Build completed successfully!" -ForegroundColor Green
    return $true
}

function Invoke-Launch {
    param([string]$Configuration)
    
    $exePath = "IPFBrowser\bin\$Configuration\IPF Browser.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Host "✗ Executable not found at: $exePath" -ForegroundColor Red
        return $false
    }
    
    Write-Host ""
    Write-Host "Launching $Configuration build..." -ForegroundColor Green
    & $exePath
    return $true
}

# Main execution
Write-Header

# Verify we're in the right directory
if (-not (Test-Path "IPFBrowser.sln")) {
    Write-Host "✗ Error: IPFBrowser.sln not found!" -ForegroundColor Red
    Write-Host "Please run this script from the project root directory." -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Perform checks
Write-Host "Checking system requirements..." -ForegroundColor Cyan
Write-Host ""
Test-DotnetFramework | Out-Null
Test-DotnetCli | Out-Null

# Interactive menu if no action specified
if ($Action -eq 'interactive') {
    Write-Host ""
    Write-Host "What would you like to do?" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "1 - Build Debug version (recommended for development)"
    Write-Host "2 - Build Release version (optimized, smaller)"
    Write-Host "3 - Run existing Debug build"
    Write-Host "4 - Run existing Release build"
    Write-Host "5 - Clean and rebuild Debug"
    Write-Host "6 - Exit"
    Write-Host ""
    
    $choice = Read-Host "Enter your choice (1-6)"
    
    switch ($choice) {
        "1" { $Action = 'build-debug' }
        "2" { $Action = 'build-release' }
        "3" { $Action = 'run-debug' }
        "4" { $Action = 'run-release' }
        "5" { $Action = 'clean-build' }
        "6" { Write-Host "Exiting..." -ForegroundColor Yellow; exit 0 }
        default { 
            Write-Host "Invalid choice. Exiting." -ForegroundColor Red
            exit 1 
        }
    }
}

# Execute action
Write-Host ""
switch ($Action) {
    'build-debug' {
        if (Invoke-Build "Debug") {
            if (-not $NoLaunch) {
                Start-Sleep -Seconds 2
                Invoke-Launch "Debug" | Out-Null
            }
        }
    }
    'build-release' {
        if (Invoke-Build "Release") {
            if (-not $NoLaunch) {
                Start-Sleep -Seconds 2
                Invoke-Launch "Release" | Out-Null
            }
        }
    }
    'run-debug' {
        Invoke-Launch "Debug" | Out-Null
    }
    'run-release' {
        Invoke-Launch "Release" | Out-Null
    }
    'clean-build' {
        Write-Host "Cleaning build artifacts..." -ForegroundColor Cyan
        & dotnet clean IPFBrowser.sln
        
        if (Invoke-Build "Debug") {
            if (-not $NoLaunch) {
                Start-Sleep -Seconds 2
                Invoke-Launch "Debug" | Out-Null
            }
        }
    }
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
