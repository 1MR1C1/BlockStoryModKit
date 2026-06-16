using System.Diagnostics;

namespace ModKit.Core;

public static class BuildRunner
{

    public static string? FindDotnet()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        foreach (string c in new[]
        {
            "dotnet",
            Path.Combine(home, ".dotnet", "dotnet"),
            "/usr/bin/dotnet", "/usr/local/bin/dotnet",
            "/usr/local/share/dotnet/dotnet", "/usr/share/dotnet/dotnet", "/snap/bin/dotnet",
            Path.Combine(pf, "dotnet", "dotnet.exe"),
            Path.Combine(local, "Microsoft", "dotnet", "dotnet.exe"),
        })
        {
            if (DotnetVersion(c) != null) return c;
        }
        return null;
    }

    public static string? DotnetVersion(string? dotnetPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(dotnetPath) ? "dotnet" : dotnetPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            string outp = p.StandardOutput.ReadToEnd().Trim();
            if (!p.WaitForExit(4000)) { try { p.Kill(); } catch { } return null; }
            return p.ExitCode == 0 && outp.Length > 0 ? outp : null;
        }
        catch { return null; }
    }

    public static async Task<bool> BuildAsync(string? dotnetPath, string projectOrDir, Action<string> onLine)
    {
        var psi = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(dotnetPath) ? "dotnet" : dotnetPath,
            Arguments = $"build \"{projectOrDir}\" -c Release -v minimal",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) onLine(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch (Exception e)
        {
            onLine("Failed to start dotnet: " + e.Message);
            onLine("Set the dotnet path in Settings if it isn't on your PATH.");
            return false;
        }
    }
}
