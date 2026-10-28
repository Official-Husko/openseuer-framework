# Dev tools

Everything for working on mods without restarting the game every few minutes: the `oben` command line tool, the DevBridge mod it talks to, scripts, and the MCP server for editors.

## oben

Build it with the solution (`dotnet build ObenseuerExtended.slnx -c Release`) and run it as `dotnet tools/ObenTool/bin/Release/net10.0/oben.dll <command>`, or straight from `tools/ObenTool/bin/Release/net10.0/oben`. `oben help` lists the commands.

| Command | What it does |
| --- | --- |
| `oben install` | Hooks the loader into the game. Works with the game open; it starts with the next launch. |
| `oben uninstall [--remove-mods]` | Puts Unity's startup files back and removes the loader. |
| `oben status` | Where the game is, whether the loader is installed, how the game will be launched, which mods are there, whether the game and the bridge are up. |
| `oben launch [--wait 150] [--fullscreen] [--instance 2]` | Starts the game windowed. `--wait` waits until the bridge answers. |
| `oben stop [--force]` | Asks the game to quit over the bridge, kills it if that doesn't work. |
| `oben logs [--player \| --runner \| --errors] [--lines N]` | The loader log, Unity's Player.log, umu/Proton's output, or this session's error report (`errors.log`). |
| `oben changelog [--write] [--notes X.Y.Z]` | Builds the player changelog from commit trailers (see [changelog.md](changelog.md)). |
| `oben save list \| info \| export \| import \| convert` | Saves on disk: list them, inspect a slot, convert between the game's format and ObenSave's (see [save-format.md](save-format.md)). |
| `oben call <method> [key=value ...]` | Runs a bridge command, e.g. `oben call game.info`. |
| `oben eval "<code>"` | Runs C# in the game (see Scripts). |
| `oben run file.cs [--watch]` | Runs a script file, again on every save with `--watch`. |
| `oben check [--installed]` | Checks every Harmony patch and AccessTools lookup in the built mods against the installed game; run it after a game update (see [testing.md](testing.md)). |
| `oben mcp` | MCP server on stdio. |

### Finding the game

`--game DIR`, then `OBENSEUER_DIR`, then `<ObenGameDir>` in the repo's `build/Local.props`, then Heroic's sideloaded games, then every Steam library.

### Launching on Linux

The tool starts the game the way Heroic does: through umu-launcher with the Proton build and Wine prefix set for the game in Heroic. It uses Heroic's own copy of umu-run (`~/.config/heroic/tools/runtimes/umu/umu-run`). Override any part with `OBEN_UMU`, `OBEN_PROTON` (a Proton folder) or `OBEN_PREFIX`. umu's output goes to `ObenLoader/logs/runner.log`. On Windows the exe is started directly.

`--instance 2` starts a second copy for testing multiplayer on one machine (run `oben install --multi-instance` once first; the game allows only one copy otherwise). It gets `OBEN_INSTANCE=2`, its own log files (`latest-2.log`, `player-2.log`) and the bridge one port up.

## DevBridge

A mod that listens on `127.0.0.1:27860` (27861 for a second instance) and takes one JSON object per line:

```
{"id": 1, "method": "game.info", "params": {}}
{"id":1,"ok":true,"result":{"scene":"Main Menu", ...}}
```

Commands run on Unity's main thread. Errors come back as `"ok": false` with a message, and a stack trace when the error came from deeper code. Settings are in `Mods/DevBridge/config.json` (address, port, timeout). There's no authentication, so leave it on localhost.

