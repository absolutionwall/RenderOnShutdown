using System;
using System.IO;
using System.Text.Json;

namespace RenderOnShutdown
{
    public class Config
    {
        public string MonitoredFolder { get; set; } = "";
        public string Command { get; set; } = "";
        public string FfmpegPath { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public bool RunAtStartup { get; set; } = false;

        public static string GetConfigPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RenderOnShutdown");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.json");
        }

        public static Config Load()
        {
            var path = GetConfigPath();
            if (!File.Exists(path)) return new Config();
            try
            {
                var t = File.ReadAllText(path);
                return JsonSerializer.Deserialize<Config>(t) ?? new Config();
            }
            catch
            {
                return new Config();
            }
        }

        public void Save()
        {
            var path = GetConfigPath();
            var t = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, t);
        }
    }
}
