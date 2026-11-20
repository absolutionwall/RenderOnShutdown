@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

echo ===================================================
echo  Publicar Nueva Version en GitHub
echo ===================================================
echo.
echo   [1] PATCH (0.1.0 -^> 0.1.1) - Arreglos menores
echo   [2] MINOR (0.1.0 -^> 0.2.0) - Nuevas funciones
echo   [3] MAJOR (0.1.0 -^> 1.0.0) - Cambios mayores
echo.
set /p "OPCION=Elige una opcion [1-3] (Enter = 1): "

set "BUMP=patch"
if "%OPCION%"=="2" set "BUMP=minor"
if "%OPCION%"=="3" set "BUMP=major"

echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%scripts\release.ps1" -Bump %BUMP%

if not "%ERRORLEVEL%"=="0" (
    echo.
    echo [ERROR] Fallo la publicacion. Revisa el mensaje de arriba.
)
pause