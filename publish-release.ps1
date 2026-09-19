# Build release artifacts for SnowRunner-Tool (portable zip + Inno setup zip)
param(
    [string]$Version = "1.0.5.4"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$releaseDir = Join-Path $root "SnowRunner-Tool\bin\Release"
$outDir = Join-Path $root "dist"
$portableZip = Join-Path $outDir "SRT-portable.zip"
$setupZip = Join-Path $outDir "SRT_setup.zip"
$setupExe = Join-Path $root "innosetup\setupfiles\SRT_setup.exe"

Write-Host "Building Release $Version..."
dotnet build "SnowRunner-Tool\SnowRunner-Tool.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Remove leftover TFM folders from older builds
Get-ChildItem $releaseDir -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like "net*" } |
    Remove-Item -Recurse -Force

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

Write-Host "Creating portable zip..."
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
$portableStaging = Join-Path $outDir "portable"
New-Item -ItemType Directory -Path $portableStaging | Out-Null
Copy-Item -Path (Join-Path $releaseDir "*") -Destination $portableStaging -Recurse -Force
Get-ChildItem $portableStaging -Filter "*.pdb" -Recurse | Remove-Item -Force
Remove-Item (Join-Path $portableStaging "placement.config") -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $portableStaging "*") -DestinationPath $portableZip -Force
Remove-Item $portableStaging -Recurse -Force

Write-Host "Compiling Inno Setup..."
& ISCC.exe "innosetup\setup.iss"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Creating setup zip..."
if (Test-Path $setupZip) { Remove-Item $setupZip -Force }
Compress-Archive -Path $setupExe -DestinationPath $setupZip -Force

Write-Host ""
Write-Host "Artifacts:"
Write-Host "  $portableZip"
Write-Host "  $setupZip"
Write-Host ""
Write-Host "Create GitHub release example:"
Write-Host "  git tag v$Version"
Write-Host "  git push origin v$Version"
Write-Host "  gh release create v$Version --title `"$Version`" --notes-file Changelog.md `"$portableZip`" `"$setupZip`""
