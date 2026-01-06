[CmdletBinding()]
param(
    [string]$DestinationDir = 'D:\utils',
    [string]$Project = 'MicrophoneManager.WinUI\MicrophoneManager.WinUI.csproj',
    [string]$PublishProfile = 'win-x64-singlefile'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath $PSScriptRoot\..).Path
Set-Location -LiteralPath $repoRoot

Write-Host "Publishing single-file Release..."
dotnet publish (Join-Path $repoRoot $Project) -c Release -p:PublishProfile=$PublishProfile -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Primary expected output (repo root, per publish profile)
$expected = Join-Path $repoRoot 'publish\win-x64-singlefile\MicrophoneManager.WinUI.exe'

# Fallback: SDK default publish location
$fallback = Join-Path $repoRoot 'MicrophoneManager.WinUI\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\MicrophoneManager.WinUI.exe'

$src = if (Test-Path -LiteralPath $expected) { $expected } elseif (Test-Path -LiteralPath $fallback) { $fallback } else { $null }

if (-not $src) {
    Write-Host "Expected output not found. Searched:" -ForegroundColor Yellow
    Write-Host "  $expected" -ForegroundColor Yellow
    Write-Host "  $fallback" -ForegroundColor Yellow

    $hits = Get-ChildItem -Path $repoRoot -Recurse -Filter MicrophoneManager.WinUI.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\publish\\|\\win-x64\\' } |
        Select-Object -First 20

    if ($hits) {
        Write-Host "Found these candidates:" -ForegroundColor Yellow
        $hits | ForEach-Object { Write-Host ("  " + $_.FullName) -ForegroundColor Yellow }
    }

    throw "Publish output MicrophoneManager.WinUI.exe not found."
}

New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
$dst = Join-Path $DestinationDir 'MicrophoneManager.exe'

try {
    Copy-Item -LiteralPath $src -Destination $dst -Force
} catch {
    throw "Failed to copy to $dst (locked/permissions?): $($_.Exception.Message)"
}

Write-Host "Copied: $src"
Write-Host "To:     $dst"