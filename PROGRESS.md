# Progress

How much of the MIT [Obenseuer Modding Framework](https://github.com/NomadsReach/Obenseuer-Modding-Framework) (ObenseuerCore, by NomadsReach) our own stack covers, module by module, on the way to full feature parity and past it. The module ids and sizes come from the map in [docs/research/framework-reference.md](docs/research/framework-reference.md).

**Keep this file current.** Every change that adds, finishes or drops a feature covered here updates its row, the module's coverage and the totals below in the same commit.

_Last updated: 2026-10-28._

## Totals

| | |
| --- | --- |
| Reference size | 89,607 lines in 31 modules (WebUi's 2,729 lines left out: it's Windows-only and our native Mods menu replaces it) |
| Covered, weighted by module size | **about 61%** |
| Modules done or nearly done (80%+) | 8 of 31 |
| Modules started | 26 of 31 |
| Modules in progress right now | 0 |

Coverage is an estimate per module of how much of what the reference module offers a mod author can do with ours, weighted by the reference module's line count. A small module done counts little; the big ones (NPCs, SceneLook, shops, player) move the number most.

Status: **Done**, **Partial** (some of it, listed), **In progress** (being built now), **Not started**, **Replaced** (ours does the same job differently).

## By module

| Id | Module | Size | Coverage | Status | Ours | Missing |
| --- | --- | ---: | ---: | --- | --- | --- |
| M01 | Bootstrap and per-mod handle | 1,720 | 70% | Partial | ObenLoader: discovery, dependency order, `ObenMod` as the per-mod handle, hot reload | Mod id and file name validation (`ModIds`), supported game build check, isolating one module's init failure from the rest |
| M02 | Patching | 311 | 70% | Partial | Harmony through the loader (`[HarmonyPatch]`, per-mod instance, unpatch on unload), feature categories, `oben check` | Patch by name and argument types from code, a failed core patch disabling only its own area with `IsAvailable` reporting |
| M03 | Logging | 363 | 65% | Partial | `LogHub`, `ModLogger`, latest/previous logs, repeat collapsing for Unity, `errors.log` report | Per-mod line rate limit, change-only `Debug(key, msg)`, log-once helper |
| M04 | Config and tweaks | 1,206 | 30% | Partial | `LoadConfig<T>`, Settings page in MODS built from config classes | The tweaks: drain multipliers (in progress, see M11), stack sizes, spoilage, craft speed, day length, first/third-person camera toggle |
| M05 | Input and UI state | 913 | 75% | Partial | `InputLock` (game input held while a mod window is open), `Hotkeys` (rebindable mod hotkeys with Ctrl/Shift/Alt chords, scopes, conflict warnings against other mods and the game's own bindings, a Hotkeys page in MODS, gated while typing, in menus or the console) | A public `UiState` (paused, typing, menu open) instead of the internal gate, swallowing game input for a key (input capture) |
| M06 | Classic mod menu | 2,963 | 95% | Done | ObenUIKit MODS menu: main menu and pause menu entries, Mods (mods and feature switches), Settings, What's New, Errors, keyboard and controller navigation | Action rows built from config fields beyond the Settings page |
| M07 | UI kit | 2,316 | 70% | Partial | `UiKit.Open` windows in the game's own look, headers, labels, buttons, cycles, toggles, sliders, text fields, key binds, rows, confirm dialogs, toasts, navigation | Tabs, segmented choices, colour swatches, a stepper, cards, docked and floating windows, 3D model preview |
| M08 | WebUi | 2,729 | — | Replaced | Native uGUI Mods menu and windows | Nothing planned: WebView2 is Windows-only and doesn't run through Proton |
| M09 | Save data | 1,355 | 85% | Done | `ModSaves` per-save mod files with size and depth limits, `SaveState` (capture and apply any savable object through the game's own save code), `SaveGate` (saving/loading in flight), `SaveSlots` (characters, slots, save now), ObenSave compressed container | Field binding (`Bind<T>`); everything here still needs a check on a real save |
| M10 | Events and time | 865 | 75% | Partial | `GameEvents` (main menu, scene, world ready, level change, save, load), `GameClock` (time, minute/hour/day events, set time, speed), `WorldState` | A new-game-started event, the load-aborted case, clock overrides (`TimeControl`), visited scenes (`WorldLocation`) |
| M11 | Survival | 924 | 90% | Done | Player stats by console name, heal, damage, died and damaged events, `StatThresholds` (crossing callbacks), `StatDrain` (per-mod multipliers and offsets that combine), eat/drink interception, damage scaling and cancelling, breakdown interception, passed-out and consumed events | Freezing all needs in one call |
| M12 | Networking and lobby | 1,986 | 60% | Partial | ObenCoop.Net: transport interface, direct IP (LiteNetLib), channels, handshake with version and mod-set check, roster, host relay, hot reload handover, blob transfer for big payloads (chunked, checked, acknowledged) and flood control | Steam transport, lobbies and invites, presence |
| M13 | Debug and dev menu | 2,353 | 60% | Partial | DevBridge, Script Runner, MCP tools (more than the reference's tracer), Cheat Menu (god mode, needs, noclip, teleport spots, money, items, time, weather) | Dev menu sections for quests and global flags, watches on live values, the API call tracer |
| M14 | Story | 5,434 | 35% | Partial | Lua variables and actor fields, Lua functions, conversations with start/end events, quest log with status changes and change events | Quest authoring, quest rewind and follow-ups, dialogue interception and response/branch authoring, crimes and fines (`Law`), the deferred story queue |
| M15 | NPCs and animals | 8,269 | 85% | Done | `Npcs` (query, spawned/despawned/died/activity events), `NpcControl` claims (pause, walk, look, hold sight/talk/barks/visibility/movement, animations, look changes, all undone on release), `NpcActions` (teleport, kill, hit, follow, say, comment, talk, look, change level), `Relationships`, the character database, `ModNpcs` (copies of game characters with stable ids, saved per save, looks and blend shapes, orders, weekly `NpcRoutines`, despawn event), `Animals` (actions, taming saved per save, breeding pens, rat traps), police pursuits, robbers, janitors, surveillance, phones in the scene, `PhoneNumbers` | Mod NPC extras (bodies without NPC logic), walking scene node groups, the saved-characters list; mod NPCs walk only on the 11 bodies the game gives AI; routines, saved mod NPCs and taming still need a check on a real save |
| M16 | Player | 6,974 | 85% | Done | Position, facing, safe teleport, state, stats, vitals, events, `PlayerLook` (capture, apply, keep across level loads, write to mod characters), `PlayerMovement` (per-mod speed and jump multipliers), `PlayerControls` locks, `PlayerSleep` (sleep, waits with their own clock label and frozen needs, sleep events, time picker), `PlayerSkills` and `CustomSkills` (kept per save, on the character page), `PlayerEffects`, `PlayerConditions`, `Medical`, `Seats`, `CustomInteractions`, `Wardrobe`, `Weapons` (hit multipliers per target), `PlayerNotes` (notes and books), `Jobs`, `GameAchievements`, `PlayerSubscriptions`, held god mode and frozen needs | Camera lock on a target, look randomise and live slider preview; custom skills, wardrobe, notes and weapons still need an in-game check |
| M17 | Items and inventory | 6,713 | 90% | Done | Item lookup, give/take/count, stacks with quality or liquid, `InventoryEvents` (added, removed, used, held changed: net changes once per frame), `HeldItems`, `StackActions`, `InventorySources` (containers that count as inventory for blueprints and renovations), `Containers`, `FloorItems` (with expiry), `UnloadedStorage` (read and queue changes for containers in scenes that aren't loaded), `LootPools`, `ResourceNodes`, `Spawners`, `Breakables`, `Extractables`, `ItemGivers`, `TaskItems`, consumable values, fuel types, container window buttons | Saving consumable changes; unloaded storage and inventory sources still need a check on a real save |
| M18 | Item and recipe registry | 1,675 | 65% | Partial | `ItemRegistry` with stable hashed ids, `RecipeRegistry`, pickups of mod items | Placeholders for items from removed mods, held models, icons from files (in progress with M21) |
| M19 | Crafting | 3,092 | 85% | Done | `CraftingEvents`, `CraftingStations` (start, stop, pause, resume), `CraftingSpeed` and `CraftingQuality` factors, `CraftingUpgrades`, `CraftingSources` (extra ingredient containers), `Recipes` with an unlock event, `RecipeFilter`, `Recycling`, `Dishwashers`, `GrowingStations`, `Plants`, `PlantActions`, `Autofarms` | A real craft with extra sources, quality and filters still needs an in-game check |
| M20 | Building and furniture | 3,181 | 45% | Partial | Stable furniture ids, placed/removed/moved events, `FurnitureBuilder` (place, move and remove from code without the player's inventory), furniture kinds and building zones | Blueprints, custom furniture from models, furniture loot, shops and pools, flatpacks |
| M21 | Models and assets | 2,892 | 55% | Partial | `ModAssets` (textures, 9-slice sprites, WAV/OGG/MP3 sounds, per mod, released on unload), `ModSounds` (play through the game's mixer groups), OBJ/MTL parsers, `ModelRegistry` (background loading, colliders, pivots, spawning) | Localization bridge, instruments, radios, televisions, model icons, swapping item models |
| M22 | World props and SceneLook | 9,596 | 90% | Done | `SceneIds`, `ObjectPath`, `WorldProps` (primitives, models, furniture and item looks, lights and anchors that persist in the save, optional use prompt), `SceneState`/`SceneLogic` (branches, counters, state machines, sequences, timers, relays with a fired event), `Landmarks` (game and mod map marks), `Signs`, `WorldObjects` with per-object mod data, `WorldLocation` (visited scenes), `SceneLooks` (read character looks from scene files and put them on any body, with blend shapes) | A MODS-menu page for scene look checks; prop and landmark persistence across saves and the use prompt still need an in-game check |
| M23 | Shops and economy | 6,273 | 35% | Partial | `Wallet` (both currencies), bank interest fixed in Unofficial ObenPatches, `Shops` (traders, checkouts, furniture shops, vending machines; stock, `Bought`/`Sold` events), `Prices` (per-mod price modifiers in both directions), `ShopStock` (add goods to traders and vending machines, topped up on restock), `Mail` (letters and parcels to the player, arrival events) | Player-run shops, bank accounts and online banking, invoices, legal services, train tickets, fuel stations, hotels |
| M24 | Doors and transport | 2,873 | 25% | Partial | Doors: list, state, open/close/lock with change events and echo-free remote changes | Door schedules, locks and keys, levels and travel, elevators, ladders, tarp doors, trains, drones, wait stations |
| M25 | World environment | 5,490 | 15% | Partial | Weather get/list/set, lights and switches, power grids (read-only) | Weather zones, sky overrides, water, radiation, lanterns, hazards, surfaces, tanks, flood gates, controlling power devices, showers, saunas, global flags, game settings, screen effects |
| M25b | Housing | 1,811 | 0% | Not started | — | Tenement apartments, tenants, hotels, renovations, parcel lockers, mailboxes |
| M26 | HUD | 2,273 | 70% | Partial | Toasts through the game's notifications, `Hud.Hint` in the game's own hint panel with real key names (`{key:Action}`) and a queue, `Hud.Subtitle`, `Hud.Bar`, all hidden while menus are open | Prompts, using the game's tutorial UI for longer guides |
| M27 | Gambling | 1,403 | 0% | Not started | — | Casino machines, card games, drinking contests, payout scaling |
| M28 | Difficulty and new game | 2,473 | 0% | Not started | — | Reading and overriding difficulty, mod difficulty presets, options on the New Game screen, start choices |
| M29 | Main menu and console | 736 | 70% | Partial | MODS on the main menu, `GameConsole` (mod commands in the F1 console, run a line and read its output) | A general API to add any button to the main menu |
| M29b | Computer apps | 1,003 | 0% | Not started | — | Apps on the in-game computer, standalone app window |
| M29c | Tutorial | 171 | 0% | Not started | — | Tutorial step events |

## Beyond parity

What our stack does that the reference framework doesn't:

- **No BepInEx.** ObenLoader hooks into Unity's own startup files; `oben install` and `oben uninstall` keep backups.
- **Hot reload** of every mod and the framework itself, with dependent mods reloaded in one batch and state carried across through `ReloadState`. Harmony's jumps are kept inside the patched methods so long reload sessions stay stable under Proton, and `patches.check` verifies every patched method.
- **Native look.** ObenUIKit stamps the game's own fonts, colours, sprites and sounds from its menus instead of drawing its own style.
- **Switchable features.** Any part of a mod can be a feature the player turns on or off at once, with its patches applied and removed live.
- **Dev tooling.** DevBridge (JSON socket into the running game), Script Runner (`oben eval`, `oben run --watch` for live-editing Harmony patches), an MCP server, two game copies side by side for co-op testing.
- **The `oben` tool.** Install, launch through Heroic/umu, logs and error report, changelog from commit trailers, save conversion, hook checker after game updates, release packaging.
- **Tests.** Unit tests for the loader, framework, tool, save codec and networking, and `oben check` against the installed game.
- **Mods on top.** Unofficial ObenPatches (game bug fixes, each switchable), Performance Fixes, ObenSave (compressed saves), Cheat Menu, ObenCoop (direct IP, transport-agnostic).
- **What's New** in game, generated from the commit history.
- **Framework extras.** Scene looks also apply the scene's blend shape weights; per-mod skill learning and melee hit multipliers; mod and game skill banners take turns; inventory events report net changes once per frame; mod NPCs the game destroys are noticed and their saved ones come back.
