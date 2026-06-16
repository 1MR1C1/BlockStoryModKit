namespace ModKit.Core;

public sealed class WorkspaceMod
{
    public string Name { get; init; } = "";
    public string Dir { get; init; } = "";
    public string Csproj { get; init; } = "";
    public bool Built { get; init; }
    public bool Installed { get; init; }

    public string Display
    {
        get
        {
            string s = Name;
            if (Built) s += "  · built";
            if (Installed) s += " · installed";
            return s;
        }
    }

    public string MainFile
    {
        get
        {
            string plugin = Path.Combine(Dir, Name + "Plugin.cs");
            if (File.Exists(plugin)) return plugin;
            string? any = Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.cs").FirstOrDefault() : null;
            return any ?? Dir;
        }
    }
}

public static class WorkspaceMods
{
    public static List<WorkspaceMod> List(string? workspaceDir, string? pluginsDir = null)
    {
        var result = new List<WorkspaceMod>();
        if (string.IsNullOrWhiteSpace(workspaceDir)) return result;
        string mods = Path.Combine(workspaceDir, "mods");
        if (!Directory.Exists(mods)) return result;

        foreach (string dir in Directory.GetDirectories(mods))
        {

            string? csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
            if (csproj == null) continue;
            string name = Path.GetFileNameWithoutExtension(csproj);
            bool installed = pluginsDir != null &&
                (File.Exists(Path.Combine(pluginsDir, name + ".dll")) || File.Exists(Path.Combine(pluginsDir, name + ".dll.disabled")));
            result.Add(new WorkspaceMod
            {
                Name = name,
                Dir = dir,
                Csproj = csproj,
                Built = File.Exists(ModScaffolder.BuiltDll(dir, name)),
                Installed = installed,
            });
        }
        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public static void Delete(WorkspaceMod mod)
    {
        if (Directory.Exists(mod.Dir)) Directory.Delete(mod.Dir, recursive: true);
    }
}
