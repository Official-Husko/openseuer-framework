<div align="center">

# ObenseuerExtended

**Bug fixes, performance fixes and mods for [Obenseuer](https://store.steampowered.com/app/951240/Obenseuer/), with a mod loader that needs no BepInEx.**

Run the installer, start the game, and everything shows up under MODS in the main menu.

[![Latest release](https://img.shields.io/github/v/release/Official-Husko/openseuer-framework?sort=semver&label=release&style=flat-square)](https://github.com/Official-Husko/openseuer-framework/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Official-Husko/openseuer-framework/total?style=flat-square&color=blue)](https://github.com/Official-Husko/openseuer-framework/releases)
[![Obenseuer 0.5.13](https://img.shields.io/badge/Obenseuer-0.5.13-6b8e23?style=flat-square)](https://store.steampowered.com/app/951240/Obenseuer/)
[![Windows and Linux](https://img.shields.io/badge/runs%20on-Windows%20%7C%20Linux%20%28Proton%29-555?style=flat-square)](#installing)
[![License](https://img.shields.io/github/license/Official-Husko/openseuer-framework?style=flat-square)](LICENSE)
[![Stars](https://img.shields.io/github/stars/Official-Husko/openseuer-framework?style=flat-square&color=yellow)](https://github.com/Official-Husko/openseuer-framework/stargazers)
[![Last commit](https://img.shields.io/github/last-commit/Official-Husko/openseuer-framework?style=flat-square)](https://github.com/Official-Husko/openseuer-framework/commits/main)

[Download](https://github.com/Official-Husko/openseuer-framework/releases) ·
[Install](#installing) ·
[Features](#features) ·
[Report a bug](https://github.com/Official-Husko/openseuer-framework/issues/new/choose) ·
[Make your own mods](#making-your-own-mods)

</div>

> [!NOTE]
> 1.0.0 is the first stable release. Every change is tested in the game, but not every combination of them, so back up your saves before you install or update, and please open an issue when something breaks. Test builds of what comes next are marked as pre-releases.

## Features

Everything comes as separate mods, and most fixes inside them have their own switch. Turn off whatever you don't want in the MODS menu.

### MODS menu

- A MODS button in the main menu and the pause menu.
- Switch mods and single fixes on and off.
- The settings of every mod on one page, with a search box and a Reset next to anything you changed.
- Change mod hotkeys, to keys or controller buttons. Clashes are pointed out.
- What's New opens once after an update.
- An Errors page that says which mod went wrong.
- A Report page that packs everything a bug report needs into one zip: versions, logs, settings and, if you want, your PC, your save and the last crash. Your user name, Steam id and IP addresses are left out. One more button opens a bug report on GitHub with the versions already filled in.
- Mouse, keyboard and controller all work.

### Unofficial ObenPatches

Fixes for bugs in the game itself, each with its own switch.

- Banks pay interest on savings and charge it on debts every week, like they say they do.
- Renovations take their materials once, not from every resource storage you have.
- Cup dispensers and other refilling item givers refill at their normal pace. After you entered their level they used to refill twice as fast.
- Money given or taken in conversations keeps the decimals of RM amounts.
- The pocket bank conversation knows when you already have an account.
- Emails from August, September and October show the right month.
- Robbers notice the police that can actually see them.
- NPCs on a two-point patrol no longer hang the game.
- Ambient sounds use their whole pitch range and all of their clips.
- Fewer errors in the log: before your first save, and from lamps without a light in Open Sewer Tenement and two Bazaar buildings.
- Optional, off by default: NPCs use their detection chance instead of always spotting you.

### Performance Fixes

Stops the game from repeating work every frame that it doesn't need to. All fixes are on by default and each one can be switched off.

- The inventory screen no longer redraws every slot on every frame.
- Looking at things, lighting zones, area portals, player stats and timers no longer create garbage every frame, so the game stutters less when it cleans up memory.
- Type `perf` in the F1 console to see your frame times.

### Game Tweaks

Optional changes to how the game plays. Nothing changes until you turn it on.

- Slower needs, bigger stacks (up to 20 times), slower spoilage.
- Faster crafting and dishwashers, and recipes without a minimum skill.
- Cheaper renovations, renovations paid with money only, or shorter waits for them.
- Longer or shorter days.
- A third-person camera on F6, with scroll zoom.

### Cheat Menu

Press Insert: god mode, needs, noclip, move speed, teleport spots, money, items, time, weather and your crime record. F8 opens a developer menu for quest states and story flags, useful when a quest gets stuck.

### ObenSave

Off until you turn it on. The game writes its saves as plain text, several megabytes each; ObenSave compresses them. Switching it off again turns them back into the game's own files, exactly as the game wrote them, so you're never stuck with saves only the mod can read.

### ObenCoop (not playable yet)

Co-op is being built. You can already host or join a game over your network or through Steam, find games in a lobby list or with a short join code, chat, and see where the other players are. The shared world is still missing, so playing together doesn't work yet. The Co-op button in the main and pause menu is where it will live.

### Also

- No BepInEx. The loader plugs into the game's own Unity startup files and keeps a copy of the originals.
- While the window is still black at start, a small panel shows how far loading the mods got. The title screen then tells you how many mods loaded and whether they fit your game version.
- The pause menu shows which build you have, for example `ObenseuerExtended v0.1.0 (a185d27)`.
- Every mod writes its own log, and the Report page packs them with everything else a bug report needs.
- `verify-game` checks your game's files against known-good lists, to rule out a broken install.

## Installing

1. Download the newest zip from [Releases](https://github.com/Official-Husko/openseuer-framework/releases).
2. Unpack it anywhere.
3. Close the game.
4. Run `install.bat` on Windows, or `install.sh` on Linux.
5. Start the game as usual. Everything is under MODS.

The installer finds the game in your Steam libraries and in Heroic. If it doesn't, give it the folder that has `Obenseuer.exe` in it:

```sh
install.bat --game "D:\Games\Obenseuer"
./install.sh --game "/path/to/Obenseuer"
```

**Updating:** unpack the new version and run its installer. Your settings stay.

**Uninstalling:** run `uninstall.bat` or `uninstall.sh`. It takes the loader out and puts the game's files back. Your mods and their settings stay unless you add `--remove-mods`. Verifying the game files in Steam removes the loader too.

Made for Obenseuer 0.5.13. After a game update the MODS menu tells you which mods weren't made for the new version. On Linux it runs through Proton, from Steam or Heroic.

## Something broke?

1. Open MODS and go to the Report page. Tick what you want to share and press Make report.
2. Press Report on GitHub. Your browser opens a bug report with the versions filled in; describe what happened and drag the zip in. The zips are in the `ObenLoader/reports` folder of your game.
3. Game won't start at all? Run `oben report` from the folder you installed from (`tool/linux-x64/oben report`, or `tool\win-x64\oben.exe report` on Windows). It makes the same zip, and [the bug form](https://github.com/Official-Husko/openseuer-framework/issues/new/choose) is a click away.

## Making your own mods

ObenseuerExtended is also what its own mods are built on, and you can use it for yours:

- A framework for the game: events, the player, items and recipes, quests and dialogue, NPCs, shops, furniture, the world, per-save data and a lot more.
- A UI kit that looks like the game's own menus, with keyboard and controller support.
- Hot reload: rebuild your mod and it reloads in the running game, in a second or two.
- Dev tools: a command line tool, a bridge to talk to the running game, a script runner, and an MCP server.

Start with [getting started](docs/modding/getting-started.md) and [dev tools](docs/modding/dev-tools.md). Every part of the framework has its own page in [docs/modding/framework](docs/modding/framework/), and [docs/game](docs/game/) has notes on how the game's own code works.

<details>
<summary>Building from source</summary>

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and the game. The build compiles against the game's own assemblies; nothing from the game is in this repo.

The build looks for the game in the default Steam library. If yours is somewhere else, set `OBENSEUER_DIR` or create `build/Local.props`:

```xml
<Project>
  <PropertyGroup>
    <ObenGameDir>/path/to/Obenseuer</ObenGameDir>
  </PropertyGroup>
</Project>
```

Then build. When the game folder exists, every build copies the loader into `Obenseuer_Data/Managed` and the mods into `Mods/`:

```sh
dotnet build -c Release
```

Hook the loader into the game once and start it:

```sh
dotnet tools/ObenTool/bin/Release/net10.0/oben.dll install
dotnet tools/ObenTool/bin/Release/net10.0/oben.dll launch --wait 150
```

On Linux the tool starts the game the same way Heroic does (umu-launcher with the game's Proton build and Wine prefix). [docs/modding/dev-tools.md](docs/modding/dev-tools.md) has the rest, and [docs/modding/releasing.md](docs/modding/releasing.md) covers packages and releases.

</details>

## Thanks

- NomadsReach, for the [Obenseuer Modding Framework](https://github.com/NomadsReach/Obenseuer-Modding-Framework) and the Obenseuer co-op mod. Both are MIT licensed and parts of them live on here; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- [LiteNetLib](https://github.com/RevenantX/LiteNetLib), which the co-op networking runs on.
- Loiste Interactive, for Obenseuer. This project isn't made or endorsed by them.

## License

GPL-3.0, see [LICENSE](LICENSE).

## Star history

<a href="https://star-history.com/#Official-Husko/openseuer-framework&Date">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=official-husko/openseuer-framework&type=Date&theme=dark" />
    <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=official-husko/openseuer-framework&type=Date" />
    <img alt="Star history" src="https://api.star-history.com/svg?repos=official-husko/openseuer-framework&type=Date" />
  </picture>
</a>
