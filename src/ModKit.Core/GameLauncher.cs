using System.Diagnostics;

namespace ModKit.Core;

public static class GameLauncher
{

    public static void LaunchViaSteam()
    {
        var psi = new ProcessStartInfo
        {
            FileName = $"steam://run/{GamePaths.AppId}",
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    public static void OpenFolder(string path)
    {
        if (!Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    public static void OpenPath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    public static void OpenInEditor(string path, string? editorCmd)
    {
        if (string.IsNullOrWhiteSpace(editorCmd)) { OpenPath(path); return; }
        try
        {
            string? folder = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            var psi = new ProcessStartInfo { FileName = editorCmd!, UseShellExecute = false };
            if (folder != null) psi.ArgumentList.Add(folder);
            psi.ArgumentList.Add(path);
            Process.Start(psi);
        }
        catch { OpenPath(path); }
    }

    public static string? FindEditor()
    {
        foreach (string c in new[] { "code", "code-insiders", "codium" })
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = c, Arguments = "--version", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p == null) continue;
                p.WaitForExit(2500);
                if (p.HasExited && p.ExitCode == 0) return c;
            }
            catch { }
        }
        return null;
    }
}
