using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MultiTabExplorer;

public class AppConfig
{
    public List<string> SavedPaths { get; set; } = new();
}

public static class ConfigService
{
    private static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MultiTabExplorer");
    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // ignore
        }
        return new AppConfig();
    }

    public static void Save(AppConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // ignore errors to avoid disrupting UI
        }
    }
}
