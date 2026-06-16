namespace ModKit.Core;

public static class GamePaths
{
    public const string AppId = "270110";
    public const string GameFolderName = "BlockStory";

    public static string PluginsDir(string gameDir) => Path.Combine(gameDir, "BepInEx", "plugins");
    public static string ManagedDir(string gameDir) => Path.Combine(gameDir, "Block Story_Data", "Managed");
    public static string LogPath(string gameDir) => Path.Combine(gameDir, "BepInEx", "LogOutput.log");

    public static bool IsValidGameDir(string? dir)
        => !string.IsNullOrWhiteSpace(dir) && Directory.Exists(Path.Combine(dir!, "BepInEx"));

    public static bool LooksLikeGameDir(string? dir)
        => !string.IsNullOrWhiteSpace(dir) && Directory.Exists(Path.Combine(dir!, "Block Story_Data"));

    public static bool HasLoader(string gameDir) => Directory.Exists(Path.Combine(gameDir, "BepInEx", "core"));

    public static string? AutoDetect()
    {
        foreach (string lib in SteamLibraries())
        {
            string candidate = Path.Combine(lib, "steamapps", "common", GameFolderName);
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var roots = new List<string>();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (string p in new[]
        {
            Path.Combine(home, ".steam", "steam"),
            Path.Combine(home, ".local", "share", "Steam"),
            Path.Combine(home, ".steam", "root"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
        })
            if (Directory.Exists(p)) roots.Add(p);

        var all = new List<string>(roots);

        foreach (string root in roots)
        {
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (string line in File.ReadAllLines(vdf))
            {
                int i = line.IndexOf("\"path\"", StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                int q1 = line.IndexOf('"', i + 6);
                int q2 = q1 >= 0 ? line.IndexOf('"', q1 + 1) : -1;
                if (q1 >= 0 && q2 > q1)
                    all.Add(line.Substring(q1 + 1, q2 - q1 - 1).Replace("\\\\", "\\"));
            }
        }
        return all.Distinct();
    }
}
