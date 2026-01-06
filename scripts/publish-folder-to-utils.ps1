[CmdletBinding()]
param(
    [string]$DestinationDir = 'D:\utils\MicrophoneManager',
    [string]$Project = 'MicrophoneManager.WinUI\MicrophoneManager.WinUI.csproj',
    [string]$PublishProfile = 'win-x64-folder'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath $PSScriptRoot\..).Path
Set-Location -LiteralPath $repoRoot

Write-Host "Publishing folder-based Release..." -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot $Project) -c Release -p:PublishProfile=$PublishProfile -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# SDK default publish location for folder-based publishing
$publishDir = Join-Path $repoRoot 'MicrophoneManager.WinUI\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish'

if (-not (Test-Path -LiteralPath $publishDir)) {
    Write-Host "Expected publish folder not found: $publishDir" -ForegroundColor Yellow
    
    $hits = Get-ChildItem -Path (Join-Path $repoRoot 'MicrophoneManager.WinUI\bin') -Recurse -Directory -Filter publish -ErrorAction SilentlyContinue |
        Select-Object -First 10
    
    if ($hits) {
        Write-Host "Found these publish folders:" -ForegroundColor Yellow
        $hits | ForEach-Object { Write-Host ("  " + $_.FullName) -ForegroundColor Yellow }
    }
    
    throw "Publish folder not found."
}

# Remove existing destination folder if it exists
if (Test-Path -LiteralPath $DestinationDir) {
    Write-Host "Removing existing folder: $DestinationDir" -ForegroundColor Yellow
    Remove-Item -LiteralPath $DestinationDir -Recurse -Force
}

# Create parent directory if needed
$parentDir = Split-Path -Parent $DestinationDir
New-Item -ItemType Directory -Path $parentDir -Force | Out-Null

# Copy entire publish folder
try {
    Copy-Item -LiteralPath $publishDir -Destination $DestinationDir -Recurse -Force
    Write-Host "Successfully published to: $DestinationDir" -ForegroundColor Green
    Write-Host "Executable: $(Join-Path $DestinationDir 'MicrophoneManager.WinUI.exe')" -ForegroundColor Green
} catch {
    throw "Failed to copy to $DestinationDir (locked/permissions?): $($_.Exception.Message)"
}