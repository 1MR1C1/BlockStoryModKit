using System.IO.Compression;
using System.Text;

namespace ModKit.Core;

public static class BugReport
{
    public static string Create(string gameDir)
    {
        string outZip = Path.Combine(gameDir, $"bugreport-{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        using (var zip = ZipFile.Open(outZip, ZipArchiveMode.Create))
        {
            string log = GamePaths.LogPath(gameDir);
            if (File.Exists(log))
            {
                try { zip.CreateEntryFromFile(log, "LogOutput.log"); }
                catch { CopyTextEntry(zip, "LogOutput.log", SafeRead(log)); }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Block Story Mod Kit — bug report");
            sb.AppendLine("Created: " + DateTime.Now.ToString("u"));
            sb.AppendLine("Game dir: " + gameDir);
            sb.AppendLine("OS: " + Environment.OSVersion + "  (" + System.Runtime.InteropServices.RuntimeInformation.OSArchitecture + ")");
            sb.AppendLine("Framework installed: " + BaseInstaller.IsInstalled(gameDir));
            sb.AppendLine();
            sb.AppendLine("Installed mods:");
            foreach (var m in ModManager.List(gameDir))
                sb.AppendLine($"  [{(m.Enabled ? "on " : "off")}] {m.Name}  {m.Version}");
            CopyTextEntry(zip, "mods.txt", sb.ToString());
        }
        return outZip;
    }

    static void CopyTextEntry(ZipArchive zip, string name, string text)
    {
        var e = zip.CreateEntry(name);
        using var w = new StreamWriter(e.Open());
        w.Write(text);
    }

    static string SafeRead(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var r = new StreamReader(fs);
            return r.ReadToEnd();
        }
        catch (Exception e) { return "(couldn't read log: " + e.Message + ")"; }
    }
}
