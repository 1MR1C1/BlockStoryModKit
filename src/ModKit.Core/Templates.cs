namespace ModKit.Core;

public enum ModTemplate { Panel, Minimal, KeybindAction, HarmonyPatch, BlockWatcher }

internal static class Templates
{
    public static string Csproj(string name) =>
$@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <AssemblyName>{name}</AssemblyName>
  </PropertyGroup>
</Project>
";

    public static string Plugin(string name, string description, ModTemplate template) => template switch
    {
        ModTemplate.Minimal => Minimal(name, description),
        ModTemplate.KeybindAction => KeybindAction(name, description),
        ModTemplate.HarmonyPatch => HarmonyPatch(name, description),
        ModTemplate.BlockWatcher => BlockWatcher(name, description),
        _ => Panel(name, description),
    };

    private static string Head(string name) =>
        $@"[BepInPlugin(""com.yourname.blockstory.{name.ToLowerInvariant()}"", ""Block Story - {name}"", ""1.0.0"")]
    [BepInDependency(Core.Guid)]               // hard dependency on the toolbox; won't load without it";

    private static string Minimal(string name, string description) =>
$@"using BepInEx;
using BlockStoryCore;

namespace BlockStoryMod
{{
    // {description}
    {Head(name)}
    public class {name}Plugin : BaseUnityPlugin
    {{
        private void Awake()
        {{
            ModRegistry.Register(new ModInfo
            {{
                Name = ""{name}"",
                Description = ""{description}"",
                GetEnabled = () => true,
                SetEnabled = _ => {{ }},
                HasConfig = false,
            }});
            Core.Log?.LogInfo(""{name} loaded."");
        }}
    }}
}}
";

    private static string KeybindAction(string name, string description) =>
$@"using BepInEx;
using UnityEngine;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{{
    // {description}
    {Head(name)}
    public class {name}Plugin : BaseUnityPlugin
    {{
        public static bool Enabled = PlayerPrefs.GetInt(""{name}_Enabled"", 1) != 0;
        private ISRef _key;

        private void Awake()
        {{
            _key = BSKeybinds.Register(""{name}"", ""Activate {name}"", ""<Keyboard>/g"");
            ModRegistry.Register(new ModInfo
            {{
                Name = ""{name}"",
                Description = ""{description}"",
                GetEnabled = () => Enabled,
                SetEnabled = on => {{ Enabled = on; PlayerPrefs.SetInt(""{name}_Enabled"", on ? 1 : 0); PlayerPrefs.Save(); }},
                HasConfig = false,
            }});
            Core.Log?.LogInfo(""{name} loaded."");
        }}

        private void Update()
        {{
            if (!Enabled) return;
            if (BSKeybinds.Pressed(_key))
            {{
                // >>> your action here <<<  (example: log where the player is)
                Vector3? p = BSWorld.PlayerPos();
                Core.Log?.LogInfo(""{name}: activated at "" + (p?.ToString(""F1"") ?? ""(menu)""));
            }}
        }}
    }}
}}
";

    private static string Panel(string name, string description) =>
$@"using BepInEx;
using UnityEngine;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{{
    // {description}
    {Head(name)}
    public class {name}Plugin : BaseUnityPlugin
    {{
        public static bool Enabled = PlayerPrefs.GetInt(""{name}_Enabled"", 1) != 0;
        private ISRef _key;
        private bool _open;
        private Rect _win = new Rect(60, 60, 320, 200);

        private void Awake()
        {{
            _key = BSKeybinds.Register(""{name}"", ""Toggle {name}"", ""<Keyboard>/g"");
            ModRegistry.Register(new ModInfo
            {{
                Name = ""{name}"",
                Description = ""{description}"",
                GetEnabled = () => Enabled,
                SetEnabled = on => {{ Enabled = on; PlayerPrefs.SetInt(""{name}_Enabled"", on ? 1 : 0); PlayerPrefs.Save(); if (!on) _open = false; }},
                HasConfig = false,
            }});
            Core.Log?.LogInfo(""{name} loaded."");
        }}

        private void Update()
        {{
            if (!Enabled) return;
            if (BSKeybinds.Pressed(_key)) _open = !_open;
        }}

        private void OnGUI()
        {{
            if (!Enabled || !_open) return;
            Theme.Build();
            _win = GUILayout.Window(GetInstanceID(), _win, DrawWindow, ""{name}"", Theme.Window);
        }}

        private void DrawWindow(int id)
        {{
            GUILayout.Label(""Player position:"", Theme.LabelGold);
            GUILayout.Label(BSWorld.PlayerPos()?.ToString(""F1"") ?? ""(not in a world)"", Theme.Label);
            GUILayout.Space(8);
            if (GUILayout.Button(""Log a message"", Theme.Button))
                Core.Log?.LogInfo(""{name}: button clicked!"");
            GUI.DragWindow(new Rect(0, 0, 100000, 26));
        }}
    }}
}}
";

