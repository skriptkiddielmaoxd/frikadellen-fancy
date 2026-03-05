# Packaging script for Frikadellen BAF installer
# Usage: run from repo root in an elevated PowerShell session:
#   .\installer\build-installer.ps1 [-Version v3.0.1]
param(
    [string]$Version = "v3.0.0"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
# Repo root is the parent of the installer script directory
$repoRoot = Resolve-Path (Join-Path $scriptDir "..") | Select-Object -ExpandProperty Path
$staging = Join-Path $repoRoot "installer\staging\app"
$outputDir = Join-Path $repoRoot "installer\output"
$innoExePaths = @("C:\Program Files (x86)\Inno Setup 6\ISCC.exe", "C:\Program Files\Inno Setup 6\ISCC.exe")

Write-Host "Cleaning staging and output..."
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Write-Host "Copying UI publish output..."
$uiPublish = Join-Path $repoRoot "Frikadellen.UI\publish"
if (-Not (Test-Path $uiPublish)) { Write-Warning "UI publish folder not found at $uiPublish. Run 'dotnet publish' first." }
else { Copy-Item -Path (Join-Path $uiPublish "*") -Destination $staging -Recurse -Force }

Write-Host "Copying Rust backend release..."
$rustRelease = Join-Path $repoRoot "target\release"
if (Test-Path $rustRelease) {
    Get-ChildItem -Path $rustRelease -Filter "*.exe" -File | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $staging -Force
    }
} else {
    Write-Warning "Rust release folder not found at $rustRelease. Run 'cargo build --release' first." }

Write-Host "Preparing Inno Setup call..."
$iss = Join-Path $repoRoot "installer\frikadellen_installer.iss"
$inno = $innoExePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-Not $inno) {
    Write-Warning "Inno Setup compiler (ISCC.exe) not found. Install Inno Setup 6 and ensure ISCC.exe is available."
    Write-Host "Staging folder created at: $staging"
    Write-Host "You can compile the installer manually with Inno Setup Compiler (ISCC.exe):"
    Write-Host ('  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "' + $iss + '"')
    exit 0
}

# Provide preprocessor defines so the .iss can reference the staging path and version
$innoArgs = '/DStagingPath="' + ($staging -replace '\\','\\\\') + '" /DAppVersion="' + $Version + '"'
Write-Host "Running ISCC: $inno $innoArgs $iss"
& $inno $innoArgs $iss

if ($LASTEXITCODE -eq 0) { Write-Host "Installer built in installer\output" } else { Write-Error "ISCC failed with exit code $LASTEXITCODE" }
