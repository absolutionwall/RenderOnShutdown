using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace RenderOnShutdown
{
    public partial class MainWindow : Window
    {
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".wmv"
        };

        private Config _config;
        private readonly string _logDir;
        private readonly List<Process> _runningProcesses = new();
        private readonly object _procLock = new();
        private readonly SemaphoreSlim _logLock = new(1, 1);
        private readonly ObservableCollection<FileItem> _items = new();
        private CancellationTokenSource? _renderCts;
        private bool _shutdownBlocked;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ShutdownBlockReasonCreate(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string reason);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ShutdownBlockReasonDestroy(IntPtr hWnd);

        public MainWindow()
        {
            InitializeComponent();
            _config = Config.Load();
            FilesList.ItemsSource = _items;
            _logDir = ResolveLogDir();
            TryCreateDir(_logDir);
            Closing += MainWindow_Closing;
            if (Application.Current != null)
            {
                Application.Current.SessionEnding += MainWindow_SessionEnding;
            }
            _ = LoadFilesAsync();
        }

        private string ResolveLogDir()
        {
            return !string.IsNullOrWhiteSpace(_config.OutputFolder)
                ? Path.Combine(_config.OutputFolder, "logs")
                : Path.Combine(Config.GetAppDataDir(), "logs");
        }

        private static void TryCreateDir(string dir)
        {
            try { Directory.CreateDirectory(dir); } catch { }
        }

        private void AppendLog(string text)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}";
            var dispatcher = Dispatcher;
            if (dispatcher != null)
            {
                try
                {
                    dispatcher.InvokeAsync(() =>
                    {
                        LogTextBox.AppendText(line);
                        LogTextBox.ScrollToEnd();
                    });
                }
                catch { }
            }
            _ = AppendLogToFileAsync(line);
        }

        private async Task AppendLogToFileAsync(string line)
        {
            await _logLock.WaitAsync().ConfigureAwait(false);
            try
            {
                try { Directory.CreateDirectory(_logDir); } catch { }
                var logfile = Path.Combine(_logDir, $"activity-{DateTime.Now:yyyyMMdd}.log");
                await File.AppendAllTextAsync(logfile, line, Encoding.UTF8).ConfigureAwait(false);
            }
            catch
            {
                // Logging must never crash the application.
            }
            finally
            {
                _logLock.Release();
            }
        }

        private async Task LoadFilesAsync()
        {
            _items.Clear();
            var folder = _config.MonitoredFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                AppendLog("Carpeta monitorizada no configurada o no existe.");
                return;
            }

            var items = await Task.Run(() => BuildItems(folder));
            foreach (var item in items)
            {
                _items.Add(item);
            }
            AppendLog($"{items.Count} archivos encontrados en {folder}");
        }

        private static List<FileItem> BuildItems(string folder)
        {
            var result = new List<FileItem>();
            foreach (var f in DiscoverFiles(folder))
            {
                var item = new FileItem { Path = f, Name = Path.GetFileName(f) };
                try { item.Thumbnail = ThumbnailHelper.GetThumbnail(f, 160, 90); } catch { }
                result.Add(item);
            }
            return result;
        }

        private static List<string> DiscoverFiles(string folder)
        {
            try
            {
                return Directory.EnumerateFiles(folder)
                    .Where(f => VideoExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private async Task StartCommandAsync(List<string> files, CancellationToken token)
        {
            if (files == null || files.Count == 0)
            {
                AppendLog("No hay archivos para procesar.");
                return;
            }

            var ffmpeg = _config.FfmpegPath;
            var outputRoot = !string.IsNullOrWhiteSpace(_config.OutputFolder)
                ? _config.OutputFolder
                : Path.Combine(Config.GetAppDataDir(), "output");
            TryCreateDir(outputRoot);
            var logsDir = Path.Combine(outputRoot, "logs");
            TryCreateDir(logsDir);

            AppendLog($"Iniciando procesamiento de {files.Count} archivos usando ffmpeg: {ffmpeg}");
            Dispatcher.Invoke(() => StatusIndicator.Fill = Brushes.Green);
            try
            {
                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) break;
                    await ProcessFileAsync(file, outputRoot, logsDir, ffmpeg, token);
                }
            }
            finally
            {
                Dispatcher.Invoke(() => StatusIndicator.Fill = Brushes.Gray);
            }
        }

        private async Task ProcessFileAsync(string file, string outputRoot, string logsDir, string ffmpeg, CancellationToken token)
        {
            try
            {
                var fileName = Path.GetFileName(file);
                var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var outputFileName = $"{nameNoExt}--{timestamp}{ext}";
                var outPath = Path.Combine(outputRoot, outputFileName);
                var perFileLog = Path.Combine(logsDir, $"run-{nameNoExt}-{timestamp}.log");

                string exe;
                string args;

                if (!string.IsNullOrWhiteSpace(_config.Command))
                {
                    var template = _config.Command;
                    template = template
                        .Replace("{input}", file)
                        .Replace("{file}", file)
                        .Replace("{output}", outPath)
                        .Replace("{ffmpeg}", ffmpeg ?? "")
                        .Replace("{name}", nameNoExt)
                        .Replace("{timestamp}", timestamp);
                    var parts = SplitCommand(template);
                    exe = parts.executable;
                    args = parts.args;
                }
                else if (!string.IsNullOrWhiteSpace(ffmpeg))
                {
                    exe = ffmpeg;
                    args = $"-y -i \"{file}\" -c:v libx264 -preset veryfast \"{outPath}\"";
                }
                else
                {
                    AppendLog("No hay comando ni ruta a ffmpeg configurada; se omite el procesamiento.");
                    return;
                }

                AppendLog($"Ejecutando: {exe} {args}");
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var p = new Process { StartInfo = psi };
                await using var logStream = new StreamWriter(perFileLog, false, Encoding.UTF8);
                var logStreamLock = new object();

                void WriteOutput(string? line)
                {
                    if (line == null) return;
                    AppendLog(line);
                    lock (logStreamLock) { logStream.WriteLine(line); logStream.Flush(); }
                }

                p.OutputDataReceived += (s, e) => WriteOutput(e.Data);
                p.ErrorDataReceived += (s, e) => WriteOutput(e.Data);

                lock (_procLock) { _runningProcesses.Add(p); }
                try
                {
                    if (!p.Start())
                    {
                        AppendLog($"No se pudo iniciar el proceso para {fileName}.");
                        return;
                    }
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    await p.WaitForExitAsync(token);
                }
                finally
                {
                    lock (_procLock) { _runningProcesses.Remove(p); }
                }

                AppendLog($"Procesado {fileName} finalizado con código {p.ExitCode}. Salida en: {outPath}");

                if (p.ExitCode == 0 && _config.RenameOriginals)
                {
                    try
                    {
                        var srcDir = Path.GetDirectoryName(file) ?? "";
                        var renderedName = $"{nameNoExt}-AR{ext}";
                        var renderedPath = Path.Combine(srcDir, renderedName);
                        if (File.Exists(renderedPath))
                        {
                            renderedName = $"{nameNoExt}-AR-{timestamp}{ext}";
                            renderedPath = Path.Combine(srcDir, renderedName);
                        }
                        File.Move(file, renderedPath);
                        AppendLog($"Archivo origen renombrado a: {renderedName}");
                    }
                    catch (Exception rex)
                    {
                        AppendLog($"Error al renombrar archivo origen {file}: {rex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("Procesamiento cancelado.");
            }
            catch (Exception ex)
            {
                AppendLog($"Error al procesar {file}: {ex.Message}");
            }
        }

        private void BtnOptions_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_config) { Owner = this };
            if (win.ShowDialog() == true)
            {
                _config = Config.Load();
                _ = LoadFilesAsync();
            }
        }

        private async void BtnRunNow_Click(object sender, RoutedEventArgs e)
        {
            if (_renderCts != null)
            {
                AppendLog("Ya hay un renderizado en curso.");
                return;
            }

            var folder = _config.MonitoredFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                AppendLog("Carpeta de origen no configurada o no existe.");
                MessageBox.Show(this, "La carpeta de origen no está configurada o no existe.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var files = DiscoverFiles(folder);
            if (files.Count == 0)
            {
                AppendLog("No hay archivos para procesar ahora mismo.");
                MessageBox.Show(this, "No se encontraron archivos de vídeo en la carpeta de origen.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var shutdownChoice = MessageBox.Show(this,
                $"Se encontraron {files.Count} archivos. ¿Deseas apagar la PC al finalizar el renderizado?",
                "Ejecutar renderizado", MessageBoxButton.YesNo, MessageBoxImage.Question);

            await RunWithShutdownAsync(files, shutdownChoice == MessageBoxResult.Yes);
        }

        private async Task RunWithShutdownAsync(List<string> files, bool shutdownWhenDone)
        {
            _renderCts = new CancellationTokenSource();
            try
            {
                AppendLog("Usuario inició ejecución manual (Ejecutar ahora).");
                await StartCommandAsync(files, _renderCts.Token);
                AppendLog("Ejecución manual finalizada.");

                if (shutdownWhenDone)
                {
                    await ShutdownWithDelayAsync();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error al iniciar ejecución manual: {ex.Message}");
            }
            finally
            {
                _renderCts.Dispose();
                _renderCts = null;
                if (_shutdownBlocked)
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    ShutdownBlockReasonDestroy(hwnd);
                    _shutdownBlocked = false;
                }
            }
        }

        private async Task ShutdownWithDelayAsync()
        {
            var seconds = Math.Max(1, _config.ShutdownDelaySeconds);
            AppendLog($"Se apagara la PC en {seconds} segundos. Usa 'shutdown /a' para abortarlo.");
            try
            {
                var psi = new ProcessStartInfo("shutdown", $"/s /t {seconds}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };
                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    AppendLog("No se pudo lanzar el comando shutdown.");
                    PlayNotificationSound();
                    return;
                }
                await proc.WaitForExitAsync().ConfigureAwait(false);
                if (proc.ExitCode != 0)
                {
                    AppendLog($"shutdown devolvio el codigo {proc.ExitCode}.");
                    PlayNotificationSound();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error al intentar apagar: {ex.Message}");
                PlayNotificationSound();
            }
        }

        private void PlayNotificationSound()
        {
            try
            {
                System.Media.SystemSounds.Beep.Play();
                AppendLog("Sonido de notificación reproducido.");
            }
            catch (Exception ex)
            {
                AppendLog($"Error al reproducir sonido: {ex.Message}");
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            try { _renderCts?.Cancel(); } catch { }
            KillAllRunningProcesses();
        }

        private async void MainWindow_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            // Windows is ending the session (shutdown/restart/logoff). If there is work to do
            // and the user asked us to render, block the shutdown so we can finish first.
            var folder = _config.MonitoredFolder;
            if (_renderCts != null || _shutdownBlocked)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return;
            }

            var files = DiscoverFiles(folder);
            if (files.Count == 0)
            {
                return;
            }

            var choice = MessageBox.Show(this,
                $"Se va a cerrar Windows y hay {files.Count} archivos pendientes de renderizar. " +
                "¿Cancelar el apagado y renderizarlos primero?",
                "RenderOnShutdown", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (choice != MessageBoxResult.Yes)
            {
                return;
            }

            e.Cancel = true;
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (ShutdownBlockReasonCreate(hwnd, "Renderizando videos pendientes..."))
            {
                _shutdownBlocked = true;
            }
            _ = RunWithShutdownAsync(files, shutdownWhenDone: true);
        }

        private (string executable, string args) SplitCommand(string cmd)
        {
            cmd = cmd.Trim();
            if (cmd.StartsWith("\""))
            {
                var end = cmd.IndexOf('"', 1);
                if (end > 0)
                {
                    var exe = cmd.Substring(1, end - 1);
                    var args = cmd.Length > end + 1 ? cmd.Substring(end + 1).Trim() : "";
                    return (exe, args);
                }
            }
            var sp = cmd.Split(' ', 2);
            var exe2 = sp[0];
            var args2 = sp.Length > 1 ? sp[1] : "";
            return (exe2, args2);
        }

        private void KillAllRunningProcesses()
        {
            List<Process> snapshot;
            lock (_procLock)
            {
                if (_runningProcesses.Count == 0) return;
                snapshot = new List<Process>(_runningProcesses);
                _runningProcesses.Clear();
            }

            AppendLog($"Cerrando {snapshot.Count} procesos en marcha...");
            foreach (var proc in snapshot)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        try { proc.Kill(entireProcessTree: true); }
                        catch { try { proc.Kill(); } catch { } }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Error al terminar proceso: {ex.Message}");
                }
            }
        }
    }

    public class FileItem : INotifyPropertyChanged
    {
        public string Path { get; init; } = "";
        public string Name { get; init; } = "";
        private BitmapSource? _thumbnail;
        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
