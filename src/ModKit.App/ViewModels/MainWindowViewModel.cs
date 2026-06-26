using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModKit.Core;

namespace ModKit.App.ViewModels;

public partial class ModRow : ObservableObject
{
    private readonly MainWindowViewModel _owner;
    public InstalledMod Mod { get; }
    public string Display => Mod.Name
        + (Mod.Version != null ? "  v" + Mod.Version : "")
        + (Mod.IsCore ? "   (required)" : "")
        + (HasUpdate ? $"   ⬆ v{AvailableVersion} available" : "");
    public bool CanToggle => !Mod.IsCore;

    [ObservableProperty] private bool _enabled;

    // Set by a GitHub update check; shows the per-mod "Update" button when a newer release exists.
    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private string? _availableVersion;

    partial void OnHasUpdateChanged(bool value) => OnPropertyChanged(nameof(Display));

    public ModRow(InstalledMod m, MainWindowViewModel owner) { Mod = m; _owner = owner; _enabled = m.Enabled; }
    partial void OnEnabledChanged(bool value) => _owner.ToggleMod(this, value);

    [RelayCommand]
    private Task UpdateThisMod() => _owner.UpdateOneMod(this);
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Config _cfg;

    // window title tracks the real build version, so it never goes stale on a release bump
    public string WindowTitle { get; } = "Block Story Mod Kit  —  v" +
        (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.2.0");

    [ObservableProperty] private string? _gameDir;
    [ObservableProperty] private string? _workspaceDir;
    [ObservableProperty] private string? _dotnetPath;
    [ObservableProperty] private string _status = "Ready.";
    [ObservableProperty] private string _newModName = "MyMod";
    [ObservableProperty] private string _newModDescription = "Does something cool.";
    [ObservableProperty] private ModTemplate _newModTemplate = ModTemplate.Panel;
    [ObservableProperty] private string _buildLog = "";
    [ObservableProperty] private string _setupInfo = "";
    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private string _baseStatus = "";
    [ObservableProperty] private string _dotnetInfo = "checking .NET SDK…";
    [ObservableProperty] private string _guideText = "";
    [ObservableProperty] private string _selectedGuide = "README.txt";

    public string[] GuideNames { get; } = { "README.txt", "INSTALL-Windows.txt", "INSTALL-Linux.txt", "HOW-TO-ADD-MODS.txt" };

    public ModTemplate[] TemplateChoices { get; } = (ModTemplate[])Enum.GetValues(typeof(ModTemplate));

    public string TemplateBlurb => NewModTemplate switch
    {
        ModTemplate.Minimal       => "Bare minimum: just shows up on the in-game Mods page and logs. Empty canvas — add your own logic.",
        ModTemplate.KeybindAction => "A rebindable hotkey runs a block of code (with an ON/OFF toggle). Fill in the action body. No UI.",
        ModTemplate.HarmonyPatch  => "Hooks a game method so your code runs before/after it. Includes a commented example patch to point at a real method.",
        ModTemplate.BlockWatcher  => "Fires a method every time any block is placed or broken. Fill in the OnBlock body to react to the world.",
        ModTemplate.WildCreature  =>"A WILD creature — a real game enemy reskinned, so the game's own NPCs fight it and it gets the real in-game health/level bar. Includes a placeable spawner + soul-catch. Resistances, oxygen, daylight-burn etc. are all deletable options.",
        ModTemplate.PetMount      => "A tameable, RIDEABLE pet summoned from a craftable Soul item (with recipe + shop price). Click to ride or open its inventory; it has its own name + inventory, climbs steps and auto-attacks. The full Xenor-style pet.",
        _                         => "A hotkey opens a themed pop-up window. Ready-made UI with a button + live player position — extend the panel.",
    };

    [ObservableProperty] private string? _editorCmd;
    [ObservableProperty] private string? _shareDir;

    private GameApiIndex? _api;
    [ObservableProperty] private string _apiSearch = "";
    [ObservableProperty] private string _apiDetail = "";
    [ObservableProperty] private string _apiStatus = "Open this tab to index the game's API.";
    [ObservableProperty] private ApiType? _selectedApiType;
    public ObservableCollection<ApiType> ApiResults { get; } = new();
    partial void OnApiSearchChanged(string value) => ApplyApiFilter();
    partial void OnSelectedApiTypeChanged(ApiType? value) => ApiDetail = value != null && _api != null ? _api.Detail(value) : "";

    public ObservableCollection<ModRow> Mods { get; } = new();
    public ObservableCollection<WorkspaceMod> WorkspaceModItems { get; } = new();
    [ObservableProperty] private WorkspaceMod? _selectedWorkspaceMod;

    [ObservableProperty] private string _modSearch = "";
    [ObservableProperty] private string? _selectedProfile;
    [ObservableProperty] private string _newProfileName = "";
    public ObservableCollection<string> ProfileNames { get; } = new();
    private List<InstalledMod> _allInstalled = new();
    partial void OnModSearchChanged(string value) => ApplyModFilter();

    public MainWindowViewModel()
    {
        GitHubUpdater.CleanupOldBinary();   // clear a leftover .old from a previous self-update
        _cfg = Config.Load();
        _gameDir = _cfg.GameDir ?? GamePaths.AutoDetect();
        _workspaceDir = _cfg.WorkspaceDir;
        _dotnetPath = _cfg.DotnetPath;
        if (Enum.TryParse(_cfg.LastTemplate, out ModTemplate t)) _newModTemplate = t;
        _editorCmd = _cfg.EditorCmd ?? GameLauncher.FindEditor();
        _shareDir = _cfg.ShareDir ?? (Directory.Exists("/home/mrc/BlockStoryMods_Share") ? "/home/mrc/BlockStoryMods_Share" : null);
        RefreshMods();
        RefreshWorkspaceMods();
        LoadGuide();
        CheckDotnet();
        if (GameDir == null) Status = "Game not found automatically — set the folder in Settings.";

        if (!BaseInstaller.IsInstalled(GameDir))
        {
            SelectedTab = 3;
            Status = "Welcome! Set your Block Story folder in Settings, then click \"Install / update framework\" here.";
        }
    }

    private async void CheckDotnet()
    {
        string? path = DotnetPath;
        string? v = await Task.Run(() => BuildRunner.DotnetVersion(path));
        if (v == null)
        {

            string? found = await Task.Run(BuildRunner.FindDotnet);
            if (found != null) { DotnetPath = found == "dotnet" ? "" : found; Persist(); v = await Task.Run(() => BuildRunner.DotnetVersion(DotnetPath)); }
        }
        DotnetInfo = v != null
            ? $".NET SDK ✓ ({v}) — needed only to build mods"
            : ".NET SDK ✗ — install it (or set the path in Settings) to BUILD mods. Not needed just to play.";
    }

    [RelayCommand]
    private void BackupMods()
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        try { string zip = ModManager.BackupPlugins(GameDir!); Status = "Backed up mods → " + zip; }
        catch (Exception e) { Status = "Backup failed: " + e.Message; }
    }

