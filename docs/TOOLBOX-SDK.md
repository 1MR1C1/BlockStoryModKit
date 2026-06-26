# Block Story Mod Kit — Toolbox SDK

How to build a Block Story mod that plugs into the **Core toolbox**.

## The big picture

The mod system is modular (Forge-style):

```
BlockStoryCore.dll   ← the toolbox / loader / shared API   (REQUIRED, load it once)
  ├─ VeinMiner.dll   ← each feature is its own droppable mod
  ├─ Xray.dll
  └─ <your mod>.dll  ← you build this; it depends on Core
```

Everything runs on the **BlockStoryCore framework (Mono)** and patches the game via **Harmony** / reflection — the
game's own assemblies are never modified. Mods are plain Unity `MonoBehaviour` plugins that call the
game's methods directly (mod code runs on the Unity main thread).

## Fastest path: the Mod Kit

1. **Settings tab** → set the game folder (Auto-detect usually finds it).
2. **Setup & Help tab** → **Install / update framework** — installs the framework + the Core toolbox
   straight into the game (the bundle is baked into the launcher; no separate download). The guides for each
   platform are on this tab too.
3. **Mod Builder tab** → pick a workspace folder → **Set up workspace** (copies the game's reference
   DLLs so your mod can compile).
4. Enter a name + description, pick a **starter template**, → **Create + Build + Install**. You now have a
   working mod loaded in the game. Use the **Your mods** list to **Edit**, **Rebuild + Install**, etc.

### No coding? Use the "Easy Mod" tab
Pick an action (info panel / message popup / heal key) + a key, type a name, click **Make my mod!** — it
generates a complete mod, sets up the workspace automatically, builds, and installs it. (CLI equivalent:
`modkit recipe <Name> <InfoPanel|MessagePopup|HealKey> [message]`.) Everything below is for writing mods by hand.

**Starter templates** (the dropdown):
- **Panel** — a rebindable key toggles a themed IMGUI window (good base for a tool with UI).
- **Minimal** — a bare plugin that just registers on the Mods page; add your own logic.
- **KeybindAction** — a rebindable key runs an action (no UI).
- **HarmonyPatch** — a commented example that hooks a game method so your code runs before/after it.
- **BlockWatcher** — fires a method every time any block is placed or broken.
- **WildCreature** — a real game enemy reskinned into your creature: the game's own NPCs fight it, it gets
  the native HP/level bar, and it comes with a placeable spawner + Soul-Catcher catch. Resistances, oxygen,
  daylight-burn etc. are all deletable lines. (See the creature SDK below.)
- **PetMount** — a tameable, **rideable** pet summoned from a craftable Soul item (recipe + shop price):
  click to ride or open its inventory; its own name + inventory, climbs steps, auto-attacks. The full pet.

**Installing / sharing mods** (Launcher tab): a built `.dll` lands in the game automatically, but you can
also **Install** a shared `.dll` / `.zip` pack / folder, **drag-and-drop** any of those onto the window,
**Backup mods**, enable/disable, and uninstall.

Or do it by hand — a mod is two files:

```
mods/<Name>/<Name>.csproj          # thin: just <AssemblyName>
mods/<Name>/<Name>Plugin.cs        # your BepInPlugin
```

(The shared `mods/Directory.Build.props` provides all the references.)

## A minimal mod

```csharp
using BepInEx;
using UnityEngine;
using BlockStoryCore;
using ISRef = UnityEngine.InputSystem.InputActionReference;

namespace BlockStoryMod
{
    [BepInPlugin("com.you.blockstory.superjump", "Block Story - Super Jump", "1.0.0")]
    [BepInDependency(Core.Guid)]                 // hard dependency: won't load without the toolbox
    public class SuperJumpPlugin : BaseUnityPlugin
    {
        private ISRef _key;

        private void Awake()
        {
            _key = BSKeybinds.Register("Super Jump", "Do Jump", "<Keyboard>/j");
            ModRegistry.Register(new ModInfo {
                Name = "Super Jump",
                Description = "Leap tall mountains.",
                GetEnabled = () => true,
                SetEnabled = on => { },
            });
        }

        private void Update()
        {
            if (BSKeybinds.Pressed(_key))
                Core.Log?.LogInfo("Jump! player at " + BSWorld.PlayerPos());
        }
    }
}
```

## The Core API (everything is in `namespace BlockStoryCore`)

### `Core`
- `Core.Guid` — the toolbox plugin GUID; use in `[BepInDependency(Core.Guid)]`.
- `Core.Log` — shared `ManualLogSource`; `Core.Log?.LogInfo("…")` (writes to the framework log).

