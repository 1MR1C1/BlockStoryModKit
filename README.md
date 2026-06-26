# Block Story Mod Kit

A launcher and modding toolkit for Block Story, for Windows and Linux. Use it to play the game with mods, install mods other people share, and build your own mods without needing to set up a dev environment yourself. Built on .NET 8 + Avalonia.

It works alongside BlockStoryCore (the in-game mod framework). If you just want to install and play with mods, grab a release below and point it at your game.

## Download

Head to the [Releases](../../releases) page and download the build for your OS:

- Windows: `ModKit.App.exe`
- Linux: `ModKit.App`

They're self-contained (the .NET runtime is bundled in), so there's nothing else to install. Just run it.

## What it does

- **Launcher** — finds your Block Story install, shows the mods you have installed, lets you turn each on or off (Core stays on), install a mod someone shared (a single `.dll`, a `.zip` pack, or a folder of DLLs), uninstall mods, and launch the game through Steam.
- **Easy Mod (no code)** — pick what it should do (info panel, message popup, or a heal key), pick a key, give it a name, and it generates, builds and installs a complete working mod for you. No C# needed.
- **Mod Builder** — sets up a self-contained mod workspace, scaffolds a new mod from a starter template (a UI panel, a bare plugin, a keybind-action, a Harmony patch, a block watcher, a reskinned **wild creature**, or a rideable **pet/mount**), builds it, and installs it into the game. It lists the mods in your workspace so you can edit, rebuild, open the folder, or delete each one — the whole edit → build → test loop from the UI.
- **Toolbox docs** — `docs/TOOLBOX-SDK.md` covers the Core API your mods build against.

## Build from source

You'll need the .NET 8 SDK.

```
dotnet run --project src/ModKit.App
```

To build all the distributables (GUI + CLI, both platforms) at once:

```
./publish.sh
```

Outputs land in `publish/{linux,windows}/` and `publish/cli-{linux,windows}/`.

## Layout

```
BlockStoryModKit.sln
src/
  ModKit.Core/     framework logic, no UI: game detection, mod manager, scaffolder, build runner
  ModKit.App/      the Avalonia GUI (Launcher / Mod Builder / Settings / Help)
  ModKit.Cli/      a terminal front-end over the same Core
docs/
  TOOLBOX-SDK.md   how to build a mod against the Core toolbox
```

`ModKit.Core` has no UI dependency, so the GUI is just a shell over it.

## Requirements

- A copy of Block Story (this tool does not include the game).
- .NET 8 SDK if you're building from source or building your own mods.
- The mod framework installed (the launcher sets this up for you in one click).

## License

All rights reserved — see [LICENSE](LICENSE). You're welcome to download and use the app, but please don't redistribute, modify, sell, or rebrand it without permission.
