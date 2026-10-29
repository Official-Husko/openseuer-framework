ObenseuerExtended
=================

A mod loader and a set of mods for Obenseuer: fixes for bugs in the game, performance fixes, a menu
for your mods, and a framework other mods build on. It doesn't need BepInEx.

Install
-------
1. Close the game.
2. Linux: run install.sh. Windows: run install.bat.
3. Start the game as usual. The mods show up in the MODS menu.

The installer finds the game in your Heroic and Steam libraries. If it can't, pass the game's
folder (the one with Obenseuer.exe in it):
    ./install.sh --game "/path/to/Obenseuer"
    install.bat --game "D:\Games\Obenseuer"
You can also set the OBENSEUER_DIR environment variable to that folder.

It copies the mods into the game's Mods folder and adds the loader to the game's Unity files. It
only changes a few small startup files, and keeps copies of the originals.

Updating
--------
Unpack the new version and run the installer again. It replaces the mods and keeps your settings.

Mods
----
ObenFramework      what the other mods build on (required)
ObenUIKit          the menus and windows of the mods (required)
ObenPatches        fixes for bugs in the game; every fix has its own switch
PerformanceFixes   cuts work the game does needlessly every frame; every fix has its own switch
ObenSave           stores saves in a small compressed file (off until you switch it on)
GameTweaks         optional changes to needs, stacks, crafting, renovations, day length, a third-person camera
CheatMenu          cheats on Insert, a developer menu on F8
ObenCoop           co-op, still being built: lobbies and chat work, the shared world doesn't yet

Each mod's page in the MODS menu has its switches and settings. To remove a single mod, delete its
folder from the game's Mods folder.

Is it working?
--------------
The window stays black for a while at start: the game starts up under the hood, then the loader
loads the mods. While it does, a small ObenseuerExtended panel over the window says how far it
got; it can be turned off with "LoadingPanel": false in ObenLoader/settings.json. The window
title says "Obenseuer - ObenseuerExtended" and the version while the mods are installed. On the title screen, the bottom-left corner
says how many mods are loaded and whether they fit your game version. If something doesn't fit, the
MODS menu's Errors page says what.

Uninstall
---------
Run uninstall.sh or uninstall.bat. It removes the loader and puts Unity's startup files back. The
Mods folder and your settings stay; add --remove-mods to delete them too. Steam's "Verify
integrity of game files" restores the game as well.

If something goes wrong
-----------------------
The loader writes a log to the ObenLoader folder inside the game folder (logs/latest.log, and
logs/errors.log when something failed). Send those with a bug report. Each mod also has its own
file in logs/mods, if its author only asks for that one.

To rule out a broken game install, check the game's files against the lists in game-files/hashes
(the right list is picked from the game's version; it takes a minute or two):
    ./game-files/verify-game.sh "/path/to/Obenseuer"
    game-files\verify-game.bat "D:\Games\Obenseuer"
It lists every file that is changed or missing. The files ObenseuerExtended adds or changes on
purpose don't count. If something is listed, verify the game in your store or reinstall it.

What's in here
--------------
Mods/            the mods, one folder each
tool/            the installer and command-line tool (oben) for Linux and Windows
game-files/      checks the game's files against known-good hash lists (verify-game.*)
LICENSE          the license (GPL-3.0)
install.*        installs everything
uninstall.*      takes it out again

