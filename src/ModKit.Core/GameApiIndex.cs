using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ModKit.Core;

public sealed class ApiType
{
    public string Namespace { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "class";
    public string Assembly { get; set; } = "";
    public List<string> Members { get; set; } = new();
    public List<string> EnumValues { get; set; } = new();

    public string FullName => string.IsNullOrEmpty(Namespace) ? Name : Namespace + "." + Name;
    public string Display => $"{Kind}  {FullName}   ({Members.Count + EnumValues.Count} members)";
}

public sealed class GameApiIndex
{
    public List<ApiType> Types { get; set; } = new();
    public DateTime BuiltUtc { get; set; }

    static readonly string[] TargetPrefixes = { "Assembly-CSharp", "Blocksters" };

    public static string CachePath =>
        Path.Combine(Path.GetDirectoryName(Config.ConfigPath)!, "game-api-index.json");

    public static GameApiIndex LoadOrBuild(string gameDir, Action<string>? onLog = null)
    {
        string managed = GamePaths.ManagedDir(gameDir);
        string stamp = Path.Combine(managed, "Assembly-CSharp.dll");
        try
        {
            if (File.Exists(CachePath) && File.Exists(stamp) &&
                File.GetLastWriteTimeUtc(CachePath) >= File.GetLastWriteTimeUtc(stamp))
            {
                var cached = JsonSerializer.Deserialize<GameApiIndex>(File.ReadAllText(CachePath));
                if (cached != null && cached.Types.Count > 0) { onLog?.Invoke($"Loaded cached game API ({cached.Types.Count} types)."); return cached; }
            }
        }
        catch { }

        var idx = Build(managed, onLog);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(idx));
        }
        catch { }
        return idx;
    }

    public static GameApiIndex Build(string managedDir, Action<string>? onLog = null)
    {
        var idx = new GameApiIndex { BuiltUtc = DateTime.UtcNow };
        if (!Directory.Exists(managedDir)) { onLog?.Invoke("Managed folder not found: " + managedDir); return idx; }

        string[] dlls = Directory.GetFiles(managedDir, "*.dll");
        string core = dlls.FirstOrDefault(d => Path.GetFileName(d).Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase))
                   ?? dlls.FirstOrDefault(d => Path.GetFileName(d).Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase))
                   ?? dlls[0];
        var resolver = new PathAssemblyResolver(dlls);
        using var mlc = new MetadataLoadContext(resolver, Path.GetFileNameWithoutExtension(core));

        foreach (string dll in dlls)
        {
            string fn = Path.GetFileNameWithoutExtension(dll);
            if (!TargetPrefixes.Any(p => fn.StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
            onLog?.Invoke("Indexing " + Path.GetFileName(dll) + "…");
            Type[] types;
            try { types = mlc.LoadFromAssemblyPath(dll).GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }
            catch (Exception ex) { onLog?.Invoke("  skipped (" + ex.Message + ")"); continue; }

            foreach (Type t in types)
            {
                if (t == null || !(t.IsPublic || t.IsNestedPublic) || t.IsSpecialName) continue;
                if (t.Name.StartsWith("<") || t.Name.Contains("__")) continue;
                try { idx.Types.Add(Describe(t, fn)); } catch { }
            }
        }
        idx.Types.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
        onLog?.Invoke($"Game API indexed: {idx.Types.Count} types.");
        return idx;
    }

    static ApiType Describe(Type t, string asm)
    {
        var d = new ApiType
        {
            Namespace = t.Namespace ?? "",
            Name = Pretty(t),
            Assembly = asm,
            Kind = t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : "class",
        };

        if (t.IsEnum)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                try { d.EnumValues.Add($"{f.Name} = {f.GetRawConstantValue()}"); } catch { d.EnumValues.Add(f.Name); }
            }
            return d;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var f in t.GetFields(flags))
        {
            if (f.IsSpecialName) continue;
            string konst = f.IsLiteral ? " = " + SafeConst(f) : "";
            d.Members.Add($"{Mod(f.IsStatic)}{Pretty(f.FieldType)} {f.Name}{konst}");
        }
        foreach (var p in t.GetProperties(flags))
            d.Members.Add($"{Pretty(p.PropertyType)} {p.Name} {{ {(p.CanRead ? "get; " : "")}{(p.CanWrite ? "set; " : "")}}}");
        foreach (var m in t.GetMethods(flags))
        {
            if (m.IsSpecialName) continue;
            string ps = string.Join(", ", m.GetParameters().Select(p => $"{Pretty(p.ParameterType)} {p.Name}"));
            d.Members.Add($"{Mod(m.IsStatic)}{Pretty(m.ReturnType)} {m.Name}({ps})");
        }
        d.Members.Sort(StringComparer.Ordinal);
        return d;
    }

    static string Mod(bool isStatic) => isStatic ? "static " : "";
    static string SafeConst(FieldInfo f) { try { return f.GetRawConstantValue()?.ToString() ?? "null"; } catch { return "?"; } }

    static string Pretty(Type t)
    {
        if (t.IsGenericType)
        {
            string n = t.Name;
            int tick = n.IndexOf('`');
            if (tick > 0) n = n.Substring(0, tick);
            return n + "<" + string.Join(", ", t.GetGenericArguments().Select(Pretty)) + ">";
        }
        return t.Name;
    }

    public List<ApiType> Search(string query, int max = 200)
    {
        if (string.IsNullOrWhiteSpace(query)) return Types.Take(max).ToList();
        string[] terms = query.Split(new[] { ' ', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);
        return Types
            .Select(t => (t, score: Score(t, terms)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.t.FullName, StringComparer.Ordinal)
            .Take(max).Select(x => x.t).ToList();
    }

    static int Score(ApiType t, string[] terms)
    {
        int s = 0;
        string name = t.Name.ToLowerInvariant();
        string full = t.FullName.ToLowerInvariant();
        foreach (string raw in terms)
        {
            string q = raw.ToLowerInvariant();
            if (name == q) s += 100;
            else if (name.Contains(q)) s += 40;
            else if (full.Contains(q)) s += 20;
            if (t.Members.Any(m => m.ToLowerInvariant().Contains(q))) s += 6;
            if (t.EnumValues.Any(v => v.ToLowerInvariant().Contains(q))) s += 6;
        }
        return s;
    }

    public string Detail(ApiType t)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{t.Kind} {t.FullName}");
        sb.AppendLine($"// from {t.Assembly}.dll");
        sb.AppendLine();
        if (t.EnumValues.Count > 0) { sb.AppendLine("values:"); foreach (var v in t.EnumValues) sb.AppendLine("  " + v); }
        foreach (var m in t.Members) sb.AppendLine("  " + m);
        return sb.ToString();
    }

    public string ForAiContext(string requestText, int maxTypes = 14, int maxMembersPer = 26)
    {
        var hits = Search(requestText, maxTypes);
        if (hits.Count == 0) return "";
        var sb = new StringBuilder();
        sb.AppendLine("RELEVANT GAME API (these symbols REALLY EXIST in this game's Assembly-CSharp — prefer them, and Harmony-patch real methods here instead of inventing names):");
        foreach (var t in hits)
        {
            sb.AppendLine($"--- {t.Kind} {t.FullName} ({t.Assembly}) ---");
            if (t.EnumValues.Count > 0) sb.AppendLine("  values: " + string.Join(", ", t.EnumValues.Take(40)));
            foreach (var m in t.Members.Take(maxMembersPer)) sb.AppendLine("  " + m);
            if (t.Members.Count > maxMembersPer) sb.AppendLine($"  …(+{t.Members.Count - maxMembersPer} more)");
        }
        return sb.ToString();
    }
}
