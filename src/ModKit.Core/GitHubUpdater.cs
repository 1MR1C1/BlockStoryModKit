using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ModKit.Core;

// Checks the projects' GitHub Releases for newer versions and pulls them.
// Mods   -> 1MR1C1/BlockStoryMods    (release assets = each mod's <Name>.dll)
// Kit    -> 1MR1C1/BlockStoryModKit  (release assets = BlockStoryModKit-Linux / -Windows.exe)
// Nothing is overwritten unless the remote version is actually higher than what's installed.
public static class GitHubUpdater
{
    public const string Owner = "1MR1C1";
    public const string ModsRepo = "BlockStoryMods";
    public const string KitRepo = "BlockStoryModKit";

    private static readonly HttpClient Http = MakeClient();

    private static HttpClient MakeClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("BlockStoryModKit-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public sealed class Release
    {
        public string Tag = "";
        public Version Version = new(0, 0);
        public readonly Dictionary<string, string> Assets = new(StringComparer.OrdinalIgnoreCase); // name -> download url
    }

    public static async Task<Release?> LatestRelease(string repo)
    {
        string url = $"https://api.github.com/repos/{Owner}/{repo}/releases/latest";
        using var resp = await Http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var rel = new Release { Tag = root.GetProperty("tag_name").GetString() ?? "" };
        rel.Version = ParseVer(rel.Tag);
        if (root.TryGetProperty("assets", out var assets))
            foreach (var a in assets.EnumerateArray())
            {
                string? name = a.GetProperty("name").GetString();
                string? dl = a.GetProperty("browser_download_url").GetString();
                if (name != null && dl != null) rel.Assets[name] = dl;
            }
        return rel;
    }

    // Downloads each release DLL that matches an installed mod, reads its real assembly version,
    // and returns only the ones that are genuinely newer (the temp file becomes the SourcePath).
    public static async Task<List<ModUpdate>> CheckMods(string gameDir)
    {
        var outList = new List<ModUpdate>();
        Release? rel = await LatestRelease(ModsRepo);
        if (rel == null) return outList;

        string tmp = Path.Combine(Path.GetTempPath(), "bsmk-update");
        Directory.CreateDirectory(tmp);

        foreach (InstalledMod m in ModManager.List(gameDir))
        {
            if (m.IsCore) continue;
            if (!rel.Assets.TryGetValue(m.Name + ".dll", out string? dl)) continue;

            string dest = Path.Combine(tmp, m.Name + ".dll");
            if (!await Download(dl, dest)) continue;

            Version remote = ReadVersion(dest) ?? new Version(0, 0);
            Version installed = ParseVer(m.Version);
            if (remote > installed)
                outList.Add(new ModUpdate
                {
                    Name = m.Name,
                    InstalledVersion = m.Version ?? "0.0",
                    AvailableVersion = remote.ToString(),
                    SourcePath = dest,
                });
        }
        return outList.OrderBy(u => u.Name).ToList();
    }

    public static void InstallMod(ModUpdate u, string gameDir) => ModManager.Install(gameDir, u.SourcePath);

    public sealed class LauncherCheck
    {
        public bool HasUpdate;
        public string Current = "";
        public string Latest = "";
        public string? AssetUrl;
        public string AssetName = "";
        public string? Message;
    }

    public static async Task<LauncherCheck> CheckLauncher()
    {
        var r = new LauncherCheck { Current = CurrentLauncherVersion().ToString() };
        Release? rel = await LatestRelease(KitRepo);
        if (rel == null) { r.Message = "Couldn't reach GitHub."; return r; }
        r.Latest = rel.Version.ToString();

        string wanted = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "BlockStoryModKit-Windows.exe" : "BlockStoryModKit-Linux";
        rel.Assets.TryGetValue(wanted, out r.AssetUrl);
        r.AssetName = wanted;

        r.HasUpdate = rel.Version > CurrentLauncherVersion() && r.AssetUrl != null;
        if (r.AssetUrl == null) r.Message = $"Release {rel.Tag} has no {wanted} asset.";
        return r;
    }

    // Self-update: download the new binary, then swap it in. Renaming the running executable is
    // allowed on both Linux and Windows, so we rename the live file aside and drop the new one in.
    // Caller should prompt a restart afterwards. Returns the message to show.
    public static async Task<string> SelfUpdateLauncher(LauncherCheck c)
    {
        if (!c.HasUpdate || c.AssetUrl == null) return "Launcher is already up to date.";
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return "Couldn't locate the running launcher to update it.";

        string neu = exe + ".new";
        if (!await Download(c.AssetUrl, neu)) return "Download failed.";
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            File.SetUnixFileMode(neu, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);

        string old = exe + ".old";
        try { if (File.Exists(old)) File.Delete(old); } catch { }
        File.Move(exe, old);          // rename the live binary aside
        File.Move(neu, exe);          // put the new one in its place
        return $"Launcher updated {c.Current} → {c.Latest}. Restart the Mod Kit to use it.";
    }

    // Call at startup to clear the leftover from a previous self-update.
    public static void CleanupOldBinary()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe + ".old")) File.Delete(exe + ".old");
        }
        catch { }
    }

    private static async Task<bool> Download(string url, string dest)
    {
        try
        {
            byte[] data = await Http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(dest, data);
            return true;
        }
        catch { return false; }
    }

    private static Version CurrentLauncherVersion()
        => Assembly.GetEntryAssembly()?.GetName().Version
           ?? Assembly.GetExecutingAssembly().GetName().Version
           ?? new Version(0, 0);

    private static Version? ReadVersion(string path)
    {
        try { return AssemblyName.GetAssemblyName(path).Version; }
        catch { return null; }
    }

    private static Version ParseVer(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new Version(0, 0);
        s = s.Trim().TrimStart('v', 'V');
        return Version.TryParse(s, out var v) ? v : new Version(0, 0);
    }
}
