# Build NSIS installer (requires makensis available in PATH)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Push-Location $scriptDir
if (!(Test-Path "../publish")) {
    Write-Error "Publish folder not found. Run `dotnet publish -c Release -r win-x64 -o publish` first."
    exit 1
}

$nsi = Join-Path $scriptDir "RenderOnShutdown.nsi"
if (!(Get-Command makensis -ErrorAction SilentlyContinue)) {
    Write-Error "makensis not found in PATH. Install NSIS (https://nsis.sourceforge.io/) and ensure makensis is in PATH."
    exit 1
}

Write-Host "Building installer using NSIS..."
& makensis $nsi
if ($LASTEXITCODE -ne 0) { Write-Error "makensis failed with exit code $LASTEXITCODE"; exit $LASTEXITCODE }
Write-Host "Installer created: RenderOnShutdown-Setup.exe"
Pop-Location
