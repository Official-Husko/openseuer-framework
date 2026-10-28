# TODO

What's planned, roughly in order, with the notes each piece builds on. Tick things off as they land.

## Foundation

- [x] ObenLoader: native loading through Unity's startup files, dependency order, hot reload
- [x] `oben` tool: install, launch through umu/Heroic, logs, bridge calls, MCP server
- [x] DevBridge and Script Runner (`oben eval`, `oben run --watch`)
- [x] ObenFramework: events (MainMenu, WorldReady, save/load), WorldState, F1 console commands, switchable features
- [x] Error report file (errors.log) for bug reports
- [x] Changelog from commit history (`oben changelog`)
- [x] In-game What's New page and popup reading changelog.json (needs a release with changelog.json to see it)
- [x] Release packaging (`build/package.sh`, `build/release.sh`; dev mods marked `devOnly`)
- [x] Unit tests (`dotnet test`) and `oben check` for hooks after game updates
- [x] GPL-3.0 license
- [x] THIRD-PARTY-NOTICES.md (NomadsReach MIT code, LiteNetLib)
- [ ] GitHub URL for the player README.txt and the docs, once the repository is up

## ObenFramework (core functionality)

Each module stays small and gets ported or written when the first mod needs it. The MIT framework in `example-framework/` covers most of these already; `docs/research/framework-reference.md` maps what's worth taking.

- [x] Player: find the player, stats, safe teleport, damage and healing, died/damaged events
- [x] Items: give, take, spawn, register new items and recipes at runtime
- [x] Money: wallet in both currencies
- [x] Saves: per-mod data in side files next to the save (needs an in-game test with real saves)
- [x] Input: input lock, rebindable mod hotkeys with a Hotkeys page
- [x] Notifications and HUD: toasts, hints in the game's hint panel, subtitles, bars
- [ ] Settings: typed settings per mod with an in-game page
- [x] Clock and weather: time of day, minute/hour/day events, weather
- [x] Dialogue and Lua: variables, Lua functions, conversations, started/ended events
- [x] Quests: quest log, status changes, change events
- [x] World: doors, interactables with stable addresses, furniture ids and events, lights, switches, power (read-only)
- [x] NPCs: queries, spawn events, control claims, schedules, mod phone numbers
- [x] Player look capture/apply and survival hooks (thresholds, drain, eating and damage interception)
- [ ] Coverage of the MIT framework: see PROGRESS.md

## ObenUIKit

UI that looks like the game's own: built from the game's uGUI elements, TMP fonts (Erika Ormig SDF) and materials, with keyboard and controller navigation that cooperates with the game's `*NavigationHandler` classes. The reference framework's `src/Ui` and mod menu are the starting point.

- [x] Theme taken from the main menu and the pause menu (stamped, not cloned)
- [x] Window, headers, labels, buttons, cycles, toggles, sliders
- [x] Keyboard/controller navigation, text fields, key binds, confirm dialogs, toasts
- [ ] Long lists, tabs, tooltips
- [x] Mod menu: MODS in the main and pause menus, Mods page with mod and feature switches
- [x] Settings, Errors and What's New pages

## ObenSave

A save format replacement as its own mod: open, documented JSON, compressed, with room for mod data, and able to read the game's own `.tnmt` saves so nothing is lost by switching. The game's saves are already plain minified JSON (`Saves/<character>/<slot>/*.tnmt`), so the gains are size, a stable documented layout and a proper place for mod data.

- [x] Format spec in `docs/modding/save-format.md`
- [x] Read and write hooks, off by default until tested with real saves
- [x] Import, export and convert with `oben save`

## ObenCoop

Port of the reference co-op mod in `example-mp/`, following `docs/research/coop-reference.md`. Steam lobbies don't work on every install, so networking sits behind a transport interface with direct IP first.

- [x] Transport interface, direct IP transport, channels, version handshake, persistent player id
- [x] Session: host, join, leave, roster (tested with two copies on one machine)
- [ ] Other players' bodies: puppets, pose, sitting, appearance
- [x] Chat (console command for now)
- [ ] Shared clock and weather, sleep vote
- [ ] Join ticket (send the host's world to a joining player)
- [ ] Shared world: doors, items, containers, building, stations, shops, quests
- [ ] Steam transport as a second option
- [ ] Free roam (players in different levels), last
- [x] Second game instance on one machine (`oben install --multi-instance`, `oben launch --instance 2`)

## Cheat Menu

- [x] Window on Insert and a MODS page: god mode, needs, heal, noclip, move speed, teleport spots, money, items, time, weather, crime (needs its in-game check)

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
- [x] EmailMonthNames, DialogueRealMoney, RandomPickSkipsLast, AmbientSoundVariety, DialogueLuaFunctions, NpcSightChance (off by default)
- [x] BankInterest: weekly interest on savings and debts
- [ ] More from playing: check `known-bugs.md`'s "not fixed" list (subscription dates) and Player.log errors during real play

## Expansion mods

Ideas welcome; each is its own mod on top of ObenFramework.
