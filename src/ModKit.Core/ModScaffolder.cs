namespace ModKit.Core;

public static class ModScaffolder
{
    public static (string modDir, string csproj) Create(string workspaceDir, string modName, string description, ModTemplate template = ModTemplate.Panel)
    {
        string name = SanitizeName(modName);
        if (string.IsNullOrWhiteSpace(description)) description = name + " mod.";

        string modDir = Path.Combine(workspaceDir, "mods", name);
        if (Directory.Exists(modDir))
            throw new IOException($"A mod folder already exists: {modDir}");

        Directory.CreateDirectory(modDir);
        string csproj = Path.Combine(modDir, name + ".csproj");
        File.WriteAllText(csproj, Templates.Csproj(name));
        File.WriteAllText(Path.Combine(modDir, name + "Plugin.cs"), Templates.Plugin(name, description, template));
        return (modDir, csproj);
    }

    public static string BuiltDll(string modDir, string name)
        => Path.Combine(modDir, "bin", "Release", name + ".dll");

    public static string SanitizeName(string n)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in n)
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.Length == 0 ? "MyMod" : sb.ToString();
    }
}
