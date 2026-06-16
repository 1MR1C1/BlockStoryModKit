using System.Text;
using System.Text.RegularExpressions;

namespace ModKit.Core;

public sealed class KeyConflict
{
    public string Binding = "";
    public List<string> Mods = new();
}

public static class ModConflicts
{
    static readonly Regex Bind = new(@"<(Keyboard|Mouse|Gamepad)>/[A-Za-z0-9]+", RegexOptions.Compiled);

    public static List<KeyConflict> Find(string pluginsDir)
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(pluginsDir)) return new();

        foreach (string dll in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            string name = Path.GetFileNameWithoutExtension(dll);
            if (name.Equals("BlockStoryCore", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (string b in BindingsIn(dll))
            {
                if (!map.TryGetValue(b, out var set)) map[b] = set = new(StringComparer.OrdinalIgnoreCase);
                set.Add(name);
            }
        }

        return map.Where(kv => kv.Value.Count > 1)
                  .OrderBy(kv => kv.Key)
                  .Select(kv => new KeyConflict { Binding = kv.Key, Mods = kv.Value.ToList() })
                  .ToList();
    }

    static IEnumerable<string> BindingsIn(string dll)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            byte[] bytes = File.ReadAllBytes(dll);

            foreach (int off in new[] { 0, 1 })
            {
                if (bytes.Length - off <= 0) continue;
                string s = Encoding.Unicode.GetString(bytes, off, ((bytes.Length - off) / 2) * 2);
                foreach (Match m in Bind.Matches(s)) found.Add(m.Value);
            }
        }
        catch { }
        return found;
    }
}
