# Block Story Mod Kit — Feature List (v1.0 beta)

An all-in-one desktop app for **installing, managing, building, and sharing mods** for Block Story
(Steam, Unity 6 / Mono). One download does the lot — no manual file copying, no separate framework setup,
and an AI that can write mods for you. Works on **Windows and Linux/Proton (incl. Steam Deck)**.

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
- Plain-English description of each template right under the picker.
- **One-button "Make my mod"**: creates the project, compiles it, and installs it into your game.
- Auto-creates and sets up the workspace on first build — zero setup for newcomers.
- **Your mods** list with per-mod **Edit** (opens in VS Code / your editor), **Rebuild + Install**, **Open folder**,
  **Delete**, plus **View code** (in-app) and **🤖 Explain** (AI explains the mod in plain English).
- Live build output with clear success/error reporting.

## 4. 🤖 AI Mod Builder — describe it, AI writes it
- **Describe a mod in plain English** → the AI writes the code → it compiles → if it errors, the AI reads the
  compiler errors and **fixes itself** (up to 3 rounds) → it installs into your game.
- **Chat to refine** — after the first build, keep a conversation: "make it stronger", "also play a sound" — it
  edits the same mod and rebuilds.
- **🤖 AI fix** — point the AI at a mod that won't compile and let it repair the build errors.
- **Grounded in the real game** — every request is fed the relevant slice of the game's actual API, so the AI
  uses real classes/methods/IDs instead of guessing.
- **Four backends, switchable from the panel:**
  - **Claude Code** — uses your existing Claude subscription via the installed CLI. **No API key needed.**
  - **Anthropic (Claude)** — your own API key.
  - **OpenAI** — your own API key.
  - **Ollama (local)** — free, offline, no key.
- **One-click free local AI setup** — installs Ollama and downloads a coding model for you, and
  **auto-picks the right model size for your hardware** (detects NVIDIA / AMD / Intel GPU VRAM, or RAM).
- Bring-your-own keys are stored only in your local config; nothing about your account ships with the app.

## 5. Game API browser — know the whole game
- Reads your game's actual code and indexes **every public class, method, field, and enum** (incl. item & block IDs).
- **Searchable browser** — look up real game systems (inventory, health, blocks, biomes…) while you write a mod by hand.
- The same index **grounds the AI** so it references things that really exist.
- Cached for instant loads; one-click re-index after a game update.

## 6. The toolkit (BlockStoryCore) — what mods can do
A documented API so mods (and the AI) get the common things as one-liners, and can reach *anything* for deep
total-conversion / overhaul mods:
- **In-game UI** — a themed Mods page + main-menu button, rebindable keybinds shown in the game's Controls screen.
- **BSPlayer** — read/set stats & vitals, heal, god mode, teleport.
- **BSItems** — list items, create/give items.
- **BSWorld** — read/set blocks at coordinates, time of day, weather, world mode.
- **BSEvents** — hook world load/unload, per-frame tick, and block place/break.
- **BSReflect** — reach any game system by name (the backbone for overhaul mods).
- **BSPatch** — easy Harmony patching to hook or replace any game method.
- Full SDK reference included (`docs/TOOLBOX-SDK.md`).

> Note: this beta ships the **tool only** — no mods are bundled. You make your own (by hand or with the AI).

## 7. Platform & extras
- **Windows + Linux/Proton + Steam Deck.**
- **GUI app** and a **command-line tool** (`modkit`) for terminal/scripting users.
- Custom app icon; single-file, self-contained builds (no separate runtime to install to run the app).

---

## Requirements
- **Block Story** (Steam).
- To **play** with mods: nothing extra (the app installs the framework). Linux needs one Steam launch option.
- To **build** mods: the **.NET 8 SDK** (the app auto-detects it and tells you if it's missing — not needed just to play).
- To use the **AI Builder**: one backend — a Claude subscription (via Claude Code), an Anthropic/OpenAI API key, or local Ollama.

## Known limitations (beta)
- Mods load at game **startup** only — installing a mod takes effect on the next restart (no hot-reload).
- AI-generated mods always **compile** before installing, but you should still review what they do; quality
  depends on the backend (cloud models are stronger than small local ones).
- GPU VRAM auto-detection covers NVIDIA, AMD, and Intel on Linux; unknown setups fall back to a RAM-based pick.
- A pre-existing build workspace may need a one-time "Set up workspace" after a framework update to see new toolkit APIs.
- AI backends other than local Ollama use **your own** account/keys and may incur their normal costs.
