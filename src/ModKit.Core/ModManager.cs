using System.IO.Compression;
using System.Reflection;

namespace ModKit.Core;

public sealed class InstalledMod
{
    public string Name { get; init; } = "";
    public string FilePath { get; set; } = "";
    public bool Enabled { get; set; }
    public string? Version { get; init; }
    public bool IsCore => Name.Equals("BlockStoryCore", StringComparison.OrdinalIgnoreCase);
}

public static class ModManager
{
    private const string DisabledSuffix = ".disabled";

    public static List<InstalledMod> List(string gameDir)
    {
        var result = new List<InstalledMod>();
        string dir = GamePaths.PluginsDir(gameDir);
        if (!Directory.Exists(dir)) return result;

        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string f in Directory.GetFiles(dir, "*.dll"))
        {
            string name = Path.GetFileNameWithoutExtension(f);
            active.Add(name);
            result.Add(new InstalledMod { Name = name, FilePath = f, Enabled = true, Version = ReadVersion(f) });
        }

        foreach (string f in Directory.GetFiles(dir, "*.dll" + DisabledSuffix))
        {
            string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f));
            if (active.Contains(name)) continue;
            result.Add(new InstalledMod { Name = name, FilePath = f, Enabled = false, Version = ReadVersion(f) });
        }

        result.Sort((a, b) =>
        {
            if (a.IsCore != b.IsCore) return a.IsCore ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    public static void SetEnabled(InstalledMod mod, bool enabled)
    {
        bool isDisabledName = mod.FilePath.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
        string target = enabled
            ? (isDisabledName ? mod.FilePath[..^DisabledSuffix.Length] : mod.FilePath)
            : (isDisabledName ? mod.FilePath : mod.FilePath + DisabledSuffix);

        if (!File.Exists(mod.FilePath) && File.Exists(target)) {   }
        else if (!string.Equals(mod.FilePath, target, StringComparison.OrdinalIgnoreCase) && File.Exists(mod.FilePath))
            MoveOver(mod.FilePath, target);

        mod.FilePath = target;
        mod.Enabled = enabled;
    }

    public static void Install(string gameDir, string dllPath)
    {
        string plugins = GamePaths.PluginsDir(gameDir);
        Directory.CreateDirectory(plugins);
        string dest = Path.Combine(plugins, Path.GetFileName(dllPath));
        File.Copy(dllPath, dest, overwrite: true);
        string disabledTwin = dest + DisabledSuffix;
        if (File.Exists(disabledTwin)) File.Delete(disabledTwin);
    }

    public static string BackupPlugins(string gameDir)
    {
        string plugins = GamePaths.PluginsDir(gameDir);
        if (!Directory.Exists(plugins)) throw new DirectoryNotFoundException("No plugins folder to back up.");
        string dest = Path.Combine(gameDir, $"mods-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        ZipFile.CreateFromDirectory(plugins, dest);
        return dest;
    }

    public static void Uninstall(InstalledMod mod)
    {
        if (File.Exists(mod.FilePath)) File.Delete(mod.FilePath);
    }

    public static int InstallFromFolder(string gameDir, string folder)
    {
        int n = 0;
        foreach (string dll in Directory.GetFiles(folder, "*.dll"))
        {
            Install(gameDir, dll);
            n++;
        }
        return n;
    }

    public static int InstallFromZip(string gameDir, string zipPath)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "modkit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            ZipFile.ExtractToDirectory(zipPath, tmp);
            int n = 0;
            foreach (string dll in Directory.GetFiles(tmp, "*.dll", SearchOption.AllDirectories))
            {
                Install(gameDir, dll);
                n++;
            }
            return n;
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    private static string? ReadVersion(string path)
    {
        try { return AssemblyName.GetAssemblyName(path).Version?.ToString(); }
        catch { return null; }
    }

    public static void ApplyProfile(string gameDir, IEnumerable<string> enabledNames)
    {
        var set = new HashSet<string>(enabledNames, StringComparer.OrdinalIgnoreCase);
        foreach (InstalledMod m in List(gameDir))
        {
            bool want = m.IsCore || set.Contains(m.Name);
            if (m.Enabled != want) SetEnabled(m, want);
        }
    }

    private static void MoveOver(string from, string to)
    {
        if (File.Exists(to)) File.Delete(to);
        File.Move(from, to);
    }
}
