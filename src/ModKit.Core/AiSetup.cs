using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace ModKit.Core;

public static class AiSetup
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static string? OllamaExe()
    {
        foreach (string c in new[] { "ollama", "/usr/local/bin/ollama", "/usr/bin/ollama",
                                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe") })
        {
            try
            {
                var psi = new ProcessStartInfo { FileName = c, Arguments = "--version", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p == null) continue;
                p.WaitForExit(3000);
                if (p.HasExited && p.ExitCode == 0) return c;
            }
            catch { }
        }
        return null;
    }

    public static async Task<bool> IsServingAsync(string endpoint)
    {
        try { using var r = await Http.GetAsync(endpoint.TrimEnd('/') + "/api/tags"); return r.IsSuccessStatusCode; }
        catch { return false; }
    }

    public static async Task<bool> HasModelAsync(string endpoint, string model)
    {
        try
        {
            using var r = await Http.GetAsync(endpoint.TrimEnd('/') + "/api/tags");
            if (!r.IsSuccessStatusCode) return false;
            var node = JsonNode.Parse(await r.Content.ReadAsStringAsync());
            foreach (var m in node?["models"]?.AsArray() ?? new JsonArray())
            {
                string name = (string?)m?["name"] ?? "";
                if (name == model || name.StartsWith(model + ":", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch { }
        return false;
    }

    public static async Task<bool> EnsureLocalAiAsync(string endpoint, string model, Action<string> onLog, CancellationToken ct = default)
    {
        onLog($"Setting up free local AI (Ollama, model: {model})…");
        string? exe = OllamaExe();
        if (exe == null)
        {
            onLog("Ollama isn't installed — attempting to install it…");
            exe = await InstallOllamaAsync(onLog, ct);
            if (exe == null)
            {
                onLog("Couldn't auto-install Ollama. Install it manually from https://ollama.com/download,");
                onLog("then click this button again. (Linux one-liner: curl -fsSL https://ollama.com/install.sh | sh)");
                return false;
            }
        }
        onLog("Ollama found: " + exe);

        if (!await IsServingAsync(endpoint))
        {
            onLog("Starting the Ollama server…");
            try { Process.Start(new ProcessStartInfo { FileName = exe, Arguments = "serve", UseShellExecute = false, CreateNoWindow = true }); }
            catch (Exception e) { onLog("Couldn't start it: " + e.Message); }
            for (int i = 0; i < 20 && !await IsServingAsync(endpoint); i++) await Task.Delay(1000, ct);
        }
        if (!await IsServingAsync(endpoint)) { onLog($"Ollama isn't responding at {endpoint}. Start it with: ollama serve"); return false; }
        onLog("Ollama is running. ✓");

        if (await HasModelAsync(endpoint, model)) { onLog($"Model “{model}” already present. ✓ Local AI is ready."); return true; }

        onLog($"Downloading model “{model}” — this is a few GB and only happens once…");
        bool pulled = await RunStreaming(exe, "pull " + model, onLog, ct);
        if (!pulled || !await HasModelAsync(endpoint, model)) { onLog("Model download didn't complete. You can retry, or run: ollama pull " + model); return false; }
        onLog($"✓ Local AI is ready (Ollama + {model}). No API key needed.");
        return true;
    }

    static async Task<string?> InstallOllamaAsync(Action<string> onLog, CancellationToken ct)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                await RunStreaming("/bin/sh", "-c \"curl -fsSL https://ollama.com/install.sh | sh\"", onLog, ct);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                await RunStreaming("winget", "install --id Ollama.Ollama -e --accept-source-agreements --accept-package-agreements", onLog, ct);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                await RunStreaming("/bin/sh", "-c \"brew install ollama\"", onLog, ct);
        }
        catch (Exception e) { onLog("Install attempt failed: " + e.Message); }
        return OllamaExe();
    }

    public static string RecommendModel(Action<string>? onLog = null)
    {
        const string fam = "qwen2.5-coder:";
        int vram = DetectVramMB();
        if (vram > 0)
        {
            string m = vram >= 22000 ? fam + "32b"
                     : vram >= 11000 ? fam + "14b"
                     : vram >= 5500 ? fam + "7b"
                     : vram >= 3000 ? fam + "3b"
                     : fam + "1.5b";
            onLog?.Invoke($"Detected ~{vram} MB GPU VRAM → recommending {m}.");
            return m;
        }
        int ram = DetectRamMB();

        string r = ram >= 48000 ? fam + "14b"
                 : ram >= 24000 ? fam + "7b"
                 : ram >= 12000 ? fam + "3b"
                 : fam + "1.5b";
        onLog?.Invoke(vram == 0
            ? $"No NVIDIA GPU detected; using system RAM (~{ram} MB, CPU inference) → recommending {r} (smaller = faster on CPU)."
            : $"Recommending {r}.");
        return r;
    }

    public static int DetectVramMB()
    {
        int best = 0;
        best = Math.Max(best, NvidiaVramMB());
        best = Math.Max(best, RocmVramMB());
        best = Math.Max(best, SysfsVramMB());
        return best;
    }

    static int NvidiaVramMB()
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = "nvidia-smi", Arguments = "--query-gpu=memory.total --format=csv,noheader,nounits", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p == null) return 0;
            string outp = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(3000)) { try { p.Kill(); } catch { } return 0; }
            int best = 0;
            foreach (string line in outp.Split('\n'))
                if (int.TryParse(line.Trim(), out int mb) && mb > best) best = mb;
            return best;
        }
        catch { return 0; }
    }

    static int RocmVramMB()
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = "rocm-smi", Arguments = "--showmeminfo vram", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p == null) return 0;
            string outp = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(3000)) { try { p.Kill(); } catch { } return 0; }
            int best = 0;
            foreach (string line in outp.Split('\n'))
            {

                if (line.IndexOf("VRAM Total Memory", StringComparison.OrdinalIgnoreCase) < 0) continue;
                int colon = line.LastIndexOf(':');
                if (colon >= 0 && long.TryParse(line.Substring(colon + 1).Trim(), out long bytes))
                    best = Math.Max(best, (int)(bytes / 1024 / 1024));
            }
            return best;
        }
        catch { return 0; }
    }

    static int SysfsVramMB()
    {
        try
        {
            if (!Directory.Exists("/sys/class/drm")) return 0;
            int best = 0;
            foreach (string card in Directory.GetDirectories("/sys/class/drm", "card*"))
            {
                string f = Path.Combine(card, "device", "mem_info_vram_total");
                if (File.Exists(f) && long.TryParse(File.ReadAllText(f).Trim(), out long bytes))
                    best = Math.Max(best, (int)(bytes / 1024 / 1024));
            }
            return best;
        }
        catch { return 0; }
    }

    public static int DetectRamMB()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (string line in File.ReadLines("/proc/meminfo"))
                    if (line.StartsWith("MemTotal:"))
                    {
                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out long kb)) return (int)(kb / 1024);
                    }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var psi = new ProcessStartInfo { FileName = "sysctl", Arguments = "-n hw.memsize", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = Process.Start(psi);
                if (p != null) { string o = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(3000); if (long.TryParse(o, out long b)) return (int)(b / 1024 / 1024); }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var s = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(s)) return (int)(s.ullTotalPhys / 1024 / 1024);
            }
        }
        catch { }

        try { long b = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; if (b > 0) return (int)(b / 1024 / 1024); } catch { }
        return 8000;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool GlobalMemoryStatusEx([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] MEMORYSTATUSEX lpBuffer);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    sealed class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    static async Task<bool> RunStreaming(string file, string args, Action<string> onLog, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = file, Arguments = args, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onLog(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onLog(e.Data); };
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0;
        }
        catch (Exception e) { onLog("Couldn't run " + file + ": " + e.Message); return false; }
    }
}
