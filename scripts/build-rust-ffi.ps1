<#
.SYNOPSIS
    Builds the Rust FFI library and copies it to the C# project.

.DESCRIPTION
    This script builds the mic-engine-ffi Rust library in release mode
    and copies the resulting DLL to the MicrophoneManager project's
    native runtime folder.

.PARAMETER Configuration
    Build configuration: Debug or Release (default: Release)

.EXAMPLE
    .\build-rust-ffi.ps1
    .\build-rust-ffi.ps1 -Configuration Debug
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Paths
$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $RepoRoot) {
    $RepoRoot = Split-Path -Parent $PSScriptRoot
}

$RustCrateDir = Join-Path $RepoRoot "mic-engine-ffi"
$CSharpProjectDir = Join-Path $RepoRoot "MicrophoneManager"
$NativeDir = Join-Path $CSharpProjectDir "runtimes\win-x64\native"

Write-Host "=== Building Rust FFI Library ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Rust crate: $RustCrateDir"
Write-Host ""

# Verify Rust is installed
if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Error "Rust/Cargo not found. Please install Rust from https://rustup.rs/"
    exit 1
}

# Build Rust library
Push-Location $RustCrateDir
try {
    $buildArgs = @("build", "--target", "x86_64-pc-windows-msvc")
    if ($Configuration -eq "Release") {
        $buildArgs += "--release"
    }

    Write-Host "Running: cargo $($buildArgs -join ' ')" -ForegroundColor Yellow
    & cargo $buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Cargo build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

# Determine source DLL path
$ConfigFolder = if ($Configuration -eq "Release") { "release" } else { "debug" }
$SourceDll = Join-Path $RustCrateDir "target\x86_64-pc-windows-msvc\$ConfigFolder\mic_engine_ffi.dll"

if (-not (Test-Path $SourceDll)) {
    # Try without target triple
    $SourceDll = Join-Path $RustCrateDir "target\$ConfigFolder\mic_engine_ffi.dll"
}

if (-not (Test-Path $SourceDll)) {
    Write-Error "Built DLL not found at expected locations"
    exit 1
}

Write-Host ""
Write-Host "Built DLL: $SourceDll" -ForegroundColor Green

# Create native directory if needed
if (-not (Test-Path $NativeDir)) {
    Write-Host "Creating directory: $NativeDir"
    New-Item -ItemType Directory -Path $NativeDir -Force | Out-Null
}

# Copy DLL
$DestDll = Join-Path $NativeDir "mic_engine_ffi.dll"
Write-Host "Copying to: $DestDll"
Copy-Item -Path $SourceDll -Destination $DestDll -Force

# Also copy to output directories if they exist
$OutputDirs = @(
    (Join-Path $CSharpProjectDir "bin\x64\Debug\net8.0-windows"),
    (Join-Path $CSharpProjectDir "bin\x64\Release\net8.0-windows"),
    (Join-Path $CSharpProjectDir "bin\Debug\net8.0-windows"),
    (Join-Path $CSharpProjectDir "bin\Release\net8.0-windows")
)

foreach ($dir in $OutputDirs) {
    if (Test-Path $dir) {
        $dest = Join-Path $dir "mic_engine_ffi.dll"
        Write-Host "Copying to: $dest"
        Copy-Item -Path $SourceDll -Destination $dest -Force
    }
}

Write-Host ""
Write-Host "=== Rust FFI build complete ===" -ForegroundColor Green
