# TODO

What's planned, roughly in order, with the notes each piece builds on. Tick things off as they land.

## Foundation

- [x] ObenLoader: native loading through Unity's startup files, dependency order, hot reload
- [x] `oben` tool: install, launch through umu/Heroic, logs, bridge calls, MCP server
- [x] DevBridge and Script Runner (`oben eval`, `oben run --watch`)
- [x] ObenFramework: events (MainMenu, WorldReady, save/load), WorldState, F1 console commands
- [ ] Error report file (errors.log) for bug reports, like NeetExtended's
- [ ] Changelog from commit history (`oben changelog`), feeding an in-game What's New popup
- [ ] Release packaging (zip with installer scripts, no DevBridge or Script Runner)
- [ ] A THIRD-PARTY-NOTICES file once the first code from the MIT reference projects is ported

## ObenFramework (core functionality)

Each module stays small and gets ported or written when the first mod needs it. The MIT framework in `example-framework/` covers most of these already; `docs/research/framework-reference.md` maps what's worth taking.

- [ ] Player: find the player, stats (`PlayerStats`), safe teleport, damage and healing (`docs/game/player.md`)
- [ ] Items: give, take, spawn, register new items and recipes at runtime (`docs/game/items-crafting.md`)
- [ ] Money: wallet in both currencies through `Money.AddMoney/RemoveMoney` (`docs/game/npcs-dialogue-quests.md`)
- [ ] Saves: per-mod data in side files next to the save, written at `Saved`, read at `Loaded` (`docs/game/save-system.md`)
- [ ] Input: mod hotkeys on free keys, input lock while a mod window is open (`docs/game/ui-input-console.md`)
- [ ] Notifications: the game's own toasts (`Notifications.instance.CreateNotification`)
- [ ] Settings: typed settings per mod with an in-game page
- [ ] Clock and weather: time of day, day changes, sleeping (`docs/game/core-systems.md`)
- [ ] Dialogue and Lua: read and set variables, register functions, start conversations
- [ ] Quests: observe and drive `TaskController`
- [ ] World: doors, interactables, furniture, power (`docs/game/world-building.md`)

## ObenUIKit

UI that looks like the game's own: built from the game's uGUI elements, TMP fonts (Erika Ormig SDF) and materials, with keyboard and controller navigation that cooperates with the game's `*NavigationHandler` classes. The reference framework's `src/Ui` and mod menu are the starting point.

- [ ] Theme taken from the game's pause menu at runtime
- [ ] Window, tabs, buttons, toggles, sliders, text fields, lists
- [ ] Mod menu: MODS in the main and pause menus, mod list with on/off switches, per-mod settings, errors, What's New

## ObenSave

A save format replacement as its own mod: open, documented JSON, compressed, with room for mod data, and able to read the game's own `.tnmt` saves so nothing is lost by switching. The game's saves are already plain minified JSON (`Saves/<character>/<slot>/*.tnmt`), so the gains are size, a stable documented layout and a proper place for mod data.

- [ ] Format spec in `docs/modding/save-format.md`
- [ ] Hook the `.tnmt` read and write paths (`SaveController` write bytes path, `Deserialize`)
- [ ] Import and export between the two formats with `oben save`

## Co-op (ObenTogether or ObenCoop)

Port of the reference co-op mod in `example-mp/`, following `docs/research/coop-reference.md`. Steam lobbies don't work on every install, so networking sits behind a transport interface with direct IP first.

- [ ] Transport interface, direct IP transport, channels, version handshake, persistent player id
- [ ] Session: host, join, leave, roster
- [ ] Other players' bodies: puppets, pose, sitting, appearance
- [ ] Chat
- [ ] Shared clock and weather, sleep vote
- [ ] Join ticket (send the host's world to a joining player)
- [ ] Shared world: doors, items, containers, building, stations, shops, quests
- [ ] Steam transport as a second option
- [ ] Free roam (players in different levels), last
- [ ] Second game instance on one machine for testing (`single-instance=` in boot.config)

## Cheat Menu

- [ ] Its own window (ObenUIKit): god mode, stats, money, items, time and weather, noclip, teleport. Much of it can call the game's own console routines.

## Performance Fixes

From `docs/research/performance.md`, each one a switch of its own:

- [ ] Throttle `Inventory.DeltaSeconds` while the inventory is open
- [ ] Cache `InteractObjects.GetInteractable` lookups per GameObject
- [ ] Batch `TimeOfDayAzure.SecondsPassed` subscribers
- [ ] Drop the LINQ in `LightsController.FixedUpdate` / `LightCullZone`
- [ ] Graphics knobs the options menu doesn't offer (shadow distance and cascades, LOD bias, volumetrics), and apply the SSAO option that's saved but never used

## Game bug fixes (ideas)

Found while reading the code; each needs checking in game first.

- [ ] `RefreshCubemapInTrigger` compares the tag against `"Player)"`
- [ ] `NPC_FOV.CheckIfNPCsAreDetected` calls `GetComponent` on the wrong object
- [ ] First run logs a `DirectoryNotFoundException` because `Saves/` doesn't exist yet
- [ ] `TimeAndDay`'s constructor ignores its hours and minutes
- [ ] `WeatherManager`'s rain and wind getters write to the log on every call
- [ ] The F1 console clears `Achievements.globalDisable` when it closes

## Expansion mods

Ideas welcome; each is its own mod on top of ObenFramework.
