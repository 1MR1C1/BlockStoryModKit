# Block Story Mod Kit — Feature List (v1.2.0)

An all-in-one desktop app for **installing, managing, building, and sharing mods** for Block Story
(Steam, Unity 6 / Mono). One download does the lot — no manual file copying, no separate framework setup.
Works on **Windows and Linux/Proton (incl. Steam Deck)**.

---

## 1. Launcher — manage your mods
- **Install mods** by file picker (`.dll` or `.zip` packs) **or drag-and-drop** a `.dll` / `.zip` / folder
  straight onto the window.
- **Enable / disable** any mod with a checkbox (no need to delete files).
- **Uninstall** a mod (Core toolbox is protected so it can't be removed by accident).
- **Mod profiles** — save a named set of enabled mods and re-apply it in one click (e.g. "PvP night", "Builder").
- **Search / filter** your installed mods.
- **Shows real version** of each mod, read from the DLL.
- **Dependency health check** — warns if your mods are enabled but the required toolbox is missing/disabled.
- **One-click Play** (launches via Steam, so all your launch options apply).
- **Backup mods** — zips your current mod set.
- **Check for updates** — compares your mods against a folder of newer builds and installs them.
- **Bug report** — bundles the game log + your mod list into a zip to share when something breaks.
- Quick buttons: open the plugins folder, open the log, check hotkey conflicts.

## 2. One-click framework setup
- Installs the whole modding framework (the BlockStoryCore runtime) **directly into your game** —
  no separate download or manual merge.
- Detects your Block Story folder automatically (across Steam libraries).
- Built-in, up-to-date **guides** (README, Windows install, Linux/Proton install, how to add mods) right in the app.
- Linux/Proton: tells you the one Steam launch option you need.

## 3. Mod Builder — make your own mods
- **Pre-coded starter templates** you flesh out — each is a real, compiling skeleton:
  - **Panel** — a hotkey opens a themed in-game window (UI + live player position).
  - **Minimal** — bare plugin that appears on the in-game Mods page.
  - **Keybind Action** — a rebindable key runs your code.
  - **Harmony Patch** — hook/replace any game method (with a commented example).
  - **Block Watcher** — react to any block placed or broken in the world.
  - **Wild Creature** — reskin a real game enemy into your own creature: the game's NPCs fight it, it gets the
    native health/level bar, drops loot + XP, and comes with a placeable spawner + Soul-Catcher catch. Oxygen,
    daylight-burn, space, resistances and more are all deletable options.
  - **Pet / Mount** — a tameable, **rideable** pet summoned from a craftable Soul item (recipe + shop price):
    its own name + inventory, climbs steps, auto-attacks, and its soul shows your creature everywhere.
- Plain-English description of each template right under the picker.
- **One-button "Make my mod"**: creates the project, compiles it, and installs it into your game.
- Auto-creates and sets up the workspace on first build — zero setup for newcomers.
- **Your mods** list with per-mod **Edit** (opens in VS Code / your editor), **Rebuild + Install**, **Open folder**,
  **Delete**, plus **View code** in-app.
- Live build output with clear success/error reporting.

## 4. Game API browser — know the whole game
- Reads your game's actual code and indexes **every public class, method, field, and enum** (incl. item & block IDs).
- **Searchable browser** — look up real game systems (inventory, health, blocks, biomes…) while you write a mod.
- Cached for instant loads; one-click re-index after a game update.

## 5. The toolkit (BlockStoryCore) — what mods can do
A documented API so mods get the common things as one-liners, and can reach *anything* for deep
total-conversion / overhaul mods:
- **In-game UI** — a themed Mods page + main-menu button, rebindable keybinds shown in the game's Controls screen.
- **BSPlayer** — read/set stats & vitals, heal, god mode, teleport.
- **BSItems / BSModel** — list/create/give items, clone game items, register recipes & icons, build models and
  render them to icons.
- **BSWorld** — read/set blocks at coordinates, time of day, weather, world mode.
- **BSEvents** — hook world load/unload, per-frame tick, and block place/break.
- **Creature SDK** — **BSReskin** (wild creatures), **BSSpawner** (spawner block + soul-catch), **BSPet**
  (rideable soul pets), **BSMob** (pure code-mobs) — the full Xenor-style creature toolkit.
- **BSReflect** — reach any game system by name (the backbone for overhaul mods).
- **BSPatch** — easy Harmony patching to hook or replace any game method.
- Full SDK reference included (`docs/TOOLBOX-SDK.md`).

> Note: this beta ships the **tool only** — no mods are bundled. You make your own.

## 6. Platform & extras
- **Windows + Linux/Proton + Steam Deck.**
- **GUI app** and a **command-line tool** (`modkit`) for terminal/scripting users.
- Custom app icon; single-file, self-contained builds (no separate runtime to install to run the app).

---

## Requirements
- **Block Story** (Steam).
- To **play** with mods: nothing extra (the app installs the framework). Linux needs one Steam launch option.
- To **build** mods: the **.NET 8 SDK** (the app auto-detects it and tells you if it's missing — not needed just to play).

## Known limitations
- Mods load at game **startup** only — installing a mod takes effect on the next restart (no hot-reload).
- A pre-existing build workspace may need a one-time "Set up workspace" after a framework update to see new toolkit APIs.
