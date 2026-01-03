<#
.SYNOPSIS
    Builds both the Rust FFI library and the C# WPF application.

.DESCRIPTION
    This script builds the complete application by:
    1. Building the Rust FFI library (mic-engine-ffi)
    2. Building the C# WPF application (MicrophoneManager)

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release)

.PARAMETER SkipRust
    Skip building Rust (use existing DLL)

.EXAMPLE
    .\build-all.ps1
    .\build-all.ps1 -Configuration Debug
    .\build-all.ps1 -SkipRust
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipRust
)

$ErrorActionPreference = "Stop"

# Paths
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}

$ScriptsDir = Join-Path $RepoRoot "scripts"
$CSharpProjectDir = Join-Path $RepoRoot "MicrophoneManager"
$CSharpProject = Join-Path $CSharpProjectDir "MicrophoneManager.csproj"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Windows Microphone Manager - Full Build" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration"
Write-Host "Repository: $RepoRoot"
Write-Host ""

# Step 1: Build Rust FFI library
if (-not $SkipRust) {
    Write-Host "--- Step 1: Building Rust FFI Library ---" -ForegroundColor Yellow
    & (Join-Path $ScriptsDir "build-rust-ffi.ps1") -Configuration $Configuration

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Rust build failed"
        exit $LASTEXITCODE
    }
    Write-Host ""
}
else {
    Write-Host "--- Step 1: Skipping Rust build (using existing DLL) ---" -ForegroundColor Yellow
    Write-Host ""
}

# Step 2: Build C# project
Write-Host "--- Step 2: Building C# WPF Application ---" -ForegroundColor Yellow

$dotnetArgs = @("build", $CSharpProject, "-c", $Configuration, "-p:Platform=x64")
Write-Host "Running: dotnet $($dotnetArgs -join ' ')" -ForegroundColor Yellow

& dotnet $dotnetArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "C# build failed"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host ""

# Show output location
$OutputDir = Join-Path $CSharpProjectDir "bin\x64\$Configuration\net8.0-windows"
if (Test-Path $OutputDir) {
    Write-Host "Output: $OutputDir"
    Write-Host ""
    Write-Host "To run:"
    Write-Host "  cd $OutputDir"
    Write-Host "  .\MicrophoneManager.exe"
}
