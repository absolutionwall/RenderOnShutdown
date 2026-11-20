param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Bump = "patch"
)

$ErrorActionPreference = "Stop"
$rootDir   = Resolve-Path "$PSScriptRoot\.."
$verFile   = Join-Path $rootDir "VERSION"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

# Leer versión actual desde VERSION
$oldVer = ([System.IO.File]::ReadAllText($verFile)).Trim()
$nums   = $oldVer.Split('.')
$major  = [int]$nums.Item(0)
$minor  = [int]$nums.Item(1)
$patch  = [int]$nums.Item(2)

# Incrementar +1 según el tipo elegido
switch ($Bump) {
    "major" { $major++; $minor = 0; $patch = 0 }
    "minor" { $minor++; $patch = 0 }
    "patch" { $patch++ }
}
$newVer = "$major.$minor.$patch"
Write-Host "=== SUBIENDO VERSION: $oldVer -> $newVer ===" -ForegroundColor Cyan

# Pedir descripción corta (opcional)
$notas = Read-Host "Descripcion de v$newVer (Enter para mensaje por defecto)"
if ([string]::IsNullOrWhiteSpace($notas)) {
    $notas = "Release v$newVer"
}

# Guardar nuevo número en VERSION (sin BOM)
[System.IO.File]::WriteAllText($verFile, $newVer, $utf8NoBom)

# Guardar cambios en Git, etiquetar y subir a GitHub
Set-Location $rootDir
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
git add .
git commit -m "release(v$newVer): $notas"
git tag -a "v$newVer" -m "$notas"
git push origin $branch --tags

Write-Host "`n[OK] Version v$newVer publicada en GitHub!" -ForegroundColor Green