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
        + (Mod.IsCore ? "   (required)" : "");
    public bool CanToggle => !Mod.IsCore;

    [ObservableProperty] private bool _enabled;

    public ModRow(InstalledMod m, MainWindowViewModel owner) { Mod = m; _owner = owner; _enabled = m.Enabled; }
    partial void OnEnabledChanged(bool value) => _owner.ToggleMod(this, value);
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Config _cfg;

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
        _                         => "A hotkey opens a themed pop-up window. Ready-made UI with a button + live player position — extend the panel.",
    };

    [ObservableProperty] private string _aiModName = "MyAiMod";
    [ObservableProperty] private string _aiPrompt = "";
    [ObservableProperty] private string _aiChatInput = "";
    [ObservableProperty] private string _aiTranscript = "";
    [ObservableProperty] private bool _aiBusy;
    [ObservableProperty] private bool _aiHasSession;
    private AiModSession? _aiSession;
    public string[] AiProviders { get; } = { "Claude Code", "Anthropic", "OpenAI", "Ollama" };
    [ObservableProperty] private string _aiProvider = "Anthropic";
    [ObservableProperty] private string? _anthropicKey;
    [ObservableProperty] private string? _openAiKey;
    [ObservableProperty] private string _anthropicModel = AiClient.DefaultAnthropicModel;
    [ObservableProperty] private string _openAiModel = AiClient.DefaultOpenAiModel;
    [ObservableProperty] private string _ollamaEndpoint = AiClient.DefaultOllamaEndpoint;
    [ObservableProperty] private string _ollamaModel = AiClient.DefaultOllamaModel;
    [ObservableProperty] private string _claudeCodeModel = AiClient.DefaultClaudeCodeModel;

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
        _cfg = Config.Load();
        _gameDir = _cfg.GameDir ?? GamePaths.AutoDetect();
        _workspaceDir = _cfg.WorkspaceDir;
        _dotnetPath = _cfg.DotnetPath;
        if (Enum.TryParse(_cfg.LastTemplate, out ModTemplate t)) _newModTemplate = t;
        _aiProvider = _cfg.AiProvider; _anthropicKey = _cfg.AnthropicKey; _openAiKey = _cfg.OpenAiKey;
        _anthropicModel = _cfg.AnthropicModel; _openAiModel = _cfg.OpenAiModel;
        _ollamaEndpoint = _cfg.OllamaEndpoint; _ollamaModel = _cfg.OllamaModel; _claudeCodeModel = _cfg.ClaudeCodeModel;

        if (_cfg.AiProvider == "Anthropic" && string.IsNullOrWhiteSpace(_cfg.AnthropicKey) && AiClient.FindClaude() != null)
            _aiProvider = "Claude Code";
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

    [RelayCommand]
    private void CheckUpdates()
    {
        if (!ValidGame) { Status = "Set a valid game folder first."; return; }
        if (string.IsNullOrWhiteSpace(ShareDir) || !Directory.Exists(ShareDir)) { Status = "Set a valid 'updates' folder in Settings first."; SelectedTab = 2; return; }
        try
        {
            var updates = UpdateCheck.Find(GameDir!, ShareDir!);
            if (updates.Count == 0) { Status = "All your mods are up to date. ✓"; return; }
            foreach (var u in updates) ModManager.Install(GameDir!, u.SourcePath);
            RefreshMods();
            Status = $"Updated {updates.Count} mod(s): " + string.Join(", ", updates.Select(u => $"{u.Name} {u.InstalledVersion}→{u.AvailableVersion}")) + " (restart the game).";
        }
        catch (Exception e) { Status = "Update check failed: " + e.Message; }
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

    partial void OnGameDirChanged(string? value) { Persist(); RefreshMods(); }
    partial void OnWorkspaceDirChanged(string? value) { Persist(); RefreshWorkspaceMods(); }
    partial void OnNewModTemplateChanged(ModTemplate value) { Persist(); OnPropertyChanged(nameof(TemplateBlurb)); }
    partial void OnAiProviderChanged(string value) => Persist();

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

    [RelayCommand]
    private async Task GenerateModWithAi()
    {
        if (AiBusy) return;
        if (string.IsNullOrWhiteSpace(AiPrompt)) { Status = "Describe what the mod should do first."; return; }
        if (!await PrepareAi()) return;

        var (provider, key, model) = ResolveAi();
        _aiSession = new AiModSession(WorkspaceDir!, AiModName, provider, key, model, OllamaEndpoint, DotnetPath);
        AiHasSession = true;
        AiTranscript = "";
        BuildLog = $"🤖 Asking {AiProvider} to build “{_aiSession.Name}”…\n";
        await RunAiTurn(_aiSession.OpeningMessage("", AiPrompt), $"Build “{AiModName}”: {AiPrompt}");
    }

    [RelayCommand]
    private async Task SendAiMessage()
    {
        if (AiBusy) return;
        if (_aiSession == null) { Status = "Start a mod with “Build with AI” first, then refine it here."; return; }
        if (string.IsNullOrWhiteSpace(AiChatInput)) return;
        if (!await PrepareAi()) return;
        string msg = AiChatInput.Trim();
        AiChatInput = "";
        await RunAiTurn(msg, msg);
    }

    private async Task<bool> PrepareAi()
    {
        if (!ValidGame) { Status = "Install the modding framework first (Setup & Help tab)."; SelectedTab = 3; return false; }
        if (BuildRunner.DotnetVersion(DotnetPath) == null) { Status = "Need the .NET SDK to build mods — install it, then set its path in Settings."; return false; }
        var (provider, _, _) = ResolveAi();
        if (provider == ModKit.Core.AiProvider.Anthropic && string.IsNullOrWhiteSpace(AnthropicKey)) { Status = "Set your Anthropic API key in Settings first (or switch backend to “Claude Code” to use your subscription)."; SelectedTab = 2; return false; }
        if (provider == ModKit.Core.AiProvider.OpenAI && string.IsNullOrWhiteSpace(OpenAiKey)) { Status = "Set your OpenAI API key in Settings first."; SelectedTab = 2; return false; }
        if (provider == ModKit.Core.AiProvider.ClaudeCode && AiClient.FindClaude() == null) { Status = "Claude Code isn't installed. Install it + run `claude` once to sign in, or pick another backend in Settings."; SelectedTab = 2; return false; }

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
        await EnsureApi();
        return true;
    }

    private async Task RunAiTurn(string sessionMessage, string transcriptUserText)
    {
        AiBusy = true;
        AddChat("You", transcriptUserText);
        Status = "AI is working…";
        try
        {

            string apiCtx = ApiContextFor(transcriptUserText);
            string fullMessage = string.IsNullOrEmpty(apiCtx) ? sessionMessage : apiCtx + "\n\n" + sessionMessage;
            AiBuildResult res = await _aiSession!.SendAsync(fullMessage, AppendBuild);
            if (res.Success && ValidGame && File.Exists(res.DllPath))
            {
                ModManager.Install(GameDir!, res.DllPath);
                AppendBuild($"\n✓ Installed {res.Name}.dll into the game's plugins folder.");
                RefreshMods(); RefreshWorkspaceMods();
                AddChat("AI", $"✓ Built + installed “{res.Name}” ({res.Attempts} attempt(s)). Restart the game to use it. Tell me what to change next.");
                Status = $"🎉 “{res.Name}” installed — restart the game (or refine it below).";
            }
            else
            {
                AddChat("AI", res.Error != null ? "Couldn't build it: " + res.Error.Split('\n')[0] + " — try rephrasing or adding detail." : "Couldn't build it — see Build output.");
                Status = "AI couldn't build it — refine your request and Send again.";
                RefreshWorkspaceMods();
            }
        }
        catch (Exception e) { Status = "AI failed: " + e.Message; AppendBuild(e.ToString()); }
        finally { AiBusy = false; }
    }

    private (ModKit.Core.AiProvider provider, string? key, string model) ResolveAi()
    {

        if (!Enum.TryParse<ModKit.Core.AiProvider>((AiProvider ?? "").Replace(" ", ""), out var p)) p = ModKit.Core.AiProvider.Anthropic;
        string? key = p switch { ModKit.Core.AiProvider.Anthropic => AnthropicKey, ModKit.Core.AiProvider.OpenAI => OpenAiKey, _ => null };
        string model = p switch
        {
            ModKit.Core.AiProvider.Anthropic => AnthropicModel,
            ModKit.Core.AiProvider.OpenAI => OpenAiModel,
            ModKit.Core.AiProvider.ClaudeCode => ClaudeCodeModel,
            _ => OllamaModel,
        };
        return (p, key, model);
    }

    private void AddChat(string who, string text) =>
        Dispatcher.UIThread.Post(() => AiTranscript += $"{who}: {text}\n\n");

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
            ApiStatus = $"Game API: {_api.Types.Count} types indexed. Search above; results feed the AI too.";
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

    private string ApiContextFor(string text) => _api?.ForAiContext(text) ?? "";

    [RelayCommand]
    private async Task AiFixMod(WorkspaceMod? mod)
    {
        if (mod == null || AiBusy) return;
        if (!await PrepareAi()) return;

        SelectedTab = 1;
        BuildLog = $"Building “{mod.Name}” to find the errors…\n";
        var log = new System.Text.StringBuilder();
        bool ok = await BuildRunner.BuildAsync(DotnetPath, mod.Csproj, l => { AppendBuild(l); log.AppendLine(l); });
        if (ok) { Status = $"“{mod.Name}” already compiles — nothing to fix."; return; }

        string errs = AiModBuilder.ExtractErrors(log.ToString());
        string code = File.Exists(mod.MainFile) ? File.ReadAllText(mod.MainFile) : "";

        var (provider, key, model) = ResolveAi();
        _aiSession = new AiModSession(WorkspaceDir!, mod.Name, provider, key, model, OllamaEndpoint, DotnetPath);
        AiHasSession = true;
        AiTranscript = "";
        AiModName = mod.Name;
        string msg =
            $"This Block Story mod \"{mod.Name}\" does NOT compile. Fix the compiler errors and return the COMPLETE corrected file as one ```csharp block.\n\n" +
            "CURRENT CODE:\n```csharp\n" + code + "\n```\n\nCOMPILER ERRORS:\n" + errs;
        await RunAiTurn(msg, $"Fix the build errors in “{mod.Name}”");
    }

    [RelayCommand]
    private void RecommendLocalModel()
    {
        OllamaModel = AiSetup.RecommendModel(s => Status = s);
        Persist();
    }

    [RelayCommand]
    private async Task SetupLocalAi()
    {
        if (AiBusy) return;
        AiBusy = true;
        SelectedTab = 2;
        BuildLog = "";
        Status = "Setting up free local AI…";
        try
        {

            if (string.IsNullOrWhiteSpace(OllamaModel) || OllamaModel.Trim() == "qwen2.5-coder")
                OllamaModel = AiSetup.RecommendModel(AppendBuild);
            bool ok = await AiSetup.EnsureLocalAiAsync(OllamaEndpoint, OllamaModel, AppendBuild);
            if (ok)
            {
                AiProvider = "Ollama";
                Persist();
                Status = "✓ Free local AI is ready — backend set to Ollama. Try the AI Builder (no key needed).";
            }
            else Status = "Local AI setup didn't finish — see the log for what to do.";
        }
        catch (Exception e) { Status = "Local AI setup failed: " + e.Message; AppendBuild(e.ToString()); }
        finally { AiBusy = false; }
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
    private async Task ExplainMod(WorkspaceMod? mod)
    {
        mod ??= SelectedWorkspaceMod;
        if (mod == null || AiBusy) { if (mod == null) Status = "Pick a mod first."; return; }
        var (provider, key, model) = ResolveAi();
        if (provider == ModKit.Core.AiProvider.Anthropic && string.IsNullOrWhiteSpace(key)) { Status = "Set an AI backend in Settings (or use Claude Code)."; SelectedTab = 2; return; }
        if (provider == ModKit.Core.AiProvider.OpenAI && string.IsNullOrWhiteSpace(key)) { Status = "Set your OpenAI key in Settings."; SelectedTab = 2; return; }
        if (provider == ModKit.Core.AiProvider.ClaudeCode && AiClient.FindClaude() == null) { Status = "Claude Code isn't installed; pick another backend."; SelectedTab = 2; return; }

        string code;
        try { code = File.ReadAllText(mod.MainFile); } catch (Exception e) { Status = "Couldn't read source: " + e.Message; return; }
        AiBusy = true;
        BuildLog = $"🤖 Explaining “{mod.Name}”…\n\n";
        Status = "AI is reading the mod…";
        try
        {
            var msgs = new List<AiMessage> { new("user", "Explain what this Block Story mod does in plain English, as a few short bullet points (no code). Mention its hotkey/toggle if any.\n\n```csharp\n" + code + "\n```") };
            string reply = await AiClient.ChatAsync(provider, key, model, OllamaEndpoint,
                "You explain C# game mods clearly and briefly for someone who isn't a programmer.", msgs);
            BuildLog += reply;
            Status = $"Explained “{mod.Name}”.";
        }
        catch (Exception e) { Status = "Explain failed: " + e.Message; AppendBuild(e.ToString()); }
        finally { AiBusy = false; }
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
        _cfg.AiProvider = AiProvider; _cfg.AnthropicKey = AnthropicKey; _cfg.OpenAiKey = OpenAiKey;
        _cfg.AnthropicModel = AnthropicModel; _cfg.OpenAiModel = OpenAiModel;
        _cfg.OllamaEndpoint = OllamaEndpoint; _cfg.OllamaModel = OllamaModel; _cfg.ClaudeCodeModel = ClaudeCodeModel;
        _cfg.EditorCmd = EditorCmd; _cfg.ShareDir = ShareDir;
        _cfg.Save();
    }
}
