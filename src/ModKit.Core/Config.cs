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

    public string AiProvider { get; set; } = "Anthropic";
    public string? AnthropicKey { get; set; }
    public string? OpenAiKey { get; set; }
    public string AnthropicModel { get; set; } = "claude-sonnet-4-5";
    public string OpenAiModel { get; set; } = "gpt-4o";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "qwen2.5-coder";
    public string ClaudeCodeModel { get; set; } = "sonnet";

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
