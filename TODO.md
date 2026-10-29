# TODO

What's planned, roughly in order, with the notes each piece builds on. Tick things off as they land.

## Foundation

- [x] ObenLoader: native loading through Unity's startup files, dependency order, hot reload
- [x] `oben` tool: install, launch through umu/Heroic, logs, bridge calls, MCP server
- [x] DevBridge and Script Runner (`oben eval`, `oben run --watch`)
- [x] ObenFramework: events (MainMenu, WorldReady, save/load), WorldState, F1 console commands, switchable features
- [x] Error report file (errors.log) for bug reports
- [x] Changelog from commit history (`oben changelog`)
- [x] In-game What's New page and popup reading changelog.json (`build/changelog.sh` writes it; the popup needs a tagged release)
- [x] Release packaging (`build/package.sh`, `build/release.sh`; dev mods marked `devOnly`)
- [x] Unit tests (`dotnet test`) and `oben check` for hooks after game updates
- [x] GPL-3.0 license
- [x] THIRD-PARTY-NOTICES.md (NomadsReach MIT code, LiteNetLib)
- [ ] GitHub URL for the player README.txt and the docs, once the repository is up
- [x] Hot reload got slower the longer the game ran: a cascade of all 9 mods took 5.2 s on the first rebuild and 15.2 s on the 24th. Two causes, both in Harmony: every patch and unpatch looked each handler up by walking all loaded modules (old copies included), and `UnpatchAll(id)` rebuilt and recompiled a method once per handler it removed, even when nothing was left. Fixed with `PatchLookups` and `Unpatching.RemoveAll`: now 2.5 s on the first and 5 s on the 24th (2026-10-29)
- [ ] Hot reload still slows a little per round (2.5 s to 5 s over 24 rounds): compiling Harmony's new replacement methods gets slower as the game holds more code (0.16 ms per method fresh, 0.8 ms after 12 reloads). Keeping 10,000 extra dynamic methods alive only costs 20%, so it is mostly the growing number of loaded assemblies inside Mono's JIT. Harmony also keeps every old replacement alive (`HarmonySharedState.originals`/`originalsMono`); dropping those might help a little

## ObenFramework (core functionality)

Each module stays small and gets ported or written when the first mod needs it. The MIT framework in `example-framework/` covers most of these already; `docs/research/framework-reference.md` maps what's worth taking.

- [x] Player: find the player, stats, safe teleport, damage and healing, died/damaged events
- [x] Items: give, take, spawn, register new items and recipes at runtime
- [x] Money: wallet in both currencies
- [x] Saves: per-mod data in side files next to the save, object state capture/apply, save gate, slots and save now (need an in-game test with real saves)
- [x] Input: input lock, rebindable mod hotkeys with a Hotkeys page
- [x] Notifications and HUD: toasts, hints in the game's hint panel, subtitles, bars
- [ ] Settings: typed settings per mod with an in-game page
- [x] Clock and weather: time of day, minute/hour/day events, weather
- [x] Dialogue and Lua: variables, Lua functions, conversations, started/ended events
- [x] Quests: quest log, status changes, change events
- [x] World: doors, interactables with stable addresses, furniture ids and events, lights, switches, power (read-only)
- [x] Building: place, move and remove furniture from code; containers and floor items with stable ids and events
- [x] Shops and mail: shop stock and prices for traders and vending machines, letters and parcels
- [x] Assets: textures, sprites, WAV/OGG/MP3 sounds and OBJ/MTL models from a mod's folder
- [x] NPCs and animals: mod NPCs with looks, orders and routines, NPC actions, relationships, animals and breeding, police, robbers, janitors, surveillance, phones in the scene
- [x] Player actions: movement multipliers, sleep and waits, skills and custom skills, effects, conditions, medical, seats, interactions, wardrobe, weapons, notes and books, jobs, achievements
- [x] Items and crafting: inventory events, extra inventory and crafting sources, containers in unloaded scenes, loot, resource nodes, spawners, breakables, stations, speed and quality, recipe filters, recycling, growing, autofarms
- [x] World props, scene logic, landmarks, signs, visited scenes and scene looks (character looks from scene files)
- [x] Environment and housing: flags, settings, sky, screen effects, power, hazards, water, weather, fixtures; apartments, tenants, hotels, renovations, mailboxes
- [x] Economy and gambling: banks, ledger, invoices, legal services, player-run shops, traders, fuel; casino, card games, contests
- [x] Story and new game: quest and dialogue authoring, law, story queue, sessions, difficulty presets, new game options, tutorial events
- [x] Transport and furniture: locks, exits and travel, elevators, ladders, trains, drones; custom furniture, blueprints; item icons and models, radios, TVs, guitar, computer apps
- [x] NPCs: queries, spawn events, control claims, schedules, mod phone numbers
- [x] Player look capture/apply and survival hooks (thresholds, drain, eating and damage interception)
- [x] Loader and core: mod id and game version checks, isolated framework parts and patch areas, log rate limits and log-once, load-aborted event, TimeControl, placeholders for items of removed mods
- [ ] Coverage of the MIT framework: see PROGRESS.md (in-game test backlog in its Missing column)

