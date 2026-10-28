# TODO

What's planned, roughly in order, with the notes each piece builds on. Tick things off as they land.

## Foundation

- [x] ObenLoader: native loading through Unity's startup files, dependency order, hot reload
- [x] `oben` tool: install, launch through umu/Heroic, logs, bridge calls, MCP server
- [x] DevBridge and Script Runner (`oben eval`, `oben run --watch`)
- [x] ObenFramework: events (MainMenu, WorldReady, save/load), WorldState, F1 console commands, switchable features
- [x] Error report file (errors.log) for bug reports
- [x] Changelog from commit history (`oben changelog`)
- [ ] In-game What's New popup reading changelog.json
- [ ] Release packaging (zip with installer scripts, no DevBridge or Script Runner)
- [x] GPL-3.0 license
- [ ] A THIRD-PARTY-NOTICES file once the first code from the MIT reference projects is ported

## ObenFramework (core functionality)

Each module stays small and gets ported or written when the first mod needs it. The MIT framework in `example-framework/` covers most of these already; `docs/research/framework-reference.md` maps what's worth taking.

- [x] Player: find the player, stats, safe teleport, damage and healing, died/damaged events
- [x] Items: give, take, spawn, register new items and recipes at runtime
- [x] Money: wallet in both currencies
- [x] Saves: per-mod data in side files next to the save (needs an in-game test with real saves)
- [ ] Input: mod hotkeys on free keys, input lock while a mod window is open (`docs/game/ui-input-console.md`)
- [ ] Notifications: the game's own toasts (`Notifications.instance.CreateNotification`)
- [ ] Settings: typed settings per mod with an in-game page
- [x] Clock and weather: time of day, minute/hour/day events, weather
- [ ] Dialogue and Lua: read and set variables, register functions, start conversations
- [ ] Quests: observe and drive `TaskController`
- [ ] World: doors, interactables, furniture, power (`docs/game/world-building.md`)

## ObenUIKit

UI that looks like the game's own: built from the game's uGUI elements, TMP fonts (Erika Ormig SDF) and materials, with keyboard and controller navigation that cooperates with the game's `*NavigationHandler` classes. The reference framework's `src/Ui` and mod menu are the starting point.

- [x] Theme taken from the main menu and the pause menu (stamped, not cloned)
- [x] Window, headers, labels, buttons, cycles, toggles, sliders
- [ ] Keyboard/controller navigation, text fields, key binds, long lists, tabs, tooltips, confirm dialogs
- [x] Mod menu: MODS in the main and pause menus, Mods page with mod and feature switches
- [ ] Settings, Errors and What's New pages

## ObenSave

A save format replacement as its own mod: open, documented JSON, compressed, with room for mod data, and able to read the game's own `.tnmt` saves so nothing is lost by switching. The game's saves are already plain minified JSON (`Saves/<character>/<slot>/*.tnmt`), so the gains are size, a stable documented layout and a proper place for mod data.

- [x] Format spec in `docs/modding/save-format.md`
- [x] Read and write hooks, off by default until tested with real saves
- [x] Import, export and convert with `oben save`

## ObenCoop

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

From `docs/research/performance.md`, each one a switch of its own (`mods/PerformanceFixes`, write-ups and test plans in `docs/modding/performance-fixes.md`). Built; each still needs its on/off check in game:

- [x] Throttle `Inventory.DeltaSeconds` while the inventory is open
- [x] Crosshair interactable lookup without reflection garbage
- [ ] Batch `TimeOfDayAzure.SecondsPassed` subscribers
- [x] Light zone checks without LINQ
- [x] Reused inventory slot array, cached outside ambient colour, idle stat effects, timers without copies
- [ ] Graphics knobs the options menu doesn't offer (shadow distance and cascades, LOD bias, volumetrics), and apply the SSAO option that's saved but never used

## Unofficial ObenPatches

Fixes for the game's own bugs, each a feature the player can switch off. Write-ups in `docs/game/known-bugs.md`.

- [x] MissingSavesFolder: error on the main menu before the first save
- [x] RobbersSeePolice: NPC sight checks read the NPC's own head
- [x] ReflectionProbeRefresh (off by default): `"Player)"` tag typo; needs a look in a level before it's turned on
- [ ] More from playing: check `known-bugs.md`'s "not fixed" list and Player.log errors during real play

## Expansion mods

Ideas welcome; each is its own mod on top of ObenFramework.
