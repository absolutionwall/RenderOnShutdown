Instalador (NSIS):

- He incluido un script NSIS en `installer/RenderOnShutdown.nsi` y un helper `installer/build_installer.ps1`.
- Para generar el instalador en tu máquina:

```powershell
cd C:\workspace\RenderOnShutdown
dotnet publish -c Release -r win-x64 -o publish --self-contained false
cd installer
.\build_installer.ps1
```

- Necesitas instalar NSIS (makensis) y tenerlo en `PATH`.
- El instalador copia la carpeta publicada, crea accesos directos y escribe la entrada en `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` para ejecutar al inicio (puedes quitarla en las opciones).

Git:

 - Inicialicé un repositorio git local y hice un commit inicial en el workspace. Para empujar a un remoto, añade el remote y ejecuta:

```powershell
cd C:\workspace\RenderOnShutdown
git remote add origin <tu-repo-url>
git push -u origin master
```

Reemplaza `<tu-repo-url>` por la URL de tu repositorio (HTTPS o SSH). Si quieres, puedo intentar empujar yo si me proporcionas la URL y las credenciales adecuadas en tu entorno, pero por seguridad generalmente es mejor que ejecutes el `git push` desde tu equipo.
# RenderOnShutdown (WPF)

Aplicación WPF para Windows que detecta archivos de vídeo en una carpeta configurable y, en caso de apagado/reinicio, pregunta si deseas renderizarlos antes de apagar. Si eliges "Sí", cancela el apagado, ejecuta el comando configurado, espera 3 minutos tras finalizar y luego fuerza el apagado.

Requisitos:
- .NET 8 SDK en Windows

Cómo ejecutar en modo desarrollo:

```powershell
cd RenderOnShutdown
dotnet build
dotnet run --project RenderOnShutdown.csproj
```

Archivos de configuración:
- `%APPDATA%\\RenderOnShutdown\\config.json` contiene `MonitoredFolder`, `FfmpegPath`, `OutputFolder`, `Command` y `RunAtStartup`.
- Si `OutputFolder` está configurado, los logs se escribirán en `OutputFolder\\logs`; de lo contrario en `%APPDATA%\\RenderOnShutdown\\logs\\`.

Notas y limitaciones:
- La app intenta cancelar el apagado a través del evento `SystemEvents.SessionEnding` y mostrar un diálogo. El comportamiento final puede depender de políticas de Windows.
- Puedes proporcionar una `Command` que use los placeholders `{input}`, `{output}` y `{ffmpeg}`. Si no proporcionas `Command` pero sí `FfmpegPath`, la app usará un comando por defecto para transcodificar con `libx264`.
- Asegúrate de usar rutas completas o comillas si hay espacios.