| Command | Parameters | Result |
| --- | --- | --- |
| `ping`, `help` | | `"pong"`, every command with its parameters |
| `game.info` | | scene, whether a world is loaded, versions, fps, mods |
| `game.quit`, `game.timescale` | `value` | |
| `scenes.loaded` | | loaded scenes, active first |
| `log.tail` | `after?`, `max?`, `level?` | recent lines from mods, the loader and Unity; pass the returned `last` as `after` next time |
| `console.exec` | `command` | runs a line in the F1 console and returns what it printed |
| `console.mods` | | console commands added by mods |
| `mods.list`, `mods.reload`, `mods.load`, `mods.unload`, `mods.rescan`, `mods.enable` | `id` | mod management |
| `patches.check` | `all?` | every patched method's jump kind, and the ones whose jump points at memory that's gone (see [../research/harmony-jumps.md](../research/harmony-jumps.md)) |
| `obj.find` | `name?`, `component?`, `includeInactive?`, `max?` | GameObjects by name or component type |
| `obj.tree`, `obj.inspect`, `obj.get`, `obj.set`, `obj.call`, `obj.active`, `obj.destroy`, `obj.move` | `target` (`#instanceId` or `Root/Child`), `component`, `member`, ... | poke at live objects |
| `type.get`, `type.set`, `type.call` | `type`, `member`/`method`, `value`/`args` | static members, e.g. `type=GameController member=instance` |
| `screenshot` | `scale?`, `format?`, `quality?` | base64 image |
| `features.list`, `features.set` | `mod`, `id`, `enabled` | switchable features of every mod |
| `player.get`, `player.stats`, `player.stat`, `player.teleport`, `player.heal`, `player.godmode` | see [framework/player.md](framework/player.md) | the player |
| `items.find`, `items.give`, `items.count`, `items.spawn`, `money.get`, `money.add` | see [framework/items.md](framework/items.md) | items and money |
| `clock.get`, `clock.set`, `weather.get`, `weather.list`, `weather.set` | see [framework/clock.md](framework/clock.md) | time and weather |
| `lua.get`, `lua.set`, `lua.run`, `quests.list`, `quests.set` | see [framework/dialogue.md](framework/dialogue.md), [framework/quests.md](framework/quests.md) | dialogue variables and the quest log |
| `doors.list`, `doors.set`, `interact.target`, `furniture.list` | see [framework/world.md](framework/world.md) | doors, what's under the crosshair, placed furniture |
| `npcs.list`, `npcs.get`, `npcs.nearest`, `phone.list` | see [framework/npcs.md](framework/npcs.md), [framework/phone.md](framework/phone.md) | NPCs and phone numbers |
| `player.look`, `player.drain` | see [framework/player.md](framework/player.md) | the player's look, active drain multipliers |
| `script.run`, `script.reset`, `script.list` | | used by `oben run` / `oben eval` |

Other mods can add commands: `ModRegistry.Get<DevBridgeMod>().Commands.Add(this, "my.command", "What it does", new[] { "param: meaning" }, p => ...)`. They're removed when that mod unloads.

## Scripts

The Script Runner mod runs C# that the tool compiles against the game's own assemblies, so you can try something in the running game without making a mod or restarting.

```sh
oben eval "ItemDatabase.database.Count"
oben eval 'var p = PlayerLocator.instance; return p != null ? p.transform.position : (object)"no player";'
oben run scripts/examples/console-greeting.cs --watch
oben run --reset console-greeting
```

- A snippet is either one expression (its value is returned) or statements that run as the body of `Run(ScriptScope Scope)`. Common usings are added: `UnityEngine`, `HarmonyLib`, `ObenLoader`, `ObenFramework` and its namespaces, `ScriptRunner`.
- A whole file needs a static `Run()` or `Run(ScriptScope scope)` method somewhere.
- Everything a script logs while it runs comes back with the result.
- Each script runs in a named scope (`--name`, or the file name). `scope.Harmony` patches, `scope.Track(...)`, `scope.CreateObject(...)` and `scope.StartCoroutine(...)` are undone when a script with the same name runs again or when it's reset. That makes `--watch` a live-edit loop for Harmony patches.
- Compile errors and stack traces use the script's own line numbers.

## MCP

`oben mcp` is a Model Context Protocol server over stdio, for editors and tools that speak it. Point the client at the built `oben` binary with the argument `mcp`. It offers:

- local tools: `game_status`, `game_launch`, `game_stop`, `game_logs`, `build`
- bridge tools: `game_info`, `log_tail`, `mods_list`, `mods_reload`, `features_list`, `features_set`, `player_get`, `player_teleport`, `player_stat`, `items_find`, `items_give`, `money_add`, `clock_set`, `obj_find`, `obj_inspect`, `obj_get`, `obj_set`, `type_get`, `type_call`, `console_exec`, `screenshot`, plus `bridge_call` and `bridge_help` for everything else
- script tools: `script_run`, `script_reset`

## A typical loop

1. `oben launch --wait 150`
2. Try the idea with `oben eval` or a script under `--watch`.
3. Move it into a mod with `"hotReload": true` and rebuild; the game picks it up by itself.
4. `oben logs` or `oben call log.tail level=Warning` when something looks off.