### `BSKeybinds` — rebindable keybinds
- `ISRef Register(string section, string label, string defaultBinding)` — registers a key; it appears in
  the game's **Settings → Controls** screen (grouped by `section`), is rebindable, and persists.
  `defaultBinding` is an InputSystem path, e.g. `"<Keyboard>/x"`, `"<Keyboard>/f6"`, `"<Mouse>/middleButton"`.
- `bool Pressed(ISRef r)` — true on the frame the key goes down.

### `ModRegistry` / `ModInfo` — appear on the in-game "Mods" page
```csharp
ModRegistry.Register(new ModInfo {
    Name        = "My Mod",
    Description  = "What it does.",
    GetEnabled  = () => MyMod.Enabled,        // toolbox reads this for the ON/OFF switch
    SetEnabled  = on => MyMod.SetEnabled(on),
    HasConfig   = true,                        // show a gear button?
    OpenConfig  = () => { ModsPage.Close(); MyConfig.Open = true; },
});
```

### `MainMenuRegistry` — add a button to the game's main menu
```csharp
MainMenuRegistry.Add("MyButton", "My Button", () => MyPage.Open(), width: 1.0f);
```
Buttons lay out in a 2-row grid extending the menu; `width` widens the wood box for long labels.

### `Theme` — shared IMGUI skin (the wood/parchment look)
`Theme.Build()` (call once per OnGUI before using), then styles: `Window, Label, LabelGold, Button,
ButtonSel, Field`. Helpers: `Theme.NumberRow(label, ref buffer, max, ...)`, `Theme.DigitsOnly(s)`.
Textures are `HideAndDontSave` so they survive scene loads. There's a `Theme.Version` that bumps on
rebuild — cache derived styles against it.

### Game-access helpers — for deep / total-conversion mods
These wrap verified game calls so you don't have to reverse-engineer them. Names of game types/members are
browsable in the Mod Kit's **Game API** tab.

- **`BSPlayer`** — `Stat(id)`, `Get(id)`/`GetMax(id)`, `Set(id,val)`, `SetMax(id,val)`, `Heal()`, `God(bool)`,
  `Teleport(Vector3)`, `Pos()`. `id` is `InvStat.Identifier` (Health, Mana, …).
- **`BSItems`** — read/give: `AllNames()`, `SlotOf(name)`, `Find(name)`, `Create(name,count)`,
  `Give(name,count)` (into a free slot). Authoring (do these inside `BSItems.WhenReady(...)`, see below):
  `RegisterClone(template, newName)` (clone a game item into a new one), `SetIcon(name, tex, sprite)`,
  `RegisterRecipe(name, grid, result)`.
- **`BSItems.WhenReady(Action)`** — runs your callback once the item database is loaded (and again on each
  world load). Cloning items / registering recipes straight in `Awake()` is too early and silently fails —
  always wrap that setup in `WhenReady`.
- **`BSModel`** — build creature/icon art from code: `Humanoid(name, material)`, `FromBoxes(...)`,
  `SolidMaterial(color)`, `SkinMaterial(tex)` / `SkinMaterialFromFile(path)`, `TextureFromPng(bytes)` /
  `LoadPng(path)`, and `RenderIcon(model, size)` (snapshot a model into a transparent icon texture).
- **`BSWorld`** — `PlayerPos()`, `World()`/`InWorld`, `GetBlock(x,y,z)`, `SetBlock(x,y,z,id)` (id 1 = air),
  `Save()`, `SetTime(hour)`, `SetWeather(cloud,hum,wind)` / `ClearWeather()`/`Rain()`/`Storm()`, `Mode`.
- **`BSEvents`** — subscribe in `Awake`: `WorldLoaded(IWorld)`, `WorldUnloaded`, `Tick`, `BlockChanged(coord,block)`.
- **`BSReflect`** — `FindType`, `FindInstance`, `Get`/`Set`/`Call`/`CallStatic` — reach *any* game system by name.
- **`BSPatch`** — `PatchAll()` for your `[HarmonyPatch]` classes, or `Hook(type,method,prefix,postfix)` by name.
  For a complete overhaul, Harmony-patch the real game methods (find them in the Game API tab).

### Full-screen overlays — block menu click-through
The game's NGUI menu reads raw input independently of IMGUI, so a full-screen IMGUI overlay must tell the
toolbox to suppress menu input:
```csharp
Overlay.ConfigOpen = true;   // while your overlay is open
Overlay.ConfigOpen = false;  // when it closes
```
`Core.MenuInputGuard` then disables the NGUI `UICamera` so clicks don't fall through to the menu buttons
behind your panel.

## Custom creatures & pets (the creature SDK)

The **WildCreature** and **PetMount** templates are complete, commented starting points — generate one and
read it first; this is the reference. Your creature's body is built from code with `BSModel`; everything
else is plumbing in Core.

