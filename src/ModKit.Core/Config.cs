using System.Text.Json;

namespace ModKit.Core;

public sealed class Config
{
    public string? GameDir { get; set; }
    public string? WorkspaceDir { get; set; }
    public string? DotnetPath { get; set; }
    public string? LastTemplate { get; set; }
    public Dictionary<string, List<string>> Profiles { get; set; } = new();

    public string? EditorCmd { get; set; }

    public string? ShareDir { get; set; }

    public static string ConfigPath
    {
        get
        {
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(baseDir, "BlockStoryModKit", "config.json");
        }
    }

    public static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath)) ?? new Config();
        }
        catch { }
        return new Config();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
