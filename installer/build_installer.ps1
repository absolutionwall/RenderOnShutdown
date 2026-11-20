# Build NSIS installer for RenderOnShutdown (requires makensis in PATH)
$ErrorActionPreference = "Stop"

$scriptDir  = $PSScriptRoot
$repoRoot   = Split-Path -Parent $scriptDir
$publishDir = Join-Path $repoRoot "publish"
$nsi        = Join-Path $scriptDir "RenderOnShutdown.nsi"
$csproj     = Join-Path $repoRoot "RenderOnShutdown.csproj"

if (!(Test-Path $publishDir)) {
    Write-Error "Publish folder not found: $publishDir. Run 'dotnet publish -c Release -r win-x64 --self-contained false -o publish' first."
    exit 1
}

if (!(Get-Command makensis -ErrorAction SilentlyContinue)) {
    Write-Error "makensis not found in PATH. Install NSIS (https://nsis.sourceforge.io/) and ensure makensis is in PATH."
    exit 1
}

# Read <Version> from the csproj (simple regex; no XML namespace in SDK-style projects).
$version = "1.0.0"
if (Test-Path $csproj) {
    $match = Select-String -Path $csproj -Pattern '<Version>\s*([^<]+)\s*</Version>' | Select-Object -First 1
    if ($match) { $version = $match.Matches[0].Groups[1].Value.Trim() }
}

Write-Host "Building installer using NSIS (version $version)..."
Push-Location $scriptDir
try {
    & makensis "/DPRODUCTVERSION=$version" $nsi
    if ($LASTEXITCODE -ne 0) {
        Write-Error "makensis failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

Write-Host "Installer created: RenderOnShutdown-Setup.exe"
