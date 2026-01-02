# Build script using VS2019 Build Tools
$vsPath = "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools"
$vcvarsPath = Join-Path $vsPath "VC\Auxiliary\Build\vcvars64.bat"

if (Test-Path $vcvarsPath) {
    Write-Host "Using VS2019 Build Tools"

    # Create a temp batch file to set up environment and build
    $tempBat = [System.IO.Path]::GetTempFileName() + ".bat"
    @"
@echo off
call "$vcvarsPath" >nul 2>&1
cd /d D:\repos\local\windows-microphone-manager\mic-manager-rs
cargo build --release
"@ | Out-File -FilePath $tempBat -Encoding ASCII

    # Run the batch file
    cmd /c $tempBat

    # Clean up
    Remove-Item $tempBat -ErrorAction SilentlyContinue
} else {
    Write-Host "VS2019 Build Tools not found"
    exit 1
}
