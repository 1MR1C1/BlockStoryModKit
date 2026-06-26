namespace ModKit.Core;

public enum ModTemplate { Panel, Minimal, KeybindAction, HarmonyPatch, BlockWatcher, WildCreature, PetMount }

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
        ModTemplate.WildCreature => WildCreature(name, description),
        ModTemplate.PetMount => PetMount(name, description),
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

    // A WILD creature: a real game enemy reskinned, so the game's own NPCs fight it + you get the real HP/level bar.
    // Includes a placeable spawner + soul-catch. Every option is a deletable line.
    private static string WildCreature(string name, string description) =>
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
        private ISRef _spawnKey;

        // a WILD creature is a REAL game enemy reskinned, so the game's NPCs/animals fight it (and it gets the real
        // in-game health/level/name bar for free). pick a host the game's NPCs already attack.
        private const string Host = ""Barlog"";   // a ground enemy (avoid flyers - melee NPCs can't reach those)
        private const string MobName = ""{name}"";

        private BSMobDef _def;
        private BSReskinOpts _opts;

        private void Awake()
        {{
            // ---- STATS / LOOT / XP ----
            _def = new BSMobDef
            {{
                Name = MobName,
                Behaviour = MobBehaviour.Hostile,   // Hostile = hunts + attacks (Neutral would only wander)
                MaxHealth = 120f,
                MoveSpeed = 3.4f,
                AttackDamage = 12f,
                AttackRange = 3.0f,
                SightRange = 16f,
                ScaleByDistance = true,   // tougher the further from world spawn
                DistanceMaxLevel = 50,
                HealthGrowth = 0.12f,
                DamagePerLevel = 0.06f,
                XpReward = 40,            // xp to the killer, times its level (Survival only)
            }};
            _def.Loot.Add((""Meat"", 1, 3, 1.0f));
            _def.Loot.Add((""Bone"", 1, 2, 0.6f));

            // ---- OPTIONS - keep, change, or DELETE any line ----
            _opts = new BSReskinOpts
            {{
                ModelScale = 1.7f,           // world size of your model
                Lift = 1f,
                RenameBarFrom = Host,        // swap the host's name on the bar for yours
                Regen = 0.1f,                // self-heal/sec when out of combat (0 = none)

                // hazards: false = your creature shrugs it off; true = it suffers it
                RequiresOxygen = false,      // true = drowns without air (in space)
                BurnsInDaylight = false,     // true = burns in sunlight (undead-style)
                DiesInSpace = false,         // true = needs a space suit in space
                KillHostFire = true,         // turn off the host's flame effects

                // resist / weakness / immunity (delete to keep the host's own table)
                Resists = BSResists.New()
                    .Immune(InvEffect.Identifier.AcidDamage)
                    .Resist(InvEffect.Identifier.PoisonDamage)   // takes 20%
                    .Weak(InvEffect.Identifier.FireMagic)        // takes 150%
                    .Build(),
            }};

            // ---- PLACEABLE SPAWNER + SOUL-CATCH (delete this block to skip it) ----
            // makes a ""{name} Spawner"" item (a reskinned Antique Spawner): placing it spawns your creature, mining it
            // gives it back, and catching a wild one with the Soul Catcher drops a spawner so it's re-placeable.
            // (WhenReady = wait for the item DB to load - cloning an item straight in Awake is too early and fails.)
            BSItems.WhenReady(() =>
            {{
                BSItems.RegisterClone(""Antique Spawner"", ""{name} Spawner"");
                BSSpawner.Register(""{name} Spawner"", Host, _def, BuildModel, _opts);
            }});

            // ---- a hotkey to spawn one in front of you, for testing ----
            _spawnKey = BSKeybinds.Register(""{name}"", ""Spawn {name}"", ""<Keyboard>/g"");

            ModRegistry.Register(new ModInfo
            {{
                Name = ""{name}"",
                Description = ""{description}"",
                GetEnabled = () => Enabled,
                SetEnabled = on => {{ Enabled = on; PlayerPrefs.SetInt(""{name}_Enabled"", on ? 1 : 0); PlayerPrefs.Save(); }},
                HasConfig = false,
            }});
            Core.Log?.LogInfo(""{name} loaded - press G in a world to spawn a wild one."");
        }}

        private void Update()
        {{
            if (!Enabled) return;
            if (BSKeybinds.Pressed(_spawnKey))
            {{
                Vector3? p = BSWorld.PlayerPos();
                if (p != null) BSReskin.Spawn(Host, p.Value + Vector3.forward * 3f, _def, BuildModel, _opts);
            }}
        }}

        // your creature's body. default = a coloured humanoid; recolour it, use a 64x64 skin, or build a custom shape
        // (see the Custom Creature template's commented BuildCritter / BSModel.FromBoxes example).
        private GameObject BuildModel() => BSModel.Humanoid(MobName, BSModel.SolidMaterial(new Color(0.5f, 0.15f, 0.15f)));
    }}
}}
";

    // A tameable, RIDEABLE pet summoned by a craftable Soul item. Core clones a real pet soul (for the whole
    // summon/ride system) and reskins the summoned pet into your creature; it gets its own name + inventory.
    private static string PetMount(string name, string description) =>
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
        private ISRef _giveKey;

        private void Awake()
        {{
            // a RIDEABLE pet summoned by a craftable ""{name} Soul"". equip the soul in-world to summon it; click it to
            // ride or open its inventory. it has its own name + inventory, climbs steps, and auto-attacks while ridden.
            // (WhenReady = wait for the item DB to load - cloning the soul item straight in Awake is too early and fails.)
            BSItems.WhenReady(() => BSPet.Register(new BSPetDef
            {{
                SoulItem       = ""{name} Soul"",
                CloneFrom      = ""Alien Dog Soul"",   // base pet to clone (gives the ride/summon machinery)
                DisplayName    = ""{name}"",
                Price          = 500,                 // diamond shop price
                MaxHealth      = 800f,
                HpPerLevel     = 500f,
                AttackPerLevel = 15f,
                Regen          = 0.1f,             // self-heal/sec out of combat
                Model          = BuildModel,

                // the soul item reuses the base pet's icon by default, so it LOOKS like the thing you cloned (it's still
                // a separate item - own pet, name + inventory). give it its own 32x32 png to tell them apart, e.g.:
                //   Icon = BSModel.LoadPng(System.IO.Path.Combine(BepInEx.Paths.PluginPath, ""{name}Icon.png"")), IconName = ""{name}Soul"",

                // optional 3x3 crafting recipe - DELETE this line to make the soul shop-only.
                Recipe = BuildRecipe,
            }}));

            // a test hotkey that just hands you the soul (the recipe needs rare mats) - delete it once you're done testing.
            _giveKey = BSKeybinds.Register(""{name}"", ""Give {name} Soul"", ""<Keyboard>/g"");

            ModRegistry.Register(new ModInfo
            {{
                Name = ""{name}"",
                Description = ""{description}"",
                GetEnabled = () => true,
                SetEnabled = _ => {{ }},
                HasConfig = false,
            }});
            Core.Log?.LogInfo(""{name} loaded - press G for the '{name} Soul' (or craft/buy it), then equip it to summon your pet."");
        }}

        private void Update()
        {{
            // G = give yourself one soul, equip it to your weapon slot to summon the pet (click it to ride / open inventory)
            if (BSKeybinds.Pressed(_giveKey)) BSItems.Give(""{name} Soul"", 1);
        }}

        // your pet's body. default = a coloured humanoid; recolour it, use a 64x64 skin, or build a custom shape.
        private GameObject BuildModel() => BSModel.Humanoid(""{name}"", BSModel.SolidMaterial(new Color(0.35f, 0.5f, 0.7f)));

        // a simple 3x3 recipe (a centrepiece in a ring of bone). change the items/counts - names must be real game items.
        private InvGameItem[,] BuildRecipe()
        {{
            var g = new InvGameItem[3, 3];
            g[0, 0] = BSItems.Create(""Bone"", 5); g[0, 1] = BSItems.Create(""Dragon Heart"", 1); g[0, 2] = BSItems.Create(""Bone"", 5);
            g[1, 0] = BSItems.Create(""Bone"", 5); g[1, 1] = BSItems.Create(""Diamond"", 3);      g[1, 2] = BSItems.Create(""Bone"", 5);
            g[2, 0] = BSItems.Create(""Bone"", 5); g[2, 1] = BSItems.Create(""Dragonscale"", 5);  g[2, 2] = BSItems.Create(""Bone"", 5);
            foreach (var c in g) if (c == null) return null;   // an item name didn't resolve -> skip the recipe
            return g;
        }}
    }}
}}
";
}