## ObenUIKit

UI that looks like the game's own: built from the game's uGUI elements, TMP fonts (Erika Ormig SDF) and materials, with keyboard and controller navigation that cooperates with the game's `*NavigationHandler` classes. The reference framework's `src/Ui` and mod menu are the starting point.

- [x] Theme taken from the main menu and the pause menu (stamped, not cloned)
- [x] Window, headers, labels, buttons, cycles, toggles, sliders
- [x] Keyboard/controller navigation, text fields, key binds, confirm dialogs, toasts
- [x] Long lists, tabs, tooltips, segmented choices, swatches, steppers, cards, docked and floating windows, 3D model previews
- [x] HUD prompts and guides, main menu buttons for any mod, UiState and input capture
- [x] Mod menu: MODS in the main and pause menus, Mods page with mod and feature switches
- [x] Settings, Errors and What's New pages

## ObenSave

A save format replacement as its own mod: open, documented JSON, compressed, with room for mod data, and able to read the game's own `.tnmt` saves so nothing is lost by switching. The game's saves are already plain minified JSON (`Saves/<character>/<slot>/*.tnmt`), so the gains are size, a stable documented layout and a proper place for mod data.

- [x] Format spec in `docs/modding/save-format.md`
- [x] Read and write hooks, off by default until tested with real saves
- [x] Import, export and convert with `oben save`

## ObenCoop

Built from the reference co-op mod in `example-mp/` (NomadsReach/Obenseuer-Coop, MIT; its author is fine with us building on it): bring it in as it is, make it run on our loader, framework and network layer, then replace it piece by piece. The plan is in `docs/modding/coop.md` ("Plan: start from the reference mod").

- [ ] Import the reference sources unchanged (one commit, MIT headers)
- [ ] Load on ObenLoader and ObenFramework (no BepInEx, no ObenseuerCore); fill framework gaps it needs
- [ ] Session.cs sends go through ObenCoop.Net (direct IP and Steam)
- [ ] `oben check` clean against 0.5.13
- [ ] Replace subsystems one by one with framework-event versions; fix the reference's own limits (ideas.md, co-op)

- [x] Transport interface, direct IP transport, channels, version handshake, persistent player id
- [x] Session: host, join, leave, roster (tested with two copies on one machine)
- [ ] Other players' bodies: puppets, pose, sitting, appearance
- [x] Chat (console command for now)
- [x] Big payloads (chunked, checked, acknowledged blob transfer) and flood control
- [ ] Shared clock and weather, sleep vote
- [ ] Join ticket (send the host's world to a joining player)
- [ ] Shared world: doors, items, containers, building, stations, shops, quests
- [x] Steam transport as a second option (tested on a fake Steam; needs a real-Steam check)
- [x] Lobbies: lists, join codes, invites, version checks
- [x] Presence data layer (positions with interpolation)
- [ ] Lobby page in ObenUIKit
- [ ] Free roam (players in different levels), last
- [x] Second game instance on one machine (`oben install --multi-instance`, `oben launch --instance 2`)

## Cheat Menu

- [x] Window on Insert and a MODS page: god mode, needs, heal, noclip, move speed, teleport spots, money, items, time, weather, crime (needs its in-game check)
- [x] Developer menu (F8, off by default): quests, global flags, watches, session and story, player and world, sections from mods

## Game Tweaks

- [x] Needs, stack size, spoilage, crafting and dishwasher speed, no minimum skill, renovation cost/money-only/time, day length, third-person camera; each its own switch, off by default (needs its in-game checks, see docs/modding/game-tweaks.md)
- [ ] Third-person camera, a complete rewrite: a camera of our own instead of the game's leftover mode (today the tweak only lifts the game's locks and points its dolly behind the head, see `CameraLocks`). A free orbit around the player, the body turning to where you walk instead of with the mouse, a shoulder offset, smooth collision and zoom, and interacting and aiming that still work from behind.
  - Idea for the same rewrite: better player animations. The body moves with what look like Unity's default animations; try a physics-based (active ragdoll or procedural) layer on top, or reuse a better set the game already has (NPC walk and idle clips, say). Co-op puppets would want the same.
