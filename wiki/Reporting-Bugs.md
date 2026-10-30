Something broke? The quickest way to tell us is a report from the game. It holds everything we would otherwise have to ask you for, one question at a time.

## From the game

1. Open **MODS** and go to the **Report** page.
2. Tick what you want to share (see below).
3. Press **Make report**. The page says where the zip went and how big it is.
4. Press **Report on GitHub**. Your browser opens a bug report with your versions and the PC parts you ticked already filled in.
5. Say what happened, and drag the zip into the report. The zips are in the `ObenLoader/reports` folder of your game; **Open folder** takes you there.

You need a GitHub account to post the report. If you'd rather not make one, **Copy summary** puts the short version on your clipboard, and you can share the zip any other way.

The F1 console can do the same: `report` makes one, `report all` adds everything, `report folder` opens the folder.

## When the game won't start

Run the tool from the folder you installed from. It makes the same zip without the game:

```sh
./tool/linux-x64/oben report
tool\win-x64\oben.exe report
```

Add `--cpu --gpu --ram` to include your PC, or `--all` for those and your newest save. It prints a link to the bug form with your versions filled in.

## What's in a report

Always:

- the versions: ObenseuerExtended, the game, and checksums of the game's files, so we know exactly which build you have
- your operating system (on Linux also the Wine version and your real system)
- the mods and whether each one loaded, and BepInEx with its plugins if you have it
- the logs of this session and the one before, and each mod's own log
- the mods' settings

If you tick them:

- **Processor**, **Graphics card and screen**, **Memory**: what your PC has
- **The save you're playing**: every file of the save you have loaded, or of your newest one if you're at the main menu. Bugs in a save are much easier to find with it.
- **The last crash** (on unless you untick it): if the game crashed in the last week, the crash dump and the log Unity kept from that moment

## Your privacy

"Hide personal info" is on unless you turn it off. Before anything goes into the zip, the report replaces your user name (the one in folder paths like `C:\Users\you`), Steam ids and IP addresses with placeholders, in every text file.

Two things can't be cleaned because they aren't text: your save, and the crash dump. The report's summary says so when they are in it. Co-op's identity file never goes in.

Nothing is sent anywhere by itself. The zip stays on your PC until you attach it somewhere.

## Bugs in the game itself

Some bugs are the game's own, not ours. [Unofficial ObenPatches](Unofficial-ObenPatches) fixes those it can, so tell us with the "Bug in the game itself" form. Please report them to Loiste Interactive on the [Steam forums](https://steamcommunity.com/app/951240/discussions/) too.
