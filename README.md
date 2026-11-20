# RenderOnShutdown

Aplicación WPF para Windows que detecta archivos de vídeo en una carpeta
configurable y, si eliges renderizarlos, ejecuta el comando configurado y luego
apaga la PC.

## Requisitos

- .NET 8 SDK en Windows (para compilar).
- El runtime **.NET 8 Desktop Runtime (Windows)** para ejecutar la build
  `--self-contained false`.

## Compilar y ejecutar (desarrollo)

```powershell
cd C:\workspace\RenderOnShutdown
dotnet build
dotnet run --project RenderOnShutdown.csproj
```

## Publicar (salida Framework-dependent)

```powershell
cd C:\workspace\RenderOnShutdown
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Si quieres una build autosuficiente (no requiere runtime instalado):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## Instalador (NSIS)

Hay un script NSIS en `installer/RenderOnShutdown.nsi` y un helper
`installer/build_installer.ps1`.

1. Instala NSIS (makensis en `PATH`).
2. Publica la app en `publish/` (ver arriba).
3. Compila el instalador:

```powershell
cd C:\workspace\RenderOnShutdown
.\installer\build_installer.ps1
```

El script copia `publish/`, crea accesos directos, escribe `HKCU\...\Run` si
marcaste la casilla y genera `installer\RenderOnShutdown-Setup.exe`.
La versión se toma de `RenderOnShutdown.csproj` y se pasa a NSIS con
`/DPRODUCTVERSION`.

## Configuración

La app lee su configuración de:

```
%APPDATA%\RenderOnShutdown\config.json
```

Puedes usar `config.example.json` como plantilla. Copia ese archivo a la ruta
anterior y ajusta los valores.

| Clave | Descripción |
| --- | --- |
| `MonitoredFolder` | Carpeta de donde se leen los vídeos. |
| `FfmpegPath` | Ruta a `ffmpeg.exe`. |
| `OutputFolder` | Carpeta destino de los renders (incluye `logs\`). |
| `Command` | Plantilla de comando personalizada (tiene prioridad sobre `FfmpegPath`). |
| `RunAtStartup` | Añade/quita la entrada de inicio de sesión (`HKCU\...\Run`). |
| `ShutdownDelaySeconds` | Segundos que Windows espera antes de apagar (muestra aviso nativo). |
| `RenameOriginals` | Si `true`, renombra los originales añadiendo `-AR` tras un render correcto. |

Ejemplo de `Command` con otra herramienta:

```json
{
  "MonitoredFolder": "C:\\Users\\TU_USUARIO\\Videos\\Pendientes",
  "Command": "C:\\ruta\\a\\tu\\render.exe \"{file}\" --out \"{output}\"",
  "RunAtStartup": false
}
```

Si no defines `Command` pero sí `FfmpegPath`, la app usa por defecto:

```text
{ffmpeg} -y -i "{input}" -c:v libx264 -preset veryfast "{output}"
```

Placeholders disponibles en `Command`:

| Placeholder | Valor |
| --- | --- |
| `{input}` / `{file}` | Ruta del archivo de origen. |
| `{output}` | Ruta del archivo de salida. |
| `{ffmpeg}` | Ruta a `ffmpeg.exe`. |
| `{name}` | Nombre del archivo de origen sin extensión. |
| `{timestamp}` | Marca de tiempo `yyyyMMddHHmmss`. |

> Nota: los placeholders se sustituyen tal cual; si una ruta puede contener
> espacios, entrecomilla el placeholder en tu plantilla (`"{input}"`).

## Apagar antes de renderizar (manual)

Al pulsar **Ejecutar ahora** puedes elegir "Sí" en el aviso para apagar la PC al
terminar. La app lanzará `shutdown /s /t <ShutdownDelaySeconds>`; para abortarlo
usa `shutdown /a` durante la cuenta atrás.

## Notas

- En `Flujo de cierre de sesión`: si hay vídeos pendientes y el usuario lo
  confirma, la app bloquea el cierre (`ShutdownBlockReasonCreate`) para
  renderizarlos antes de apagar.
- Rutas en `config.json` deben ir con doble barra invertida (`\\`) o barra
  simple (`/`); `config.example.json` ya está correctamente escapado.
