using System.Reflection;

namespace ModKit.Core;

public sealed class ModUpdate
{
    public string Name = "";
    public string? InstalledVersion;
    public string? AvailableVersion;
    public string SourcePath = "";
}

public static class UpdateCheck
{
    public static List<ModUpdate> Find(string gameDir, string shareDir)
    {
        var result = new List<ModUpdate>();
        if (!GamePaths.IsValidGameDir(gameDir) || !Directory.Exists(shareDir)) return result;

        var share = new Dictionary<string, (string path, Version ver)>(StringComparer.OrdinalIgnoreCase);
        foreach (string f in Directory.EnumerateFiles(shareDir, "*.dll", SearchOption.AllDirectories))
        {
            string name = Path.GetFileNameWithoutExtension(f);
            Version v = ReadVersion(f) ?? new Version(0, 0);
            if (!share.TryGetValue(name, out var cur) || v > cur.ver) share[name] = (f, v);
        }

        foreach (var m in ModManager.List(gameDir))
        {
            if (!share.TryGetValue(m.Name, out var s)) continue;
            Version installed = ParseVer(m.Version);
            if (s.ver > installed)
                result.Add(new ModUpdate
                {
                    Name = m.Name,
                    InstalledVersion = m.Version ?? "0.0",
                    AvailableVersion = s.ver.ToString(),
                    SourcePath = s.path,
                });
        }
        return result.OrderBy(u => u.Name).ToList();
    }

    static Version? ReadVersion(string path)
    {
        try { return AssemblyName.GetAssemblyName(path).Version; }
        catch { return null; }
    }

    static Version ParseVer(string? s) => Version.TryParse(s, out var v) ? v : new Version(0, 0);
}