- [ ] Bug: in third person NPCs look at the camera, not at the player. The game's `CharacterHeadController` sets `eyes.lookTarget = ThirdPersonCameraCollision.instance.transform` (the camera) when the player comes near; while the camera is out, point them at the player's head instead (a patch there, or a target that follows the head bone).

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
- [x] RenovationMaterialsOnce: `TenementController.RemoveRequiredItems` passed the full amount to every storage instead of what's still missing, so renovations took the item from every storage that held it
- [ ] BankExchangeCoins: changing Real Money into coins uses an integer division (`TotalOCSupply / DailyAverageOcSpending`), so it pays 0 coins whenever the bank's coin supply is below its daily spending; the quote checks the RM supply instead and divides by zero when daily coin spending is 0
- [ ] ScreenshotFolder: F11 and F12 never save a screenshot. `TakeScreenshot` makes `Obenseuer/Screenshots` but hands Unity the relative path `Screenshots/...`, which the player resolves inside `Obenseuer_Data`, so every shot ends in "Failed to store screen shot" (seen 2026-10-29). Pass the full path, or create the folder Unity writes to
- [ ] More from playing: check `known-bugs.md`'s "not fixed" list (subscription dates) and Player.log errors during real play

## Review follow-ups

Smaller problems found in the code review of the third round (2026-10-29), left for later. The ones that mattered were fixed then.

- [ ] Loader: a failed mod logs its error twice (ModLifecycle, then the start-up pass in Loader); `InfoOnce`/`WarnOnce` spend their key even when the rate limiter drops the line
- [x] Framework: a part whose setup throws keeps its `[HarmonyPatch]` classes applied (they go on up front, per patch area). Fixed: its area is switched off; the same for a feature whose class or `OnEnable` fails
- [ ] Framework: `ItemRegistry.Unregister` leaves stacks picked up this session holding the old `Item` (sellable, actions from the unloaded assembly) until the next load
- [ ] Framework: a checkout line a modifier prices at 0 is reported with Oc 0, though MarketShop charges at least 1
- [ ] Kit: every window adds a close entry to its owner's tracker and never takes it out (dialogs open one per question)
- [ ] Kit: a list's hover and Enter target stay on the old index when the wheel scrolls under a resting pointer (store the row, not its index)
- [ ] Kit: handles from an earlier build (`ButtonHandle.Text`, `CycleHandle.Set`, `SliderHandle.Set`, `UiWindow.Title`) throw after a rebuild; `LabelHandle` guards, the rest should too
- [ ] Kit: main menu mod buttons are copied active, so the copy's localization components run before they are stripped (check the label in game)
- [ ] Kit: a guide waits forever when another mod hides its prompt
- [ ] Kit: float settings miss the number ladder (`NumberSteps` compares the widened float with 1e-9, so 0.15f adds a second "0.15")
- [ ] Game Tweaks, RenovationTime: the progress bar and the hours use the scaled `GetRequiredTimeHours`, but orders placed before the factor changed keep their own time (the bar is wrong, even negative), and Instant still shows hours. Story upgrades are scaled since 2026-10-29. Remember each order's total at `AddUpgrade` and work progress out from that
- [ ] Game Tweaks: switching scroll zoom on inside a first-person trigger lifts that trigger's lock (triggers already active aren't registered)
- [ ] Tracer: failed calls skip the per-window line cap
- [ ] Cheat Menu: watches call any property getter four times a second; a watch whose type is gone searches every assembly on each rebuild; stat sliders without a maximum stop at 100
- [ ] Co-op, direct IP: idle connections with the public key hold the transport's slots for up to 5 s each (handshake timeout); limit unfinished handshakes per address
- [ ] Co-op: lobby list results never expire (joining a stale one ends in a lookup timeout); a failed `Connect` leaves LiteNetLib running until the next Stop; restoring a session when Steam never started doesn't leave the Steam lobby

## Expansion mods

Ideas welcome; each is its own mod on top of ObenFramework.
