==========================================================
  Block Story Mod Kit — v1.0 (BETA)
==========================================================

Thanks for testing! This is the TOOL only — no mods are bundled.
You install the framework and then build/add your own.

WHAT'S IN THIS FOLDER
  ModKit.App          <- the app for LINUX (run it)         [chmod +x if needed]
  ModKit.App.exe      <- the app for WINDOWS (double-click)
  FEATURES.txt        <- everything the app does
  (use only the one for your OS)

WINDOWS: "Windows protected your PC" (SmartScreen)?
  This is NORMAL for a beta — the app just isn't code-signed yet (it is
  NOT a virus). To run it:
    * Click "More info" (Докладніше) -> "Run anyway" (Виконати в будь-якому разі).
    * If "Run anyway" doesn't appear: right-click ModKit.App.exe ->
      Properties -> tick "Unblock" (bottom) -> OK, then run it.

QUICK START
  1. Run the app for your OS.
  2. Settings tab  -> set (or Auto-detect) your Block Story folder.
  3. Setup & Help  -> "Install / update framework".  (one time)
  4. LINUX/STEAM DECK ONLY: in Steam, right-click Block Story ->
     Properties -> Launch Options, paste exactly:
         WINEDLLOVERRIDES="winhttp=n,b" %command%
  5. Launch the game once — a "Mods" button appears on the main menu.

MAKE A MOD
  * Mod Builder tab: pick a starter template, name it, "Make my mod".
  * Or use the AI Builder: type what you want in plain English.
      - Free option: Settings -> "Set up free local AI" (installs a local
        model; no API key, runs offline).
      - Or use your Claude subscription / an API key.
  * Game API tab: browse the real game classes/items while you work.

BUILDING MODS NEEDS the .NET 8 SDK (the app checks for it and tells you).
PLAYING with mods does NOT need anything extra.

FOUND A BUG?
  Launcher tab -> "Bug report" makes a zip (log + your mod list). Send it over.

Known beta notes:
  * Mods load at game startup — restart the game to apply changes.
  * AI mods always compile before installing, but review what they do.
==========================================================
