using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.Linq;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Controls;

namespace RenderOnShutdown
{
    public partial class MainWindow : Window
    {
        private Config _config;
        private string _logDir;
        private readonly System.Collections.Generic.List<Process> _runningProcesses = new();
        private readonly object _procLock = new();
        private ObservableCollection<FileItem> _items = new();

        public MainWindow()
        {
            InitializeComponent();
            _config = Config.Load();
            FilesList.ItemsSource = _items;

            _logDir = !string.IsNullOrWhiteSpace(_config.OutputFolder) ? Path.Combine(_config.OutputFolder, "logs") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RenderOnShutdown", "logs");
            Directory.CreateDirectory(_logDir);

            LoadFilesAsync();

            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // If there are running processes, attempt to terminate them so ffmpeg doesn't continue after the app exits.
            KillAllRunningProcesses();
        }

        private void AppendLog(string text)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}{Environment.NewLine}";
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(line);
                LogTextBox.ScrollToEnd();
            });
            var logfile = Path.Combine(_logDir, $"activity-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(logfile, line, Encoding.UTF8);
        }

        private async void LoadFilesAsync()
        {
            _items.Clear();
            var folder = _config.MonitoredFolder;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
            var exts = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv" };
            var files = Directory.EnumerateFiles(folder).Where(f => exts.Contains(Path.GetExtension(f).ToLower())).ToList();
            foreach (var f in files)
            {
                BitmapSource? thumb = null;
                try { thumb = ThumbnailHelper.GetThumbnail(f, 160, 90); } catch { }
                _items.Add(new FileItem { Path = f, Name = Path.GetFileName(f), Thumbnail = thumb });
            }
            AppendLog($"Cargados {files.Count} archivos desde {folder}");
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            // Removed: folder selection moved to Options.
        }

        private void BtnOptions_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_config);
            if (win.ShowDialog() == true)
            {
                _config = Config.Load();
                LoadFilesAsync();
                _logDir = !string.IsNullOrWhiteSpace(_config.OutputFolder) ? Path.Combine(_config.OutputFolder, "logs") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RenderOnShutdown", "logs");
                Directory.CreateDirectory(_logDir);
            }
        }



        private async void BtnRunNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = _config.MonitoredFolder;
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    AppendLog("Carpeta monitorizada no configurada o no existe.");
                    System.Windows.MessageBox.Show(this, "La carpeta de origen no está configurada o no existe.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var exts = new[] { ".mp4", ".mov", ".avi", ".mkv", ".wmv" };
                var files = Directory.EnumerateFiles(folder).Where(f => exts.Contains(Path.GetExtension(f).ToLower())).ToList();
                if (files.Count == 0)
                {
                    AppendLog("No hay archivos para procesar ahora mismo.");
                    System.Windows.MessageBox.Show(this, "No se encontraron archivos de vídeo en la carpeta de origen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Preguntar primero si desea apagar al finalizar
                var shutdownChoice = System.Windows.MessageBox.Show(this, $"Se encontraron {files.Count} archivos. ¿Deseas apagar la PC al finalizar el renderizado?", "Ejecutar renderizado", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                AppendLog("Usuario inició ejecución manual (Ejecutar ahora).");
                await StartCommandAsync(files);
                AppendLog("Ejecución manual finalizada.");
                
                if (shutdownChoice == System.Windows.MessageBoxResult.Yes)
                {
                    AppendLog("Usuario eligió apagar PC. Esperando 200 segundos antes de proceder...");
                    await Task.Delay(TimeSpan.FromSeconds(200));
                    AppendLog("Apagando PC ahora.");
                    try
                    {
                        Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { CreateNoWindow = true, UseShellExecute = false });
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Error al intentar apagar: {ex.Message}");
                    }
                }
                else
                {
                    AppendLog("Usuario eligió no apagar. Reproduciendo sonido de aviso.");
                    PlayNotificationSound();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"Error al iniciar ejecución manual: {ex.Message}");
            }
        }

        private async Task StartCommandAsync(System.Collections.Generic.List<string> files)
        {
            if (files == null || files.Count == 0)
            {
                AppendLog("No hay archivos para procesar.");
                return;
            }

            var ffmpeg = _config.FfmpegPath;
            var outputRoot = !string.IsNullOrWhiteSpace(_config.OutputFolder) ? _config.OutputFolder : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RenderOnShutdown", "output");
            Directory.CreateDirectory(outputRoot);
            var logsDir = Path.Combine(outputRoot, "logs");
            Directory.CreateDirectory(logsDir);

            AppendLog($"Iniciando procesamiento de {files.Count} archivos usando ffmpeg: {ffmpeg}");
            Dispatcher.Invoke(() => StatusIndicator.Fill = System.Windows.Media.Brushes.Green);

            foreach (var file in files)
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
                        // Use command template; support placeholders {input},{output},{ffmpeg},{file}
                        var template = _config.Command;
                        template = template.Replace("{input}", file).Replace("{file}", file).Replace("{output}", outPath).Replace("{ffmpeg}", ffmpeg ?? "").Replace("{name}", nameNoExt).Replace("{timestamp}", timestamp);
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
                        break;
                    }

                    AppendLog($"Ejecutando: {exe} {args}");
                    using var logStream = new StreamWriter(perFileLog, false, Encoding.UTF8);

                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };

                    var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                    lock (_procLock)
                    {
                        _runningProcesses.Add(p);
                    }
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) { AppendLog(e.Data); logStream.WriteLine(e.Data); logStream.Flush(); } };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) { AppendLog(e.Data); logStream.WriteLine(e.Data); logStream.Flush(); } };

                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    await Task.Run(() => p.WaitForExit());
                    AppendLog($"Procesado {fileName} finalizado con código {p.ExitCode}. Salida en: {outPath}");
                    lock (_procLock)
                    {
                        _runningProcesses.Remove(p);
                    }

                    // Si el proceso finalizó correctamente, renombrar archivo de origen añadiendo sufijo -AR
                    if (p.ExitCode == 0)
                    {
                        try
                        {
                            var srcDir = Path.GetDirectoryName(file) ?? "";
                            var renderedName = $"{nameNoExt}-AR{ext}";
                            var renderedPath = Path.Combine(srcDir, renderedName);
                            // Si ya existe un archivo con ese nombre, añade timestamp para evitar colisión
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
                    // ensure we remove process from running list in case exit code wasn't 0
                    lock (_procLock)
                    {
                        if (_runningProcesses.Contains(p))
                            _runningProcesses.Remove(p);
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Error al procesar {file}: {ex.Message}");
                }
            }

            Dispatcher.Invoke(() => StatusIndicator.Fill = System.Windows.Media.Brushes.Gray);
        }

        private void KillAllRunningProcesses()
        {
            lock (_procLock)
            {
                if (_runningProcesses.Count == 0) return;
                AppendLog($"Cerrando {_runningProcesses.Count} procesos en marcha...");
                foreach (var proc in _runningProcesses.ToArray())
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            try
                            {
                                proc.Kill(entireProcessTree: true);
                            }
                            catch
                            {
                                try { proc.Kill(); } catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Error al terminar proceso: {ex.Message}");
                    }
                }
                _runningProcesses.Clear();
            }
        }

        private void PlayNotificationSound()
        {
            try
            {
                // Reproducir el sonido de notificación del sistema
                System.Media.SystemSounds.Beep.Play();
                AppendLog("Sonido de notificación reproducido.");
            }
            catch (Exception ex)
            {
                AppendLog($"Error al reproducir sonido de notificación: {ex.Message}");
            }
        }

        private (string executable, string args) SplitCommand(string cmd)
        {
            // Very simple splitter: if starts with quoted path, extract it
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
    }

    public class FileItem : INotifyPropertyChanged
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        private BitmapSource? _thumbnail;
        public BitmapSource? Thumbnail { get => _thumbnail; set { _thumbnail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
