# SnowRunner-Tool Build Script
# WPF-Projekte müssen mit MSBuild gebaut werden - dotnet build kompiliert XAML nicht korrekt.

$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1

if (-not $msbuild) {
    Write-Error "MSBuild nicht gefunden. Bitte Visual Studio mit .NET Desktop-Entwicklung installieren."
    exit 1
}

Write-Host "Baue mit MSBuild: $msbuild"
& $msbuild SnowRunner-Tool\SnowRunner-Tool.csproj /restore /p:Configuration=Debug /v:minimal
