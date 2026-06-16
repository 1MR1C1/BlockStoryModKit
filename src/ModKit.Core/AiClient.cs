using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ModKit.Core;

public enum AiProvider { Anthropic, OpenAI, Ollama, ClaudeCode }

public sealed class AiMessage
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = "";
    public AiMessage() { }
    public AiMessage(string role, string text) { Role = role; Text = text; }
}

public static class AiClient
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(180) };

    public const string DefaultAnthropicModel = "claude-sonnet-4-5";
    public const string DefaultOpenAiModel = "gpt-4o";
    public const string DefaultOllamaEndpoint = "http://localhost:11434";
    public const string DefaultOllamaModel = "qwen2.5-coder";
    public const string DefaultClaudeCodeModel = "sonnet";

    public static async Task<string> ChatAsync(AiProvider provider, string? apiKey, string model, string? endpoint,
        string system, IReadOnlyList<AiMessage> messages, CancellationToken ct = default)
    {
        return provider switch
        {
            AiProvider.Anthropic => await Anthropic(apiKey, model, system, messages, ct),
            AiProvider.OpenAI => await OpenAi(apiKey, model, system, messages, ct),
            AiProvider.ClaudeCode => await ClaudeCodeCli(model, system, messages, ct),
            _ => await Ollama(endpoint, model, system, messages, ct),
        };
    }

    static async Task<string> ClaudeCodeCli(string model, string system, IReadOnlyList<AiMessage> msgs, CancellationToken ct)
    {
        string? exe = FindClaude();
        if (exe == null) throw new InvalidOperationException(
            "Claude Code CLI not found. Install it (npm i -g @anthropic-ai/claude-code) and run `claude` once to sign in with your subscription.");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-p");
        psi.ArgumentList.Add("--output-format"); psi.ArgumentList.Add("text");
        psi.ArgumentList.Add("--system-prompt"); psi.ArgumentList.Add(system);
        if (!string.IsNullOrWhiteSpace(model)) { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(model); }

        using var p = System.Diagnostics.Process.Start(psi) ?? throw new Exception("Couldn't start the claude CLI.");
        await p.StandardInput.WriteAsync(FlattenConversation(msgs).AsMemory(), ct);
        p.StandardInput.Close();
        string outp = await p.StandardOutput.ReadToEndAsync(ct);
        string err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new Exception("Claude Code error: " + (err.Length > 0 ? Trim(err) : Trim(outp)));
        return outp;
    }

    static string FlattenConversation(IReadOnlyList<AiMessage> msgs)
    {
        if (msgs.Count == 1) return msgs[0].Text;
        var sb = new StringBuilder();
        foreach (var m in msgs)
        {
            sb.AppendLine(m.Role == "assistant" ? "=== Your previous answer ===" : "=== Request ===");
            sb.AppendLine(m.Text);
            sb.AppendLine();
        }
        sb.AppendLine("=== Now respond to the latest request above ===");
        return sb.ToString();
    }

    public static string? FindClaude()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (string c in new[]
        {
            "claude", "/usr/bin/claude", "/usr/local/bin/claude",
            Path.Combine(home, ".claude", "local", "claude"),
            Path.Combine(home, ".npm-global", "bin", "claude"),
            Path.Combine(appData, "npm", "claude.cmd"),
        })
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo { FileName = c, Arguments = "--version", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) continue;
                p.WaitForExit(4000);
                if (p.HasExited && p.ExitCode == 0) return c;
            }
            catch { }
        }
        return null;
    }

    static async Task<string> Anthropic(string? key, string model, string system, IReadOnlyList<AiMessage> msgs, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Set your Anthropic API key in Settings.");
        var body = new JsonObject
        {
            ["model"] = string.IsNullOrWhiteSpace(model) ? DefaultAnthropicModel : model,
            ["max_tokens"] = 8000,
            ["system"] = system,
            ["messages"] = ToJsonMessages(msgs),
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", key);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = Json(body);
        using var resp = await Http.SendAsync(req, ct);
        string s = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new Exception($"Anthropic {(int)resp.StatusCode}: {Trim(s)}");
        var node = JsonNode.Parse(s);
        return (node?["content"]?.AsArray() ?? new JsonArray())
            .Where(c => (string?)c?["type"] == "text")
            .Select(c => (string?)c?["text"] ?? "")
            .Aggregate("", (a, b) => a + b);
    }

    static async Task<string> OpenAi(string? key, string model, string system, IReadOnlyList<AiMessage> msgs, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Set your OpenAI API key in Settings.");
        var arr = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = system } };
        foreach (var m in msgs) arr.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Text });
        var body = new JsonObject
        {
            ["model"] = string.IsNullOrWhiteSpace(model) ? DefaultOpenAiModel : model,
            ["messages"] = arr,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        req.Content = Json(body);
        using var resp = await Http.SendAsync(req, ct);
        string s = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new Exception($"OpenAI {(int)resp.StatusCode}: {Trim(s)}");
        return (string?)JsonNode.Parse(s)?["choices"]?[0]?["message"]?["content"] ?? "";
    }

    static async Task<string> Ollama(string? endpoint, string model, string system, IReadOnlyList<AiMessage> msgs, CancellationToken ct)
    {
        string baseUrl = string.IsNullOrWhiteSpace(endpoint) ? DefaultOllamaEndpoint : endpoint.TrimEnd('/');
        var arr = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = system } };
        foreach (var m in msgs) arr.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Text });
        var body = new JsonObject
        {
            ["model"] = string.IsNullOrWhiteSpace(model) ? DefaultOllamaModel : model,
            ["messages"] = arr,
            ["stream"] = false,
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/api/chat") { Content = Json(body) };
        HttpResponseMessage resp;
        try { resp = await Http.SendAsync(req, ct); }
        catch (Exception e) { throw new Exception($"Can't reach Ollama at {baseUrl} — is it running? ({e.Message})"); }
        using (resp)
        {
            string s = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new Exception($"Ollama {(int)resp.StatusCode}: {Trim(s)}");
            return (string?)JsonNode.Parse(s)?["message"]?["content"] ?? "";
        }
    }

    static JsonArray ToJsonMessages(IReadOnlyList<AiMessage> msgs)
    {
        var arr = new JsonArray();
        foreach (var m in msgs) arr.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Text });
        return arr;
    }

    static StringContent Json(JsonObject body) =>
        new(body.ToJsonString(new JsonSerializerOptions()), Encoding.UTF8, "application/json");

    static string Trim(string s) => s.Length > 400 ? s.Substring(0, 400) + "…" : s;

    public static string ExtractCode(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return "";
        int i = FindFence(reply, "```csharp");
        if (i < 0) i = FindFence(reply, "```cs");
        if (i < 0) i = FindFence(reply, "```c#");
        int contentStart;
        if (i >= 0) contentStart = reply.IndexOf('\n', i) + 1;
        else
        {
            int g = reply.IndexOf("```", StringComparison.Ordinal);
            if (g < 0) return reply.Trim();
            contentStart = reply.IndexOf('\n', g) + 1;
        }
        if (contentStart <= 0) return reply.Trim();
        int end = reply.IndexOf("```", contentStart, StringComparison.Ordinal);
        return (end < 0 ? reply.Substring(contentStart) : reply.Substring(contentStart, end - contentStart)).Trim();
    }

    static int FindFence(string s, string fence) => s.IndexOf(fence, StringComparison.OrdinalIgnoreCase);
}
