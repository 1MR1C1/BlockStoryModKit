# Block Story Mod Kit

A launcher and modding toolkit for Block Story, for Windows and Linux. Play the game with mods, install mods other people share, and build your own — without setting up a dev environment yourself. Built on .NET 8 + Avalonia.

It works alongside BlockStoryCore (the in-game mod framework). If you just want to install mods and play, grab a release below and point it at your game.

## Download

Go to the [Releases](../../releases) page and grab the build for your OS:

- Windows: `BlockStoryModKit-Windows.exe`
- Linux: `BlockStoryModKit-Linux`

They're self-contained — the .NET runtime is bundled in, so there's nothing else to install. Just run it.

## What it does

- **Launcher** — finds your Block Story install and lists the mods you have. Turn each one on or off (Core stays on), install a mod someone shared (a single `.dll`, a `.zip` pack, or a folder of DLLs), uninstall, save mod **profiles** you can re-apply in a click, filter the list, and launch the game through Steam. It checks GitHub for newer mod + launcher versions on its own, and the **Backup / Load** buttons save and restore your whole mod set + profiles.
- **Mod Builder** — sets up a self-contained mod workspace and scaffolds a new mod from a starter template (a UI panel, a bare plugin, a keybind action, a Harmony patch, a block watcher, a reskinned **wild creature**, or a rideable **pet/mount**), then builds and installs it. It lists the mods in your workspace so you can edit, rebuild, open the folder, or delete each one — the whole edit → build → test loop from the UI.
- **Game API** — indexes your game's actual code so you can search every public class, method, field, and enum (item and block IDs included) right inside the app while you write a mod.
- **Setup & Help** — one-click framework install, the built-in guides, a bug-report bundler, and your game/mods/.NET status at a glance.

The app comes with several looks — pick one from your profile: **Fantasy** (the ornate default), **Slate** (clean flat dark), **Charcoal** (minimal dark neutral), or **Neon** (an animated synthwave skin). Your choice is remembered between runs.

## Requirements

- A copy of Block Story (this tool doesn't include the game).
- The mod framework installed — the launcher sets that up for you in one click.
- The .NET 8 SDK is only needed if you want to **build your own** mods; the launcher itself needs nothing.

## License

All rights reserved — see [LICENSE](LICENSE). You're welcome to download and use the app, but please don't redistribute, modify, sell, or rebrand it without permission.

---

Block Story is a trademark of its respective owner. This project is an independent, unofficial tool and is not affiliated with or endorsed by the makers of Block Story.
