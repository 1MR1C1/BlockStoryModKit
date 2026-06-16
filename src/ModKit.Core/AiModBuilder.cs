using System.Text;

namespace ModKit.Core;

public sealed class AiBuildResult
{
    public bool Success;
    public string Name = "";
    public string ModDir = "";
    public string Csproj = "";
    public string DllPath = "";
    public string LastCode = "";
    public int Attempts;
    public string? Error;
}

public static class AiModBuilder
{
    public const string SystemPrompt = """
You are an expert C# mod author for the game Block Story. You write BepInEx 5 (Mono / .NET) plugins that
use the BlockStoryCore toolbox. You output ONE complete C# source file and nothing else.

HARD RULES:
- Output ONLY a single ```csharp fenced code block. No prose, no explanation, no extra files.
- namespace must be: BlockStoryMod
- The plugin class must be named exactly {CLASSNAME} and inherit BaseUnityPlugin.
- Decorate it with [BepInPlugin("com.modkit.blockstory.{LOWERNAME}", "Block Story - {NAME}", "1.0.0")]
  and [BepInDependency(Core.Guid)].
- In Awake(), call ModRegistry.Register(new ModInfo { ... }) so it shows on the in-game Mods page.
- Only reference APIs you are sure exist (BCL, UnityEngine, the toolbox below, and Harmony). Do NOT invent
  game classes. If you must hook a game method, use Harmony and keep the typeof(...) target generic with a
  comment, OR prefer the toolbox APIs and world events listed below.
- When the user asks for a change, return the FULL updated file (not a diff).

TOOLBOX (namespace BlockStoryCore):
- Core.Guid : string  -> use in [BepInDependency(Core.Guid)]
- Core.Log  : BepInEx ManualLogSource -> Core.Log?.LogInfo("...")
- Keybinds — at the TOP of the file add this alias:  using ISRef = UnityEngine.InputSystem.InputActionReference;
    then:   ISRef _key = BSKeybinds.Register("MyMod", "Do Thing", "<Keyboard>/h");   // a field, set in Awake()
    and:    if (BSKeybinds.Pressed(_key)) { /* ... */ }                              // true the frame it's pressed
    defaultBinding is an InputSystem path: "<Keyboard>/g", "<Keyboard>/f6", "<Mouse>/middleButton".
    NEVER write "BSKeybinds.ISRef" — ISRef is ONLY the alias above, used as a plain type name.
- ModRegistry.Register(new ModInfo { Name, Description, GetEnabled=()=>Enabled, SetEnabled=on=>{...}, HasConfig=false })
- MainMenuRegistry.Add(string id, string label, System.Action onClick, float width = 1f)
- Theme.Build() once per OnGUI, then styles: Theme.Window, Theme.Label, Theme.LabelGold, Theme.Button, Theme.Field
- BSWorld.PlayerPos() : UnityEngine.Vector3?  (null on menu / before a world loads)
- Overlay.ConfigOpen = true/false  (set true while a full-screen IMGUI overlay is open, false when closed)

HIGH-LEVEL HELPERS (prefer these — they wrap verified game calls):
- BSPlayer: Stat(id), Get(id)/GetMax(id), Set(id,val), SetMax(id,val), Heal(), God(bool), Teleport(Vector3), Pos()
    id is InvStat.Identifier (e.g. InvStat.Identifier.Health, .Mana). Add: using ID = InvStat.Identifier;
- BSItems: AllNames(), SlotOf(name), Create(name,count), Give(name,count)->bool (drops into a free player slot)
- BSWorld: World():IWorld, InWorld, GetBlock(x,y,z)/GetBlock(Vector3i):Block?, SetBlock(x,y,z,id)/SetBlock(Vector3i,id,data,paint) (id 1 = air),
    Save(), SetTime(hour 0..24), SetWeather(cloud,humidity,wind), ClearWeather()/Rain()/Storm(), Mode (get/set WorldMode)
- BSEvents: events WorldLoaded(IWorld), WorldUnloaded, Tick, BlockChanged(Vector3i,Block) — subscribe in Awake (no polling needed)
- BSReflect: FindType(name), FindInstance(typeName), Get(obj,member)/Set(obj,member,val), Call(obj,method,args), CallStatic(typeName,method,args)
    — use for any game system not wrapped above; the "RELEVANT GAME API" section names real types/members.
- BSPatch (Harmony): BSPatch.PatchAll() to apply your [HarmonyPatch] classes; or BSPatch.Hook(typeName,method,prefix,postfix) by name.
    For DEEP / total-conversion mods, Harmony-patch the real game methods shown in the game-API section.

PATTERNS:
- Persist an on/off flag with PlayerPrefs.GetInt("{NAME}_Enabled", 1) != 0.
- Use a rebindable key (BSKeybinds) instead of polling UnityEngine.Input where possible.
- Keep it self-contained and compilable. Prefer simple, robust code over cleverness.
""";

