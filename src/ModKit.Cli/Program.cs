using ModKit.Core;

return await Cli.Run(args);

static class Cli
{
    static Config _cfg = Config.Load();

    public static async Task<int> Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help") { Help(); return 0; }

        var rest = new List<string>();
        string? game = _cfg.GameDir, ws = _cfg.WorkspaceDir, dotnet = _cfg.DotnetPath;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game": game = Next(args, ref i); break;
                case "--workspace": ws = Next(args, ref i); break;
                case "--dotnet": dotnet = Next(args, ref i); break;
                default: rest.Add(args[i]); break;
            }
        }
        game ??= GamePaths.AutoDetect();
        if (string.IsNullOrEmpty(dotnet)) dotnet = BuildRunner.FindDotnet();
        string cmd = rest[0].ToLowerInvariant();
        var a = rest.Skip(1).ToList();

        try
        {
            switch (cmd)
            {
                case "detect":
                    string? d = GamePaths.AutoDetect();
                    Console.WriteLine(d ?? "Block Story not found — pass --game DIR.");
                    return d == null ? 1 : 0;

                case "status":
                    Console.WriteLine($"game:      {game ?? "(not set)"}");
                    Console.WriteLine($"  valid:   {GamePaths.IsValidGameDir(game)}");
                    Console.WriteLine($"  framework: {(BaseInstaller.IsInstalled(game) ? "installed" : "NOT installed")}");
                    Console.WriteLine($"workspace: {ws ?? "(not set)"}");
                    Console.WriteLine($"  ready:   {(!string.IsNullOrWhiteSpace(ws) && WorkspaceInitializer.IsInitialized(ws!))}");
                    Console.WriteLine($"dotnet:    {BuildRunner.DotnetVersion(dotnet) ?? "NOT found"}");
                    return 0;

                case "list":
                    if (!Need(GamePaths.IsValidGameDir(game), "no valid game folder (BepInEx missing)")) return 1;
                    foreach (InstalledMod m in ModManager.List(game!))
                        Console.WriteLine($"  [{(m.Enabled ? "x" : " ")}] {m.Name}{(m.IsCore ? "  (required)" : "")}");
                    return 0;

                case "install":
                    if (!Need(a.Count > 0, "usage: modkit install <path-to-.dll/.zip/folder>")) return 1;
                    if (!Need(GamePaths.IsValidGameDir(game), "framework not installed — run: modkit setup --base <zip>")) return 1;
                    int n = Directory.Exists(a[0]) ? ModManager.InstallFromFolder(game!, a[0])
                          : a[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ModManager.InstallFromZip(game!, a[0])
                          : Install1(game!, a[0]);
                    Console.WriteLine($"installed {n} mod(s) — restart the game.");
                    return 0;

                case "enable":
                case "disable":
                    if (!Need(a.Count > 0, $"usage: modkit {cmd} <ModName>")) return 1;
                    return Toggle(game, a[0], cmd == "enable");

                case "uninstall":
                    if (!Need(a.Count > 0, "usage: modkit uninstall <ModName>")) return 1;
                    return Uninstall(game, a[0]);

                case "backup":
                    if (!Need(GamePaths.IsValidGameDir(game), "no valid game folder")) return 1;
                    Console.WriteLine("backed up -> " + ModManager.BackupPlugins(game!));
                    return 0;

                case "setup":
                    return Setup(game, a);

                case "mods":
                    foreach (WorkspaceMod m in WorkspaceMods.List(ws, GamePaths.IsValidGameDir(game) ? GamePaths.PluginsDir(game!) : null))
                        Console.WriteLine($"  {m.Display}");
                    return 0;

                case "new":
                    return New(game, ws, a);

                case "build":
                    return await Build(game, ws, dotnet, a);

                case "play":
                    GameLauncher.LaunchViaSteam(); Console.WriteLine("launching via Steam…"); return 0;

                default:
                    Console.Error.WriteLine("unknown command: " + cmd); Help(); return 1;
            }
        }
        catch (Exception e) { Console.Error.WriteLine("error: " + e.Message); return 1; }
    }

    static int Install1(string game, string dll) { ModManager.Install(game, dll); return 1; }

    static int Toggle(string? game, string name, bool on)
    {
        if (!GamePaths.IsValidGameDir(game)) { Console.Error.WriteLine("no valid game folder"); return 1; }
        InstalledMod? m = ModManager.List(game!).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (m == null) { Console.Error.WriteLine("mod not found: " + name); return 1; }
        ModManager.SetEnabled(m, on);
        Console.WriteLine($"{name}: {(on ? "enabled" : "disabled")} (restart the game)");
        return 0;
    }

    static int Uninstall(string? game, string name)
    {
        if (!GamePaths.IsValidGameDir(game)) { Console.Error.WriteLine("no valid game folder"); return 1; }
        InstalledMod? m = ModManager.List(game!).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (m == null) { Console.Error.WriteLine("mod not found: " + name); return 1; }
        if (m.IsCore) { Console.Error.WriteLine("refusing to uninstall the required Core toolbox"); return 1; }
        ModManager.Uninstall(m);
        Console.WriteLine("uninstalled " + name);
        return 0;
    }

    static int Setup(string? game, List<string> a)
    {
        if (!Need(!string.IsNullOrWhiteSpace(game) && GamePaths.LooksLikeGameDir(game), "pass --game <BlockStory folder>")) return 1;
        int bi = a.IndexOf("--base");
        string? baseZip = bi >= 0 && bi + 1 < a.Count ? a[bi + 1] : null;
        if (!Need(baseZip != null && File.Exists(baseZip), "usage: modkit setup --base <base-bundle.zip>")) return 1;
        using FileStream fs = File.OpenRead(baseZip!);
        int n = BaseInstaller.Install(game!, fs);
        Console.WriteLine($"installed framework ({n} files). On Linux set the Steam launch option (see the guide).");
        return 0;
    }

    static int New(string? game, string? ws, List<string> a)
    {
        if (!Need(a.Count > 0, "usage: modkit new <Name> [--template Panel|Minimal|KeybindAction|HarmonyPatch|BlockWatcher] [--desc \"...\"]")) return 1;
        if (!EnsureWorkspace(ref ws, game)) return 1;
        string name = a[0];
        ModTemplate t = ModTemplate.Panel;
        int ti = a.IndexOf("--template"); if (ti >= 0 && ti + 1 < a.Count) Enum.TryParse(a[ti + 1], true, out t);
        int di = a.IndexOf("--desc"); string desc = di >= 0 && di + 1 < a.Count ? a[di + 1] : name + " mod.";
        var (dir, _) = ModScaffolder.Create(ws!, name, desc, t);
        Console.WriteLine($"created {ModScaffolder.SanitizeName(name)} ({t}) at {dir}");
        return 0;
    }

    static bool EnsureWorkspace(ref string? ws, string? game)
    {
        if (string.IsNullOrWhiteSpace(ws)) { Console.Error.WriteLine("set a workspace: --workspace DIR"); return false; }
        if (!WorkspaceInitializer.IsInitialized(ws!))
        {
            if (!GamePaths.IsValidGameDir(game)) { Console.Error.WriteLine("workspace not set up and no valid game to copy refs from"); return false; }
            Console.WriteLine("setting up workspace…");
            WorkspaceInitializer.Init(ws!, game!, Console.WriteLine);
        }
        return true;
    }

    static async Task<int> Build(string? game, string? ws, string? dotnet, List<string> a)
    {
        if (!Need(a.Count > 0, "usage: modkit build <ModName>")) return 1;
        WorkspaceMod? m = WorkspaceMods.List(ws).FirstOrDefault(x => x.Name.Equals(a[0], StringComparison.OrdinalIgnoreCase));
        if (m == null) { Console.Error.WriteLine("mod not found in workspace: " + a[0]); return 1; }
        bool ok = await BuildRunner.BuildAsync(dotnet, m.Csproj, Console.WriteLine);
        if (!ok) return 1;
        string dll = ModScaffolder.BuiltDll(m.Dir, m.Name);
        if (GamePaths.IsValidGameDir(game) && File.Exists(dll)) { ModManager.Install(game!, dll); Console.WriteLine($"installed {m.Name}.dll (restart the game)"); }
        return 0;
    }

    static bool Need(bool ok, string msg) { if (!ok) Console.Error.WriteLine(msg); return ok; }
    static string? Next(string[] args, ref int i) => ++i < args.Length ? args[i] : null;

    static void Help() => Console.WriteLine(@"Block Story Mod Kit — CLI
Uses the same saved settings as the GUI; override with --game DIR / --workspace DIR / --dotnet PATH.

  modkit detect                 find the Block Story install
  modkit status                 show game / framework / workspace / .NET status
  modkit list                   list installed mods ([x]=enabled)
  modkit install <path>         install a .dll, .zip pack, or folder of dlls
  modkit enable <Name>          enable a mod
  modkit disable <Name>         disable a mod
  modkit uninstall <Name>       remove a mod (not Core)
  modkit backup                 zip the plugins folder
  modkit setup --base <zip>     install the framework (BepInEx + Core) into the game
  modkit mods                   list mods in your workspace
  modkit new <Name> [--template Panel|Minimal|KeybindAction|HarmonyPatch|BlockWatcher] [--desc ""...""]
  modkit build <Name>           rebuild a workspace mod and install it
  modkit play                   launch the game via Steam");
}
