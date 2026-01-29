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
