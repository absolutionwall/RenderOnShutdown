using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace RenderOnShutdown
{
    public sealed class Config
    {
        private const string AppName = "RenderOnShutdown";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public string MonitoredFolder { get; set; } = "";
        public string Command { get; set; } = "";
        public string FfmpegPath { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public bool RunAtStartup { get; set; } = false;
        public int ShutdownDelaySeconds { get; set; } = 200;
        public bool RenameOriginals { get; set; } = false;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        public static string GetAppDataDir()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetConfigPath()
        {
            return Path.Combine(GetAppDataDir(), "config.json");
        }

        public static Config Load()
        {
            var path = GetConfigPath();
            if (!File.Exists(path)) return new Config();
            try
            {
                var text = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Config>(text, SerializerOptions) ?? new Config();
            }
            catch
            {
                return new Config();
            }
        }

        public void Save()
        {
            var path = GetConfigPath();
            var json = JsonSerializer.Serialize(this, SerializerOptions);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }

        public void ApplyStartupSetting()
        {
#pragma warning disable CA1416
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key == null) return;
            if (RunAtStartup)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    key.SetValue(AppName, "\"" + exe + "\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
#pragma warning restore CA1416
        }
    }
}
