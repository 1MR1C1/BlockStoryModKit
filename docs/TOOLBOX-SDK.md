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

### Game-access helpers (v1.51) — for deep / total-conversion mods
These wrap verified game calls so you (and the AI Builder) don't have to reverse-engineer them. Names of
game types/members are browsable in the Mod Kit's **Game API** tab.

- **`BSPlayer`** — `Stat(id)`, `Get(id)`/`GetMax(id)`, `Set(id,val)`, `SetMax(id,val)`, `Heal()`, `God(bool)`,
  `Teleport(Vector3)`, `Pos()`. `id` is `InvStat.Identifier` (Health, Mana, …).
- **`BSItems`** — `AllNames()`, `SlotOf(name)`, `Create(name,count)`, `Give(name,count)` (into a free slot).
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
modkit new <Name> [--template Panel|Minimal|KeybindAction] [--desc "..."]
modkit recipe <Name> <InfoPanel|MessagePopup|HealKey> [message]   (no-code)
modkit build <Name>           rebuild a workspace mod and install it
modkit play
```
Override the saved paths per-run with `--game DIR` / `--workspace DIR` / `--dotnet PATH`.