### Wild creature — reskin a real enemy (`BSReskin`)

Take a real game enemy the NPCs already fight, strip its look + brain, and drape your model + AI on top. You
inherit native NPC combat (both ways) and the real in-game HP/level bar for free.

```csharp
BSReskin.Spawn("Barlog", pos, def, BuildModel, opts);   // host MOBType, where, stats, model, options
```

- **`BSMobDef def`** — `Behaviour` (Passive / Neutral / Hostile), `MaxHealth`, `MoveSpeed`,
  `AttackDamage/Range/Cooldown`, `SightRange`, distance-based level scaling, a `Loot` table (drops on the
  ground, not into a bag), and `XpReward` (level-scaled, paid to the player and/or the pet that landed the kill).
- **`BSReskinOpts opts`** — every hazard is a toggle, defaulting to "your creature shrugs it off":
  `RequiresOxygen`, `BurnsInDaylight`, `DiesInSpace`. Plus `Regen`, `ModelScale`, `BarLift`, and `Resists`
  (build with `BSResists.New().Immune(id).Resist(id).Weak(id).Build()`).

### Placeable spawner + soul-catch (`BSSpawner`)

```csharp
BSItems.WhenReady(() => {
    BSItems.RegisterClone("Antique Spawner", "My Spawner");       // a craftable spawner item
    BSSpawner.Register("My Spawner", "Barlog", def, BuildModel, opts);
});
```

Placing the item spawns your creature; mining it gives the item back; catching a wild one with the Soul
Catcher drops a spawner so it's re-placeable. Each spawner gets a stable, name-derived block marker, so two
creatures can safely share the same host enemy.

### Pet / mount — a rideable soul pet (`BSPet`)

```csharp
BSItems.WhenReady(() => BSPet.Register(new BSPetDef {
    SoulItem = "My Soul", CloneFrom = "Alien Dog Soul",
    DisplayName = "My Pet", Price = 500, Model = BuildModel,
    MaxHealth = 800f, HpPerLevel = 500f, AttackPerLevel = 15f,
    Recipe = BuildRecipe,            // optional 3x3 grid; omit = shop-only
}));
```

Clones a real pet soul (inheriting the whole summon / ride / saddle system), reskins the summoned pet into
your creature, and gives it its own name + inventory, step-climbing, click-to-ride, and a rendered icon
(shown on the soul in the recipe book, inventory, in-hand preview, and the world drop). Equip the soul to
summon; click the pet to ride or open its inventory. Supply your own `Icon` to override the rendered one.

### A pure code-mob (`BSMob`) — advanced

For a creature that *isn't* a reskin of a real enemy, `BSMob.Define(def)` + `BSMob.Spawn(name, pos)` /
`BSMob.SpawnAroundPlayer(name, count)` runs a fully custom mob (its own model, `BSMobAI`, and health bar).
It's less integrated than a reskin (no native bar, no soul-catch), so reach for **WildCreature** first.

## Reading the world / blocks (advanced, via reflection on game types)

`TerrainLoader.instance` → `world` (`IWorld`), and `world[new Vector3i(x,y,z)]` gets/sets a `Block`
(`new Block(id, data, paint)`). **Air is block id 1, not 0.** Every block change fires
`IWorld.BlockPlacedEvent(coord, block)`. Player damage: `PlayerHealth.Attacked(-amount, null, null)`.

## Build + install

The Mod Kit's **Create + Build + Install** builds your mod and installs it for you. Restart the game —
your mod shows up on the **Mods** page and its keybinds under **Settings → Controls**.

## Cross-platform

The same DLLs work on Windows and on Linux/Proton. The Mod Kit handles launching with the right options on
each platform.

## CLI (`modkit`)

A terminal front-end over the same Core (and the same saved settings as the GUI) lives in `src/ModKit.Cli`.
Build it with `dotnet build src/ModKit.Cli -c Release`. Common commands:

```
modkit status                 game / framework / workspace / .NET status
modkit list                   installed mods ([x]=enabled)
modkit install <dll|zip|dir>  install a mod / pack / folder
modkit enable|disable <Name>
modkit uninstall <Name>
modkit backup                 zip the plugins folder
modkit setup --base <zip>     install the framework into the game
modkit new <Name> [--template Panel|Minimal|KeybindAction|HarmonyPatch|BlockWatcher|WildCreature|PetMount] [--desc "..."]
modkit recipe <Name> <InfoPanel|MessagePopup|HealKey> [message]   (no-code)
modkit build <Name>           rebuild a workspace mod and install it
modkit play
```
Override the saved paths per-run with `--game DIR` / `--workspace DIR` / `--dotnet PATH`.
