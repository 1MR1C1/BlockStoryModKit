# Block Story Mod Kit

A launcher and modding toolkit for Block Story, for Windows and Linux. Play the game with mods, install mods other people share, and build your own — without setting up a dev environment yourself. Built on .NET 8 + Avalonia.

It works alongside BlockStoryCore (the in-game mod framework). If you just want to install mods and play, grab a release below and point it at your game.

## Download

Go to the [Releases](../../releases) page and grab the build for your OS:

- Windows: `ModKit.App.exe`
- Linux: `ModKit.App`

They're self-contained — the .NET runtime is bundled in, so there's nothing else to install. Just run it.

## What it does

- **Launcher** — finds your Block Story install and lists the mods you have. Turn each one on or off (Core stays on), install a mod someone shared (a single `.dll`, a `.zip` pack, or a folder of DLLs), uninstall, save mod **profiles** you can re-apply in a click, filter the list, and launch the game through Steam. It checks GitHub for newer mod + launcher versions on its own, and the **Backup / Load** buttons save and restore your whole mod set + profiles.
- **Mod Builder** — sets up a self-contained mod workspace and scaffolds a new mod from a starter template (a UI panel, a bare plugin, a keybind action, a Harmony patch, a block watcher, a reskinned **wild creature**, or a rideable **pet/mount**), then builds and installs it. It lists the mods in your workspace so you can edit, rebuild, open the folder, or delete each one — the whole edit → build → test loop from the UI.
- **Game API** — indexes your game's actual code so you can search every public class, method, field, and enum (item and block IDs included) while you write a mod.
- **Setup & Help** — one-click framework install, the built-in guides, a bug-report bundler, and your game/mods/.NET status at a glance.

The whole UI is a fantasy-game skin you can rearrange: hit **Edit Layout** and drag or resize any block where you want it — it snaps to a grid and remembers your arrangement.

The Core API your mods build against is documented in `docs/TOOLBOX-SDK.md`.

## Build from source

You'll need the .NET 8 SDK.

```
dotnet run --project src/ModKit.App
```

To build every distributable (GUI + CLI, both platforms) at once:

```
./publish.sh
```

Outputs land in `publish/{linux,windows}/` and `publish/cli-{linux,windows}/`.

## Layout

```
BlockStoryModKit.sln
src/
  ModKit.Core/     framework logic, no UI: game detection, mod manager, scaffolder, build runner
  ModKit.App/      the Avalonia GUI (Launcher / Builder / Settings / Setup / Game API)
  ModKit.Cli/      a terminal front-end over the same Core
docs/
  TOOLBOX-SDK.md   how to build a mod against the Core toolbox
```

`ModKit.Core` has no UI dependency, so the GUI (and the CLI) are just shells over it.

## Requirements

- A copy of Block Story (this tool doesn't include the game).
- .NET 8 SDK if you're building from source or building your own mods.
- The mod framework installed — the launcher sets that up for you in one click.

## License

All rights reserved — see [LICENSE](LICENSE). You're welcome to download and use the app, but please don't redistribute, modify, sell, or rebrand it without permission.
