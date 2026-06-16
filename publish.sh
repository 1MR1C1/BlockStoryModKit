#!/usr/bin/env bash
# Build + publish all four distributables in one go:
#   publish/linux/ModKit.App          (GUI, Linux)
#   publish/windows/ModKit.App.exe    (GUI, Windows)
#   publish/cli-linux/modkit          (CLI, Linux)
#   publish/cli-windows/modkit.exe    (CLI, Windows)
# Also refreshes the embedded base bundle from the mod project's latest dist zip, so the launcher's
# "Install framework" ships the current BepInEx + Core.  Run:  ./publish.sh
set -e
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

# 1) refresh the embedded base bundle from the mod project's newest dist zip (if it's on this machine)
BASE_SRC="$(ls -t /home/mrc/blockstory_mod/dist/BlockStoryMods-Base_*.zip 2>/dev/null | head -1)"
if [ -n "$BASE_SRC" ]; then
  cp -f "$BASE_SRC" src/ModKit.App/Assets/base.zip
  echo "embedded base <- $(basename "$BASE_SRC")"
else
  echo "embedded base: keeping existing src/ModKit.App/Assets/base.zip (no dist zip found)"
fi

APP=src/ModKit.App/ModKit.App.csproj
CLI=src/ModKit.Cli/ModKit.Cli.csproj
pub() { "$DOTNET" publish "$1" -c Release -r "$2" --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o "$3" >/dev/null 2>&1; rm -f "$3"/*.pdb; echo "  -> $3"; }

echo "publishing (self-contained, single-file)…"
pub "$APP" linux-x64 publish/linux
pub "$APP" win-x64   publish/windows
pub "$CLI" linux-x64 publish/cli-linux
pub "$CLI" win-x64   publish/cli-windows

echo "done:"
for f in publish/linux/ModKit.App publish/windows/ModKit.App.exe publish/cli-linux/modkit publish/cli-windows/modkit.exe; do
  [ -e "$f" ] && printf "  %6s  %s\n" "$(du -h "$f" | cut -f1)" "$f"
done
