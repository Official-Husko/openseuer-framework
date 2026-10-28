# Progress

How much of the MIT [Obenseuer Modding Framework](https://github.com/NomadsReach/Obenseuer-Modding-Framework) (ObenseuerCore, by NomadsReach) our own stack covers, module by module, on the way to full feature parity and past it. The module ids and sizes come from the map in [docs/research/framework-reference.md](docs/research/framework-reference.md).

**Keep this file current.** Every change that adds, finishes or drops a feature covered here updates its row, the module's coverage and the totals below in the same commit.

_Last updated: 2026-10-28._

## Totals

| | |
| --- | --- |
| Reference size | 89,607 lines in 31 modules (WebUi's 2,729 lines left out: it's Windows-only and our native Mods menu replaces it) |
| Covered, weighted by module size | **about 25%** |
| Modules done or nearly done (80%+) | 1 of 31 |
| Modules started | 23 of 31 |
| Modules in progress right now | 9 |

Coverage is an estimate per module of how much of what the reference module offers a mod author can do with ours, weighted by the reference module's line count. A small module done counts little; the big ones (NPCs, SceneLook, shops, player) move the number most.

Status: **Done**, **Partial** (some of it, listed), **In progress** (being built now), **Not started**, **Replaced** (ours does the same job differently).

## By module

| Id | Module | Size | Coverage | Status | Ours | Missing |
| --- | --- | ---: | ---: | --- | --- | --- |
| M01 | Bootstrap and per-mod handle | 1,720 | 70% | Partial | ObenLoader: discovery, dependency order, `ObenMod` as the per-mod handle, hot reload | Mod id and file name validation (`ModIds`), supported game build check, isolating one module's init failure from the rest |
| M02 | Patching | 311 | 70% | Partial | Harmony through the loader (`[HarmonyPatch]`, per-mod instance, unpatch on unload), feature categories, `oben check` | Patch by name and argument types from code, a failed core patch disabling only its own area with `IsAvailable` reporting |
| M03 | Logging | 363 | 65% | Partial | `LogHub`, `ModLogger`, latest/previous logs, repeat collapsing for Unity, `errors.log` report | Per-mod line rate limit, change-only `Debug(key, msg)`, log-once helper |
| M04 | Config and tweaks | 1,206 | 30% | Partial | `LoadConfig<T>`, Settings page in MODS built from config classes | The tweaks: drain multipliers (in progress, see M11), stack sizes, spoilage, craft speed, day length, first/third-person camera toggle |
| M05 | Input and UI state | 913 | 40% | In progress | `InputLock` (game input held while a mod window is open), pause and console gating in the Cheat Menu | Rebindable mod hotkeys with chords and conflict warnings (in progress), a public `UiState` (paused, typing, menu open), input capture |
| M06 | Classic mod menu | 2,963 | 95% | Done | ObenUIKit MODS menu: main menu and pause menu entries, Mods (mods and feature switches), Settings, What's New, Errors, keyboard and controller navigation | Action rows built from config fields beyond the Settings page |
| M07 | UI kit | 2,316 | 70% | Partial | `UiKit.Open` windows in the game's own look, headers, labels, buttons, cycles, toggles, sliders, text fields, key binds, rows, confirm dialogs, toasts, navigation | Tabs, segmented choices, colour swatches, a stepper, cards, docked and floating windows, 3D model preview |
| M08 | WebUi | 2,729 | — | Replaced | Native uGUI Mods menu and windows | Nothing planned: WebView2 is Windows-only and doesn't run through Proton |
| M09 | Save data | 1,355 | 45% | In progress | `ModSaves` per-save mod files, ObenSave compressed container | Capture and apply of any savable object through the game's own save code, busy gate, save slots and save now (in progress) |
| M10 | Events and time | 865 | 75% | Partial | `GameEvents` (main menu, scene, world ready, level change, save, load), `GameClock` (time, minute/hour/day events, set time, speed), `WorldState` | A new-game-started event, the load-aborted case, clock overrides (`TimeControl`), visited scenes (`WorldLocation`) |
| M11 | Survival | 924 | 45% | In progress | Player stats by console name, heal, damage, died and damaged events | Threshold callbacks, per-mod drain multipliers, eat/drink and damage interception, pass-out and breakdown events (in progress) |
| M12 | Networking and lobby | 1,986 | 45% | In progress | ObenCoop.Net: transport interface, direct IP (LiteNetLib), channels, handshake with version and mod-set check, roster, host relay, hot reload handover | Chunked big-payload transfer and receive budget (in progress), Steam transport, lobbies and invites, presence |
| M13 | Debug and dev menu | 2,353 | 60% | Partial | DevBridge, Script Runner, MCP tools (more than the reference's tracer), Cheat Menu (god mode, needs, noclip, teleport spots, money, items, time, weather) | Dev menu sections for quests and global flags, watches on live values, the API call tracer |
| M14 | Story | 5,434 | 35% | Partial | Lua variables and actor fields, Lua functions, conversations with start/end events, quest log with status changes and change events | Quest authoring, quest rewind and follow-ups, dialogue interception and response/branch authoring, crimes and fines (`Law`), the deferred story queue |
| M15 | NPCs and animals | 8,269 | 0% | In progress | — | NPC queries, spawn events, control claims, schedules, phone lines (in progress); mod NPCs, NPC appearance, animals and breeding, robbers, janitors, surveillance, relationships |
| M16 | Player | 6,974 | 25% | In progress | Position, facing, safe teleport, state (dead, seated, sleeping, crouching, swimming), stats, vitals, events | Appearance capture and apply (in progress), movement speed API, skills and custom skills, effects and conditions, medical, seats, interactions, weapons, wardrobe, notes, books, achievements, jobs, sleep and wait |
| M17 | Items and inventory | 6,713 | 30% | In progress | Item lookup and search, give/take/count, spawn in the world | Containers and floor items with stable ids and events (in progress), item added/removed events, equip held, extra inventory sources, editing containers in unloaded scenes, loot pools, resource nodes, spawners, breakables, task items |
| M18 | Item and recipe registry | 1,675 | 65% | Partial | `ItemRegistry` with stable hashed ids, `RecipeRegistry`, pickups of mod items | Placeholders for items from removed mods, held models, icons from files (in progress with M21) |
| M19 | Crafting | 3,092 | 5% | Not started | Recipe registry only | Craft start/finish events, stations, quality, speed, upgrades, extra ingredient sources, recipe filter, recycling, dishwashers, growing, autofarms |
| M20 | Building and furniture | 3,181 | 25% | In progress | Stable furniture ids, placed/removed/moved events | Place, move and remove through an API without the player's inventory (in progress), blueprints, custom furniture from models, furniture loot and shops |
| M21 | Models and assets | 2,892 | 0% | In progress | — | Textures and sprites, WAV/OGG sounds, OBJ/MTL models, model registry (in progress); localization bridge, instruments, radios, televisions |
| M22 | World props and SceneLook | 9,596 | 5% | Not started | Stable object addresses (`ObjectAddress`) | Props that persist in the save, scene state machines, counters and relays, landmarks, signs, re-applying character looks from scene files (SceneLook) |
| M23 | Shops and economy | 6,273 | 10% | In progress | `Wallet` (both currencies), bank interest fixed in Unofficial ObenPatches | Shop stock, price hooks, purchase and sale events, mail (in progress); traders, player-run shops, bank accounts and online banking, invoices, legal services, train tickets, fuel stations |
| M24 | Doors and transport | 2,873 | 25% | Partial | Doors: list, state, open/close/lock with change events and echo-free remote changes | Door schedules, locks and keys, levels and travel, elevators, ladders, tarp doors, trains, drones, wait stations |
| M25 | World environment | 5,490 | 15% | Partial | Weather get/list/set, lights and switches, power grids (read-only) | Weather zones, sky overrides, water, radiation, lanterns, hazards, surfaces, tanks, flood gates, controlling power devices, showers, saunas, global flags, game settings, screen effects |
| M25b | Housing | 1,811 | 0% | Not started | — | Tenement apartments, tenants, hotels, renovations, parcel lockers, mailboxes |
| M26 | HUD | 2,273 | 20% | In progress | Toasts through the game's notifications | Hints in the game's own hint overlay, subtitles, HUD bars (in progress), prompts |
| M27 | Gambling | 1,403 | 0% | Not started | — | Casino machines, card games, drinking contests, payout scaling |
| M28 | Difficulty and new game | 2,473 | 0% | Not started | — | Reading and overriding difficulty, mod difficulty presets, options on the New Game screen, start choices |
| M29 | Main menu and console | 736 | 70% | Partial | MODS on the main menu, `GameConsole` (mod commands in the F1 console, run a line and read its output) | A general API to add any button to the main menu |
| M29b | Computer apps | 1,003 | 0% | Not started | — | Apps on the in-game computer, standalone app window |
| M29c | Tutorial | 171 | 0% | Not started | — | Tutorial step events |

## Beyond parity

What our stack does that the reference framework doesn't:

- **No BepInEx.** ObenLoader hooks into Unity's own startup files; `oben install` and `oben uninstall` keep backups.
- **Hot reload** of every mod and the framework itself, with dependent mods reloaded in one batch and state carried across through `ReloadState`.
- **Native look.** ObenUIKit stamps the game's own fonts, colours, sprites and sounds from its menus instead of drawing its own style.
- **Switchable features.** Any part of a mod can be a feature the player turns on or off at once, with its patches applied and removed live.
- **Dev tooling.** DevBridge (JSON socket into the running game), Script Runner (`oben eval`, `oben run --watch` for live-editing Harmony patches), an MCP server, two game copies side by side for co-op testing.
- **The `oben` tool.** Install, launch through Heroic/umu, logs and error report, changelog from commit trailers, save conversion, hook checker after game updates, release packaging.
- **Tests.** Unit tests for the loader, framework, tool, save codec and networking, and `oben check` against the installed game.
- **Mods on top.** Unofficial ObenPatches (game bug fixes, each switchable), Performance Fixes, ObenSave (compressed saves), Cheat Menu, ObenCoop (direct IP, transport-agnostic).
- **What's New** in game, generated from the commit history.
