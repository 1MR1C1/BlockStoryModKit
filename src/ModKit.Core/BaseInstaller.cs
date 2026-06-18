using System.IO.Compression;
using System.Runtime.InteropServices;

namespace ModKit.Core;

public static class BaseInstaller
{

    // Installed if BepInEx core is present and EITHER loader is in place:
    // winhttp.dll (Windows/Proton) or the native-Linux launcher script.
    public static bool IsInstalled(string? gameDir)
        => !string.IsNullOrWhiteSpace(gameDir)
           && Directory.Exists(Path.Combine(gameDir!, "BepInEx", "core"))
           && (File.Exists(Path.Combine(gameDir!, "winhttp.dll"))
               || File.Exists(Path.Combine(gameDir!, "start_modded.sh"))
               || File.Exists(Path.Combine(gameDir!, "run_bepinex.sh")));

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

        // The zip can't carry the unix exec bit, so restore it on the native-Linux launcher scripts.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const UnixFileMode exec = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            foreach (string name in new[] { "start_modded.sh", "run_bepinex.sh" })
            {
                string sh = Path.Combine(gameDir, name);
                if (File.Exists(sh)) File.SetUnixFileMode(sh, exec);
            }
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