    [RelayCommand]
    private void CheckConflicts()
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        try
        {
            var conflicts = ModConflicts.Find(GamePaths.PluginsDir(GameDir!));
            if (conflicts.Count == 0) { Status = "No hotkey conflicts found between your active mods. ✓"; return; }
            string msg = string.Join("   ", conflicts.Select(c => $"{c.Binding} → {string.Join(" + ", c.Mods)}"));
            Status = $"⚠ {conflicts.Count} hotkey clash(es): {msg}  (rebind one in-game: Settings → Controls)";
        }
        catch (Exception e) { Status = "Conflict check failed: " + e.Message; }
    }

    [RelayCommand]
    private void MakeBugReport()
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        try { string zip = BugReport.Create(GameDir!); Status = "Bug report → " + zip; GameLauncher.OpenFolder(GameDir!); }
        catch (Exception e) { Status = "Bug report failed: " + e.Message; }
    }

    [ObservableProperty] private string _updateInfo = "";
    [ObservableProperty] private bool _launcherHasUpdate;
    private List<ModUpdate> _modUpdates = new();

    // Check GitHub Releases for newer mod + launcher versions (downloads nothing but the tiny DLLs
    // it needs to read their real version; installs nothing — just reports + flags the rows).
    [RelayCommand]
    private async Task CheckUpdates()
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        UpdateInfo = "Checking GitHub for updates…";
        try
        {
            _modUpdates = await GitHubUpdater.CheckMods(GameDir!);
            var lc = await GitHubUpdater.CheckLauncher();
            LauncherHasUpdate = lc.HasUpdate;
            FlagModRows();
            string mods = _modUpdates.Count == 0
                ? "All mods up to date."
                : $"{_modUpdates.Count} mod update(s): " + string.Join(", ", _modUpdates.Select(u => $"{u.Name} {u.InstalledVersion}→{u.AvailableVersion}"));
            string kit = lc.HasUpdate ? $"Launcher v{lc.Current}→v{lc.Latest} available."
                                      : (lc.Message ?? $"Launcher up to date (v{lc.Current}).");
            UpdateInfo = mods + "   " + kit;
        }
        catch (Exception e) { UpdateInfo = "Update check failed: " + e.Message; }
    }

    private void FlagModRows()
    {
        var byName = _modUpdates.ToDictionary(u => u.Name, u => u, StringComparer.OrdinalIgnoreCase);
        foreach (var row in Mods)
        {
            bool has = byName.TryGetValue(row.Mod.Name, out var u);
            row.AvailableVersion = has ? u!.AvailableVersion : null;
            row.HasUpdate = has;
        }
    }

    // Per-mod "Update" button (called from ModRow). Checks first if we haven't already.
    public async Task UpdateOneMod(ModRow row)
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        var u = _modUpdates.FirstOrDefault(x => x.Name.Equals(row.Mod.Name, StringComparison.OrdinalIgnoreCase));
        if (u == null)
        {
            Status = $"Checking {row.Mod.Name}…";
            _modUpdates = await GitHubUpdater.CheckMods(GameDir!);
            u = _modUpdates.FirstOrDefault(x => x.Name.Equals(row.Mod.Name, StringComparison.OrdinalIgnoreCase));
        }
        if (u == null) { Status = $"{row.Mod.Name} is already up to date. ✓"; FlagModRows(); return; }
        GitHubUpdater.InstallMod(u, GameDir!);
        string done = $"Updated {u.Name} {u.InstalledVersion}→{u.AvailableVersion} (restart the game).";
        _modUpdates.Remove(u);
        RefreshMods();
        FlagModRows();
        Status = done;
    }

    [RelayCommand]
    private async Task UpdateAllMods()
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        UpdateInfo = "Checking GitHub for mod updates…";
        try
        {
            _modUpdates = await GitHubUpdater.CheckMods(GameDir!);
            if (_modUpdates.Count == 0) { UpdateInfo = "All mods are already up to date. ✓"; FlagModRows(); return; }
            var done = _modUpdates.ToList();
            foreach (var u in done) GitHubUpdater.InstallMod(u, GameDir!);
            _modUpdates.Clear();
            RefreshMods();
            FlagModRows();
            UpdateInfo = $"Updated {done.Count} mod(s): " + string.Join(", ", done.Select(u => $"{u.Name}→{u.AvailableVersion}")) + " (restart the game).";
        }
        catch (Exception e) { UpdateInfo = "Mod update failed: " + e.Message; }
    }

    [RelayCommand]
    private async Task UpdateLauncher()
    {
        UpdateInfo = "Checking GitHub for a launcher update…";
        try
        {
            var c = await GitHubUpdater.CheckLauncher();
            LauncherHasUpdate = c.HasUpdate;
            if (!c.HasUpdate) { UpdateInfo = c.Message ?? $"Launcher is up to date (v{c.Current}). ✓"; return; }
            UpdateInfo = await GitHubUpdater.SelfUpdateLauncher(c);
        }
        catch (Exception e) { UpdateInfo = "Launcher update failed: " + e.Message; }
    }

    private static Stream OpenBase()
    {
        using Stream s = AssetLoader.Open(new Uri("avares://ModKit.App/Assets/base.zip"));
        var ms = new MemoryStream();
        s.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    [RelayCommand]
    private void InstallBase()
    {
        if (string.IsNullOrWhiteSpace(GameDir) || !Directory.Exists(GameDir)) { Status = "Set the Block Story folder first (Settings)."; return; }
        if (!GamePaths.LooksLikeGameDir(GameDir)) { Status = "That folder doesn't look like Block Story (no 'Block Story_Data')."; return; }
        try
        {
            using Stream s = OpenBase();
            int n = BaseInstaller.Install(GameDir!, s);
            Status = $"Installed the modding framework ({n} files). On Linux, set the Steam launch option (see the Linux guide).";
            RefreshMods();
        }
        catch (Exception e) { Status = "Framework install failed: " + e.Message; }
    }

    private void LoadGuide()
    {
        try { using Stream s = OpenBase(); GuideText = BaseInstaller.ReadText(s, SelectedGuide); }
        catch (Exception e) { GuideText = "Couldn't load guide: " + e.Message; }
    }

    partial void OnSelectedGuideChanged(string value) => LoadGuide();

    private bool ValidGame => GamePaths.IsValidGameDir(GameDir);

    // Linux Steam launch option (shown on the Setup tab so users can copy the exact line).
    // Recommended = Proton + winhttp (same loader as Windows; works on every setup).
    public bool IsLinux => System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
    public string LinuxLaunchOption => "WINEDLLOVERRIDES=\"winhttp=n,b\" %command%";

    partial void OnGameDirChanged(string? value) { Persist(); RefreshMods(); }
    partial void OnWorkspaceDirChanged(string? value) { Persist(); RefreshWorkspaceMods(); }
    partial void OnNewModTemplateChanged(ModTemplate value) { Persist(); OnPropertyChanged(nameof(TemplateBlurb)); }

    private void UpdateSetupStatus()
    {
        bool ws = !string.IsNullOrWhiteSpace(WorkspaceDir) && WorkspaceInitializer.IsInitialized(WorkspaceDir!);
        bool core = ValidGame && _allInstalled.Any(m => m.IsCore);
        SetupInfo = $"Game {(ValidGame ? "✓" : "✗")}    Core installed {(core ? "✓" : "✗")}    Workspace {(ws ? "ready ✓" : "not set up ✗")}";
        BaseStatus = BaseInstaller.IsInstalled(GameDir)
            ? "Framework installed ✓  (BepInEx + Core present)"
            : "Framework not installed — point to the game folder, then click Install.";
    }

    [RelayCommand]
    private void DetectGame()
    {
        string? d = GamePaths.AutoDetect();
        if (d != null) { GameDir = d; Status = "Found game: " + d; }
        else Status = "Couldn't find Block Story — set the folder manually.";
    }

    [RelayCommand]
    private void RefreshMods()
    {
        if (!ValidGame) { _allInstalled = new(); Mods.Clear(); Status = "No valid game folder (needs a BepInEx folder)."; UpdateSetupStatus(); RefreshProfiles(); return; }
        _allInstalled = ModManager.List(GameDir!);
        ApplyModFilter();

        var core = _allInstalled.FirstOrDefault(m => m.IsCore);
        bool othersEnabled = _allInstalled.Any(m => !m.IsCore && m.Enabled);
        if (othersEnabled && (core == null || !core.Enabled))
            Status = $"⚠ Your mods need BlockStoryCore, but it's {(core == null ? "missing" : "disabled")} — they won't load until it's {(core == null ? "installed" : "enabled")}.";
        else
            Status = $"{_allInstalled.Count} mod(s) installed.";
        UpdateSetupStatus();
        RefreshProfiles();
    }

    private void ApplyModFilter()
    {
        Mods.Clear();
        string q = (ModSearch ?? "").Trim();
        foreach (InstalledMod m in _allInstalled)
            if (q.Length == 0 || m.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                Mods.Add(new ModRow(m, this));
    }

    private void RefreshProfiles()
    {
        ProfileNames.Clear();
        foreach (string k in _cfg.Profiles.Keys.OrderBy(x => x)) ProfileNames.Add(k);
    }

    [RelayCommand]
    private void SaveProfile()
    {
        if (!ValidGame) { Status = "No valid game folder."; return; }
        string name = (string.IsNullOrWhiteSpace(NewProfileName) ? SelectedProfile : NewProfileName)?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name)) { Status = "Type a profile name first."; return; }
        var enabled = _allInstalled.Where(m => m.Enabled && !m.IsCore).Select(m => m.Name).ToList();
        _cfg.Profiles[name] = enabled; _cfg.Save();
        RefreshProfiles(); SelectedProfile = name; NewProfileName = "";
        Status = $"Saved profile “{name}” ({enabled.Count} mods).";
    }

    [RelayCommand]
    private void ApplyProfile()
    {
        if (!ValidGame || string.IsNullOrWhiteSpace(SelectedProfile) || !_cfg.Profiles.TryGetValue(SelectedProfile!, out var list))
        { Status = "Pick a saved profile first."; return; }
        ModManager.ApplyProfile(GameDir!, list);
        Status = $"Applied profile “{SelectedProfile}” — restart the game.";
        RefreshMods();
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (string.IsNullOrWhiteSpace(SelectedProfile) || !_cfg.Profiles.Remove(SelectedProfile!)) return;
        _cfg.Save(); Status = $"Deleted profile “{SelectedProfile}”."; RefreshProfiles(); SelectedProfile = null;
    }

    [RelayCommand]
    private void Play()
    {
        try { GameLauncher.LaunchViaSteam(); Status = "Launching Block Story via Steam…"; }
        catch (Exception e) { Status = "Launch failed: " + e.Message; }
    }

    [RelayCommand]
    private void OpenPlugins()
    {
        if (ValidGame) GameLauncher.OpenFolder(GamePaths.PluginsDir(GameDir!));
    }

    [RelayCommand]
    private void OpenGameFolder()
    {
        if (!string.IsNullOrWhiteSpace(GameDir) && Directory.Exists(GameDir)) GameLauncher.OpenFolder(GameDir!);
        else Status = "Set the game folder first.";
    }

    [RelayCommand]
    private void OpenLog()
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        string log = GamePaths.LogPath(GameDir!);
        if (File.Exists(log)) GameLauncher.OpenPath(log);
        else Status = "No BepInEx log yet — run the game once.";
    }

    public void ToggleMod(ModRow row, bool enabled)
    {
        try { ModManager.SetEnabled(row.Mod, enabled); row.Mod.Enabled = enabled; Status = $"{row.Mod.Name}: {(enabled ? "enabled" : "disabled")} (restart game)"; }
        catch (Exception e) { Status = "Toggle failed: " + e.Message; }
    }

    public void InstallFrom(string path)
    {
        if (!ValidGame) { Status = "Set a valid game folder first (Settings)."; return; }
        try
        {
            int n;
            if (Directory.Exists(path)) n = ModManager.InstallFromFolder(GameDir!, path);
            else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) n = ModManager.InstallFromZip(GameDir!, path);
            else { ModManager.Install(GameDir!, path); n = 1; }
            Status = n > 0 ? $"Installed {n} mod(s) — restart the game." : "No .dll files found to install.";
            RefreshMods();
        }
        catch (Exception e) { Status = "Install failed: " + e.Message; }
    }

    [RelayCommand]
    private void UninstallMod(ModRow? row)
    {
        if (row == null || row.Mod.IsCore) return;
        try { ModManager.Uninstall(row.Mod); Status = $"Uninstalled {row.Mod.Name}."; RefreshMods(); }
        catch (Exception e) { Status = "Uninstall failed: " + e.Message; }
    }

    [RelayCommand]
    private async Task InitWorkspace()
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        if (string.IsNullOrWhiteSpace(WorkspaceDir)) { Status = "Pick a workspace folder first."; return; }
        Persist();
        BuildLog = "Setting up workspace…\n";
        await Task.Run(() => WorkspaceInitializer.Init(WorkspaceDir!, GameDir!, AppendBuild));
        Status = "Workspace initialized.";
    }

    [RelayCommand]
    private async Task CreateAndBuild()
    {
        if (!ValidGame) { Status = "Install the modding framework first (Setup & Help tab)."; SelectedTab = 3; return; }
        if (BuildRunner.DotnetVersion(DotnetPath) == null) { Status = "Need the .NET SDK to build mods — install it, then set its path in Settings."; return; }

        if (string.IsNullOrWhiteSpace(WorkspaceDir))
        {
            WorkspaceDir = Path.Combine(Path.GetDirectoryName(Config.ConfigPath)!, "workspace");
            Persist();
        }
        if (!WorkspaceInitializer.IsInitialized(WorkspaceDir!))
        {
            BuildLog = "Setting up your workspace…\n";
            await Task.Run(() => WorkspaceInitializer.Init(WorkspaceDir!, GameDir!, AppendBuild));
        }

        string name = ModScaffolder.SanitizeName(NewModName);
        try
        {
            var (modDir, csproj) = ModScaffolder.Create(WorkspaceDir!, NewModName, NewModDescription, NewModTemplate);
            BuildLog += $"Created mod at {modDir} ({NewModTemplate} template)\n\nBuilding…\n";
            await BuildAndInstall(name, csproj, modDir);
            RefreshWorkspaceMods();
        }
        catch (Exception e) { Status = e.Message; AppendBuild(e.ToString()); }
    }

    partial void OnSelectedTabChanged(int value) { if (value == 4) _ = EnsureApi(); }

    private async Task EnsureApi()
    {
        if (_api != null) return;
        if (!ValidGame) { ApiStatus = "Set a valid game folder first (Settings)."; return; }
        ApiStatus = "Indexing the game's API (reading its DLLs)…";
        try
        {
            string game = GameDir!;
            _api = await Task.Run(() => GameApiIndex.LoadOrBuild(game, s => Dispatcher.UIThread.Post(() => ApiStatus = s)));
            ApiStatus = $"Game API: {_api.Types.Count} types indexed. Search above to find real classes/methods/IDs.";
            ApplyApiFilter();
        }
        catch (Exception e) { ApiStatus = "Couldn't index the game API: " + e.Message; }
    }

    [RelayCommand]
    private async Task RebuildApi()
    {
        try { if (File.Exists(GameApiIndex.CachePath)) File.Delete(GameApiIndex.CachePath); } catch { }
        _api = null;
        await EnsureApi();
    }

    private void ApplyApiFilter()
    {
        ApiResults.Clear();
        if (_api == null) return;
        foreach (var t in _api.Search(ApiSearch, 400)) ApiResults.Add(t);
    }

    [RelayCommand]
    private void RefreshWorkspaceMods()
    {
        WorkspaceModItems.Clear();
        string? plugins = ValidGame ? GamePaths.PluginsDir(GameDir!) : null;
        foreach (WorkspaceMod m in WorkspaceMods.List(WorkspaceDir, plugins)) WorkspaceModItems.Add(m);
        UpdateSetupStatus();
    }

    [RelayCommand]
    private async Task RebuildMod(WorkspaceMod? mod)
    {
        if (mod == null) return;
        BuildLog = $"Rebuilding {mod.Name}…\n";
        try { await BuildAndInstall(mod.Name, mod.Csproj, mod.Dir); RefreshWorkspaceMods(); }
        catch (Exception e) { Status = e.Message; AppendBuild(e.ToString()); }
    }

    [RelayCommand]
    private void OpenModFolder(WorkspaceMod? mod)
    {
        if (mod != null && Directory.Exists(mod.Dir)) GameLauncher.OpenFolder(mod.Dir);
    }

    [RelayCommand]
    private void ViewCode(WorkspaceMod? mod)
    {
        mod ??= SelectedWorkspaceMod;
        if (mod == null) { Status = "Pick a mod first."; return; }
        try { BuildLog = $"// ===== {Path.GetFileName(mod.MainFile)} =====\n\n" + File.ReadAllText(mod.MainFile); }
        catch (Exception e) { Status = "Couldn't read source: " + e.Message; }
    }

    [RelayCommand]
    private void EditMod(WorkspaceMod? mod)
    {
        if (mod != null) GameLauncher.OpenInEditor(mod.MainFile, EditorCmd);
    }

    [RelayCommand]
    private void DeleteMod(WorkspaceMod? mod)
    {
        if (mod == null) return;
        try { WorkspaceMods.Delete(mod); Status = $"Deleted source for “{mod.Name}” (the installed DLL stays — disable it on the Launcher tab)."; RefreshWorkspaceMods(); }
        catch (Exception e) { Status = "Delete failed: " + e.Message; }
    }

    private async Task BuildAndInstall(string name, string csproj, string modDir)
    {
        AppendBuild("Building…");
        bool ok = await BuildRunner.BuildAsync(DotnetPath, csproj, AppendBuild);
        if (!ok) { Status = $"Build failed for {name} — see the log."; return; }

        string dll = ModScaffolder.BuiltDll(modDir, name);
        if (ValidGame && File.Exists(dll))
        {
            ModManager.Install(GameDir!, dll);
            AppendBuild($"\n✓ Installed {name}.dll into the game's plugins folder.");
            RefreshMods();
            Status = $"Built + installed “{name}” (restart the game).";
        }
        else { Status = $"Built “{name}”, but couldn't install (no valid game folder or DLL missing)."; }
    }

    [RelayCommand]
    private void OpenWorkspace()
    {
        if (!string.IsNullOrWhiteSpace(WorkspaceDir) && Directory.Exists(WorkspaceDir))
            GameLauncher.OpenFolder(WorkspaceDir!);
        else Status = "No workspace folder set.";
    }

    [RelayCommand] private void SaveSettings() { Persist(); Status = "Settings saved."; RefreshMods(); CheckDotnet(); }

    private void AppendBuild(string line) => Dispatcher.UIThread.Post(() => BuildLog += line + "\n");

    private void Persist()
    {
        _cfg.GameDir = GameDir; _cfg.WorkspaceDir = WorkspaceDir; _cfg.DotnetPath = DotnetPath;
        _cfg.LastTemplate = NewModTemplate.ToString();
        _cfg.EditorCmd = EditorCmd; _cfg.ShareDir = ShareDir;
        _cfg.Save();
    }
}
