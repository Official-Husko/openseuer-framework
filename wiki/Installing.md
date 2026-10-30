## What you need

- Obenseuer 0.5.13. Other versions may work, but the mods were made for this one; after a game update the MODS menu tells you which of them weren't.
- Windows, or Linux running the game through Proton (Steam or Heroic).
- Nothing else. The installer brings everything, .NET included.

## Installing

1. Download the newest zip from [Releases](https://github.com/Official-Husko/openseuer-framework/releases). Pre-releases end in `_pre-release.zip`.
2. Unpack it anywhere, for example your Downloads folder.
3. Close the game.
4. Run `install.bat` on Windows or `install.sh` on Linux.
5. Start the game the usual way. The mods are under **MODS** in the main menu.

The installer looks for the game in your Steam libraries and in Heroic. If it can't find it, give it the folder that has `Obenseuer.exe` in it:

```sh
install.bat --game "D:\Games\Obenseuer"
./install.sh --game "/path/to/Obenseuer"
```

You can also set the `OBENSEUER_DIR` environment variable to that folder.

### What it changes

It copies the mods into the game's `Mods` folder and the loader into `Obenseuer_Data/Managed`, and adds the loader to two small Unity startup files (`ScriptingAssemblies.json` and `RuntimeInitializeOnLoads.json`). It keeps a copy of each original next to it, ending in `.obenbak`. Nothing else in the game is touched, and your saves stay as the game writes them.

## Is it working?

- At start the window stays black for a while longer than usual. A small ObenseuerExtended panel shows how far loading the mods got.
- The window title says "Obenseuer - ObenseuerExtended".
- The title screen's bottom left says how many mods loaded and whether they fit your game version.
- The bottom of the pause menu shows the version you have, for example `ObenseuerExtended v0.1.0 (a185d27)`. The part in brackets says exactly which build it is; mention it when you report a bug.

## Updating

Unpack the new version and run its installer. It replaces the mods and keeps your settings.

## Uninstalling

Run `uninstall.bat` or `uninstall.sh`. It takes the loader out and puts Unity's startup files back. The `Mods` folder and your mod settings stay, in case you come back; add `--remove-mods` to delete them as well.

Verifying the game files in Steam also puts the startup files back, which switches the mods off.

To remove a single mod, delete its folder from the game's `Mods` folder, or switch it off in the MODS menu.

## Is my game install all right?

The zip has a `game-files` folder that checks your game against lists of the original files:

```sh
./game-files/verify-game.sh "/path/to/Obenseuer"
game-files\verify-game.bat "D:\Games\Obenseuer"
```

It lists every file that is changed or missing, leaving out the ones ObenseuerExtended changes on purpose. If something is listed, verify the game in Steam or reinstall it.
