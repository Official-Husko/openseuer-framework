## The window stays black at start

That's normal for a while. The game starts up first, then the loader loads the mods, and Unity draws nothing until the main menu is ready. A small ObenseuerExtended panel over the window says how far loading got, and the window title shows it too. On slower PCs it can take half a minute.

If it stays black for minutes, close the game and look at `ObenLoader/logs/latest.log` in the game folder; the last lines say where it stopped. Then [make a report](Reporting-Bugs) with `oben report`.

## There's no MODS button

- Run the installer again; it's safe to run twice.
- Did Steam verify or update the game? That puts back Unity's startup files and switches the loader off. Install again.
- Check that the game folder has `Mods/ObenFramework` and `Mods/ObenUIKit`. The other mods need both.

## After a game update

When Obenseuer updates, some mods may not fit the new version. The title screen and the MODS menu's Errors page say which. Most keep working, since the game's code rarely changes where they hook in. If one breaks, switch it off on the Mods page, and watch [Releases](https://github.com/Official-Husko/openseuer-framework/releases) for a version made for the update.

## A mod or a fix gets in the way

Switch it off on the **Mods** page of the MODS menu. Every fix in Unofficial ObenPatches and Performance Fixes and every tweak in Game Tweaks has its own switch, so you don't have to give up the whole mod.

## Is it the mods or the game?

Uninstall (`uninstall.bat` or `uninstall.sh`), which leaves your mods and settings in place, and try again with the plain game. Install again afterwards and everything is back as it was. If the problem is still there without mods, it's the game's; see [Bugs in the game itself](Reporting-Bugs#bugs-in-the-game-itself).

## The game crashed

Unity keeps a record of every crash. The next [report](Reporting-Bugs) you make includes the last one from the past week, so make a report and send it with a few words on what you were doing.

## My game files might be broken

Run `game-files/verify-game` from the zip, see [Installing](Installing#is-my-game-install-all-right).

## Linux and Proton

The mods run under Proton the same as on Windows. The installer finds games in Steam and in Heroic. If you start the game some other way, pass the game folder with `--game`.

The F1 console, logs and reports all work the same way. The report says which Wine version and which Linux you're on, which helps a lot with Proton-only bugs.