    private static string HarmonyPatch(string name, string description) =>
$@"using BepInEx;
using HarmonyLib;
using BlockStoryCore;

namespace BlockStoryMod
{{
    // {description}
    {Head(name)}
    public class {name}Plugin : BaseUnityPlugin
    {{
        private void Awake()
        {{
            var harmony = new Harmony(""com.yourname.blockstory.{name.ToLowerInvariant()}"");
            harmony.PatchAll();   // applies every [HarmonyPatch] class below
            ModRegistry.Register(new ModInfo
            {{
                Name = ""{name}"",
                Description = ""{description}"",
                GetEnabled = () => true,
                SetEnabled = _ => {{ }},
                HasConfig = false,
            }});
            Core.Log?.LogInfo(""{name} loaded."");
        }}
    }}

    // >>> GIVE THIS A BODY <<<
    // 1. Find a method to hook (e.g. with ILSpy on Block Story_Data/Managed/Assembly-CSharp.dll).
    // 2. Replace TYPE / ""MethodName"" below, then uncomment.
    // Prefix runs before the original (return false to skip it); Postfix runs after.
    //
    // [HarmonyPatch(typeof(PlayerHealth), ""Attacked"")]
    // static class {name}_Patch
    // {{
    //     static void Prefix() {{ Core.Log?.LogInfo(""{name}: before Attacked""); }}
    //     static void Postfix() {{ Core.Log?.LogInfo(""{name}: after Attacked""); }}
    // }}
}}
";

    private static string BlockWatcher(string name, string description) =>
$@"using BepInEx;
using UnityEngine;
using BlockStoryCore;
using Blocksters.MathLib;
using Blocksters.Terrain.chunk;

namespace BlockStoryMod
{{
    // {description}
    {Head(name)}
    public class {name}Plugin : BaseUnityPlugin
    {{
        public static bool Enabled = PlayerPrefs.GetInt(""{name}_Enabled"", 1) != 0;
        private IWorld _world;

        private void Awake()
        {{
            ModRegistry.Register(new ModInfo
            {{
                Name = ""{name}"",
                Description = ""{description}"",
                GetEnabled = () => Enabled,
                SetEnabled = on => {{ Enabled = on; PlayerPrefs.SetInt(""{name}_Enabled"", on ? 1 : 0); PlayerPrefs.Save(); }},
                HasConfig = false,
            }});
            Core.Log?.LogInfo(""{name} loaded."");
        }}

        private void Update()
        {{
            // keep our event hooked to whatever world is currently loaded
            var tl = TerrainLoader.instance;
            var w = tl != null ? tl.world : null;
            if (!ReferenceEquals(w, _world))
            {{
                if (_world != null) _world.BlockPlacedEvent -= OnBlock;
                _world = w;
                if (_world != null) _world.BlockPlacedEvent += OnBlock;
            }}
        }}

        private void OnBlock(Vector3i coord, Block block)
        {{
            if (!Enabled) return;
            // >>> GIVE THIS A BODY <<<  fires on every place AND break (break => air, id 1)
            Core.Log?.LogInfo(""{name}: block "" + block.id + "" at "" + coord);
        }}
    }}
}}
";
}
