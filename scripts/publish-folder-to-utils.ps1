[CmdletBinding()]
param(
    [string]$DestinationDir = '~\resources\MicrophoneManager',
    [string]$Project = 'MicrophoneManager.WinUI\MicrophoneManager.WinUI.csproj',
    [string]$PublishProfile = 'win-x64-folder'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath $PSScriptRoot\..).Path
Set-Location -LiteralPath $repoRoot

Write-Host "Publishing Release ($PublishProfile)..." -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot $Project) -c Release -p:PublishProfile=$PublishProfile -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# SDK default publish location for folder-based publishing
$publishDir = Join-Path $repoRoot 'MicrophoneManager.WinUI\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish'

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

# Resolve '~' to the user profile (it is not expanded for all path operations)
if ($DestinationDir -match '^~([\\/]|$)') {
    $DestinationDir = Join-Path $HOME $DestinationDir.Substring(1).TrimStart('\','/')
}

# NOTE: use the folder profile, not single-file. WinUI loads its XAML resources (.pri) from disk\n# next to the exe, and single-file bundling hides them inside the exe, so the app fails at startup.\n#\n# The destination may be a shared folder containing unrelated tools, so never delete
# it or anything in it. Only the files produced by the publish are copied (overwriting
# same-named files, e.g. a previous MicrophoneManager.WinUI.exe).
New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null

try {
    Copy-Item -Path (Join-Path $publishDir '*') -Destination $DestinationDir -Recurse -Force
    Write-Host "Successfully published to: $DestinationDir" -ForegroundColor Green
    Write-Host "Executable: $(Join-Path $DestinationDir 'MicrophoneManager.WinUI.exe')" -ForegroundColor Green
} catch {
    throw "Failed to copy to $DestinationDir (locked/permissions?): $($_.Exception.Message)"
}