    public static string ExtractErrors(string buildLog)
    {
        var lines = buildLog.Split('\n')
            .Where(l => l.Contains(": error", StringComparison.OrdinalIgnoreCase)
                     || l.Contains(": warning CS", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Trim())
            .Distinct()
            .Take(40)
            .ToList();
        string joined = string.Join("\n", lines);
        return joined.Length > 4000 ? joined.Substring(0, 4000) : (joined.Length == 0 ? buildLog : joined);
    }
}

public sealed class AiModSession
{
    public string Name { get; }
    public string ModDir { get; }
    public string Csproj { get; }
    public string CsFile { get; }
    public List<AiMessage> Conversation { get; } = new();

    readonly AiProvider _provider;
    readonly string? _apiKey;
    readonly string _model;
    readonly string? _endpoint;
    readonly string? _dotnetPath;
    readonly string _system;

    public AiModSession(string workspaceDir, string rawName, AiProvider provider, string? apiKey, string model,
        string? endpoint, string? dotnetPath)
    {
        Name = ModScaffolder.SanitizeName(rawName);
        _provider = provider; _apiKey = apiKey; _model = model; _endpoint = endpoint; _dotnetPath = dotnetPath;

        ModDir = Path.Combine(workspaceDir, "mods", Name);
        Directory.CreateDirectory(ModDir);
        Csproj = Path.Combine(ModDir, Name + ".csproj");
        if (!File.Exists(Csproj)) File.WriteAllText(Csproj, Templates.Csproj(Name));
        CsFile = Path.Combine(ModDir, Name + "Plugin.cs");

        _system = AiModBuilder.SystemPrompt
            .Replace("{CLASSNAME}", Name + "Plugin")
            .Replace("{LOWERNAME}", Name.ToLowerInvariant())
            .Replace("{NAME}", Name);
    }

    public string OpeningMessage(string description, string userPrompt) =>
        $"Create a Block Story mod named \"{Name}\".\n" +
        (string.IsNullOrWhiteSpace(description) ? "" : $"Short description: {description}\n") +
        $"It should do this:\n{userPrompt}\n\n" +
        $"Remember: output ONE ```csharp file, class {Name}Plugin, namespace BlockStoryMod.";

    public async Task<AiBuildResult> SendAsync(string userMessage, Action<string> onLog, int maxAttempts = 3, CancellationToken ct = default)
    {
        var r = new AiBuildResult { Name = Name, ModDir = ModDir, Csproj = Csproj };
        Conversation.Add(new AiMessage("user", userMessage));

        for (int attempt = 1; attempt <= maxAttempts && !ct.IsCancellationRequested; attempt++)
        {
            r.Attempts = attempt;
            onLog($"\n── AI attempt {attempt}/{maxAttempts} ({_provider}) — asking the model…");
            string reply;
            try { reply = await AiClient.ChatAsync(_provider, _apiKey, _model, _endpoint, _system, Conversation, ct); }
            catch (Exception e) { r.Error = e.Message; onLog("AI error: " + e.Message); return r; }
            Conversation.Add(new AiMessage("assistant", reply));

            string code = AiClient.ExtractCode(reply);
            if (string.IsNullOrWhiteSpace(code))
            {
                r.Error = "The AI returned no code.";
                onLog(r.Error);
                Conversation.Add(new AiMessage("user", "You returned no code. Output ONE ```csharp file as instructed."));
                continue;
            }
            r.LastCode = code;
            File.WriteAllText(CsFile, code);
            onLog($"Wrote {Name}Plugin.cs ({code.Length} chars). Compiling…");

            var log = new StringBuilder();
            bool ok = await BuildRunner.BuildAsync(_dotnetPath, Csproj, line => { onLog(line); log.AppendLine(line); });
            if (ok)
            {
                r.Success = true;
                r.DllPath = ModScaffolder.BuiltDll(ModDir, Name);
                onLog($"✓ Compiled on attempt {attempt}.");
                return r;
            }

            string errs = AiModBuilder.ExtractErrors(log.ToString());
            onLog("Build failed — sending the errors back to the AI to fix.");
            Conversation.Add(new AiMessage("user",
                "That code did not compile. Fix it and return the COMPLETE corrected file again as one ```csharp block. " +
                "Compiler errors:\n" + errs));
            r.Error = errs;
        }

        onLog($"\n✗ Gave up after {r.Attempts} attempts. You can edit the source and rebuild manually, or send another instruction.");
        return r;
    }
}
