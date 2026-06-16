using System.IO.Compression;

namespace ModKit.Core;

public static class BaseInstaller
{

    public static bool IsInstalled(string? gameDir)
        => !string.IsNullOrWhiteSpace(gameDir)
           && File.Exists(Path.Combine(gameDir!, "winhttp.dll"))
           && Directory.Exists(Path.Combine(gameDir!, "BepInEx", "core"));

    public static int Install(string gameDir, Stream baseZip)
    {
        int n = 0;
        using var zip = new ZipArchive(baseZip, ZipArchiveMode.Read, leaveOpen: true);
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            string rel = entry.FullName.Replace('\\', '/');

            if (!rel.Contains('/') && rel.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;

            if (rel.Equals("BepInEx/config/BepInEx.cfg", StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(gameDir, "BepInEx", "config", "BepInEx.cfg"))) continue;

            string dest = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
            n++;
        }
        return n;
    }

    public static string ReadText(Stream baseZip, string entryName)
    {
        using var zip = new ZipArchive(baseZip, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry? e = zip.GetEntry(entryName);
        if (e == null) return $"(guide '{entryName}' not found)";
        using var r = new StreamReader(e.Open());
        return r.ReadToEnd();
    }
}
