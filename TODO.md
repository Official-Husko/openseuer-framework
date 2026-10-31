# TODO

What's planned, roughly in order, with the notes each piece builds on. Tick things off as they land.

## Foundation

- [x] ObenLoader: native loading through Unity's startup files, dependency order, hot reload
- [x] `oben` tool: install, launch through umu/Heroic, logs, bridge calls, MCP server
- [x] DevBridge and Script Runner (`oben eval`, `oben run --watch`)
- [x] ObenFramework: events (MainMenu, WorldReady, save/load), WorldState, F1 console commands, switchable features
- [x] Error report file (errors.log) for bug reports
- [x] Changelog from commit history (`oben changelog`)
- [x] In-game What's New page and popup reading changelog.json (`build/changelog.sh` writes it)
- [x] Release packaging (`build/package.sh`, `build/release.sh`; dev mods marked `devOnly`)
- [x] Unit tests (`dotnet test`) and `oben check` for hooks after game updates
- [x] GPL-3.0 license
- [x] THIRD-PARTY-NOTICES.md (NomadsReach MIT code, LiteNetLib)
- [x] GitHub URL for the player README.txt and the docs (releases, wiki and issues)
- [x] Hot reload got slower the longer the game ran: a cascade of all 9 mods took 5.2 s on the first rebuild and 15.2 s on the 24th. Two causes, both in Harmony: every patch and unpatch looked each handler up by walking all loaded modules (old copies included), and `UnpatchAll(id)` rebuilt and recompiled a method once per handler it removed, even when nothing was left. Fixed with `PatchLookups` and `Unpatching.RemoveAll`: now 2.5 s on the first and 5 s on the 24th (2026-10-29)
- [ ] Hot reload still slows a little per round (2.5 s to 5 s over 24 rounds): compiling Harmony's new replacement methods gets slower as the game holds more code (0.16 ms per method fresh, 0.8 ms after 12 reloads). Keeping 10,000 extra dynamic methods alive only costs 20%, so it is mostly the growing number of loaded assemblies inside Mono's JIT. Harmony also keeps every old replacement alive (`HarmonySharedState.originals`/`originalsMono`); dropping those might help a little

## ObenFramework (core functionality)

Each module stays small and gets ported or written when the first mod needs it. The MIT framework in `example-framework/` covers most of these already; `docs/research/framework-reference.md` maps what's worth taking.

- [x] Player: find the player, stats, safe teleport, damage and healing, died/damaged events
- [x] Items: give, take, spawn, register new items and recipes at runtime
- [x] Money: wallet in both currencies
- [x] Saves: per-mod data in side files next to the save, object state capture/apply, save gate, slots and save now (writing on real saves is checked; the load, reset and hot reload round trip, save state and slots are still open in to-test.md area 13)
- [x] Input: input lock, rebindable mod hotkeys with a Hotkeys page
- [x] Notifications and HUD: toasts, hints in the game's hint panel, subtitles, bars
- [x] Settings: typed settings per mod with an in-game page (`LoadConfig<T>`, the Settings page in MODS, `ModSettings.Register` and `Draw`)
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
- [x] Coverage of the MIT framework: every module of PROGRESS.md at 100% since 1.0.0 (WebUi replaced by the native menus); the in-game checks still open are in to-test.md
- [x] Scene looks on mod NPCs (M22): fixed. The giant head was the clothing kit's meshes, authored standing along Z, read in the body's Y-up space (each mesh is now brought into the body's space through a shared bone); looks also carried every clothing variant of the kit (now only the ones the scene shows). Blender-rig looks are retargeted onto slider bodies: the role table knows the rig, the rebuilt skeleton is stood in a T-pose for its avatar and the look is kept on the body's hips.

## ObenUIKit

UI that looks like the game's own: built from the game's uGUI elements, TMP fonts (Erika Ormig SDF) and materials, with keyboard and controller navigation that cooperates with the game's `*NavigationHandler` classes. The reference framework's `src/Ui` and mod menu are the starting point.

- [x] Theme taken from the main menu and the pause menu (stamped, not cloned)
- [x] Window, headers, labels, buttons, cycles, toggles, sliders
- [x] Keyboard/controller navigation, text fields, key binds, confirm dialogs, toasts
- [x] Long lists, tabs, tooltips, segmented choices, swatches, steppers, cards, docked and floating windows, 3D model previews
- [x] HUD prompts and guides, main menu buttons for any mod, UiState and input capture
- [x] Mod menu: MODS in the main and pause menus, Mods page with mod and feature switches
- [x] Settings, Errors and What's New pages
- [ ] Later (owner's idea, 2026-10-31): reuse the compact rows of the game's Custom difficulty page (title on the left, `< value >` on the right, the description box beside the list) where they fit, like the Mods menu's pages and settings. The New Game screen's mod option rows (M28) copy those rows already, so that code is the starting point

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
- [x] Steam transport as a second option (tested on a fake Steam, and on real Steam with one account as Spacewar and TOXIKK; two real accounts are still to check, to-test.md area 4)
- [x] Lobbies: lists, join codes, invites, version checks
- [x] Presence data layer (positions with interpolation)
- [x] Lobby page in ObenUIKit: the co-op window in the main and pause menus (host, join by code, find games, players, join codes hidden in streamer mode)
- [ ] Free roam (players in different levels), last
- [x] Second game instance on one machine (`oben install --multi-instance`, `oben launch --instance 2`)

## Cheat Menu

- [x] Window on Insert and a MODS page: god mode, needs, heal, noclip, move speed, teleport spots, money, items, time, weather, crime (needs its in-game check)
- [x] Developer menu (F8, off by default): quests, global flags, watches, session and story, player and world, sections from mods

## Game Tweaks

- [x] Needs, stack size, spoilage, crafting and dishwasher speed, no minimum skill, renovation cost/money-only/time, day length, third-person camera; each its own switch, off by default (the table's checks passed 2026-10-29; third person by hand, in a save and in a first-person trigger are still open, to-test.md area 3)
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
- [x] ItemGiverRefillTime: refilling item givers (cup dispensers) were subscribed to the clock twice after every level load (`OnLoadingGame` calls `Start` again and its `startDone` guard is never set), so they refilled twice as fast and leaving their level logged "InteractableItemGiver is null!"; the subscription now happens once
- [ ] BankExchangeCoins: changing Real Money into coins uses an integer division (`TotalOCSupply / DailyAverageOcSpending`), so it pays 0 coins whenever the bank's coin supply is below its daily spending; the quote checks the RM supply instead and divides by zero when daily coin spending is 0
- [x] ScreenshotFolder: F11 and F12 never saved a screenshot (the game handed Unity a path relative to `Obenseuer_Data`); they now land in the Screenshots folder next to the game
- [x] ScreenshotHiddenHud: a no-UI screenshot taken while the HUD was hidden left the hide-HUD key needing two presses (the game added its hide entry twice); with the HUD hidden it now takes an ordinary screenshot
- [x] StrayLightControllers: three lamps carry a `LightController` without a `Light` (Open Sewer Tenement, Interior Bazaar Pontikka Roof, Interior Bazaar Mallment), so `Awake` threw a NullReferenceException on every load of those levels; such a controller is now switched off before `Awake`, as Unity ends up doing anyway
- [x] HeadLampPrompt: taking off a hat with a head lamp before ever switching it on left the "Head Lamp [B]" prompt on screen (the head slot destroys the lamp without its `onUnequip`); the prompt is now hidden when the head slot changes
- [x] AutoConsumeLastItem: the workbench's auto-eat ate a phantom copy of the last item of a stack and then threw in `SlotController.TakeItem`; candidates whose slot is already empty are skipped
- [x] DateWeekdayShift: fixed in ObenPatches (docs/game/known-bugs.md). No level has a weekday `DateRelay`; the autofarms in the Potato Mine, Tenement A, Kolhola and the Fungi Boutique now replant on their listed days.
- [ ] MusicSliderLevelMusic (check first): the Music slider doesn't reach the levels' background music. Measured 2026-10-30 with a tone on each mixer group against the game's output: only the mixer group named Music follows the Music slider and every other group follows Sound, and Interior Player Tenement's "Music Kolhola" source plays through Soundscape, so it follows Sound. Find out which sources use the Music group at all (main menu, radios, scene music in other levels) and whether the level music is meant as ambience before deciding on a fix (route music sources to the Music group, or have the Music slider also drive them)
- [ ] ExchangeCreditTruncation (check first): `Bank.Exchange` pays `(int)(amount * rate)` in float, so 100 OC at Kolhola's money exchange (rate 0.06) pays 5 RM instead of 6: 0.06f is slightly less than 0.06 and the product 5.99999 is cut to 5. Measured 2026-10-31 on a copy of Slot1. Any amount can lose up to a whole unit this way, in either direction. A fix would round the product to a few decimals before the cut (keep the game's whole-unit credit); `Banks.Quote` and `BankExchangeMath` would follow it when the fix is on
- [x] LanternStaleTimer: blowing out a candle lantern left its timer running, so a candle lit since went out at the old one's end; a timer with time left is now stopped when the candle goes out
- [x] HotelDayOnce: hotel stays only counted down once per visit to the hotel's level (`lastCheck` became the clock's own object); the hotel now keeps a copy
- [x] CleaningDayLate: an apartment's cleaning job ended a day late (the midnight check read the clock's day before it moved on); it now compares the day the event passes in
- [x] GamblingNeedMood: the urge to gamble only counted toward the mood for smokers (its test read `SmokingNeed`); the test now reads the gambling urge
- [x] CardTableNetWinnings: a card table added the stake back instead of the payout at the end of a game, so its count was 0 and the NPCs' "Player Ends On Win/Loss" remark never played; the payout is counted now
- [x] RatBetOverwrite: a second bet in the same rat fight round replaced the first (both charged, only the last counted), and picking a rat moved the stake even without a bet; bets on one rat now add up, a bet on another rat refunds the first, and a stake stays on its rat
- [ ] SlotDoublingStats (minor): the slot machine's double or nothing books the gambling statistics twice over. A won toss books the whole doubled amount as a new win (12 won, doubled to 24: 36 booked), and a coin on its edge books the amount as lost though the coins stay in the machine. Measured 2026-10-31 on a copy of Slot1. These feed gambling addiction and mood. A fix would book only the gain of a won toss and nothing for the edge
- [ ] NewGameAfterCredits (check first): after the ending credits (`StatsAfterCreditsUI` loads the main menu through the loading screen) `SaveController.LastSaveFolderPath` is still the finished game's folder; only the pause menu's quit clears it. The first save of a new game started then copies that folder into the new one (`SaveGame` copies everything but Info, Globals, the current level and the dialogue), so the new character would start with the old game's other levels and mod data. Read 2026-10-31 in the code, not seen in the game. A fix would clear the path when a new character is confirmed (`NewGame.SaveCharacter`), as the pause menu does
- [ ] More from playing: check `known-bugs.md`'s "not fixed" list (subscription dates) and Player.log errors during real play

## Crashes and bug reports

- [ ] Crash prompt: the loader marks a session as running and clears the mark when the game quits. At the next start a mark left behind means the game crashed (or was killed): offer a report on the title screen with that crash's Crash_ folder in it. Needs no game files changed.
- [ ] Crash handler wrapper: Unity starts `UnityCrashHandler64.exe --attach <pid> <handle>` next to the exe and it writes `%TEMP%\Loiste Interactive\Obenseuer\Crashes\Crash_<UTC time>` (crash.dmp, Player.log). A wrapper installed in its place (the original kept as `UnityCrashHandler64.original.exe`, put back by `oben uninstall`) would start the original with the same arguments, so Unity's dumps keep working, then make a report and offer it the moment the game dies. A small native Windows exe; the Go companion app can build it, since Go cross-compiles to Windows. Check first on a copy of the game folder that the player starts with a replaced or missing handler, and what Steam's file check does with it.
- [ ] Game errors seen 2026-10-30 in Open Sewer Tenement: `[WaypointConnection] nearest.node is null` (four positions), `ArgumentException: no nodes passed` and `Cannot apply GraphUpdateScene, no points defined`. Look for the cause; maybe a fix for ObenPatches.

## Review follow-ups

Smaller problems found in the code review of the third round (2026-10-29), left for later. The ones that mattered were fixed then.

- [ ] Loader: a failed mod logs its error twice (ModLifecycle, then the start-up pass in Loader); `InfoOnce`/`WarnOnce` spend their key even when the rate limiter drops the line
- [x] Framework: a part whose setup throws keeps its `[HarmonyPatch]` classes applied (they go on up front, per patch area). Fixed: its area is switched off; the same for a feature whose class or `OnEnable` fails
- [ ] Framework: `ItemRegistry.Unregister` leaves stacks picked up this session holding the old `Item` (sellable, actions from the unloaded assembly) until the next load
- [ ] Framework: a checkout line a modifier prices at 0 is reported with Oc 0, though MarketShop charges at least 1
- [ ] Kit: every window adds a close entry to its owner's tracker and never takes it out (dialogs open one per question)
- [ ] Kit: a list's hover and Enter target stay on the old index when the wheel scrolls under a resting pointer (store the row, not its index)
- [x] Kit: handles from an earlier build (`ButtonHandle.Text`, `CycleHandle.Set`, `SliderHandle.Set`, `UiWindow.Title`) threw after a rebuild; they guard like `LabelHandle` now and have `Alive` (checked in game on a closed window)
- [ ] Kit: main menu mod buttons are copied active, so the copy's localization components run before they are stripped (check the label in game)
- [ ] Kit: a guide waits forever when another mod hides its prompt
- [x] Kit: float settings missed the number ladder (`NumberSteps` compared the widened float with 1e-9, so 0.15f added a second "0.15"); it now compares with a tolerance a float's precision fits in (unit tested)
- [ ] Game Tweaks, RenovationTime: the progress bar and the hours use the scaled `GetRequiredTimeHours`, but orders placed before the factor changed keep their own time (the bar is wrong, even negative), and Instant still shows hours. Story upgrades are scaled since 2026-10-29. Remember each order's total at `AddUpgrade` and work progress out from that
- [ ] Game Tweaks: switching scroll zoom on inside a first-person trigger lifts that trigger's lock (triggers already active aren't registered)
- [x] Tracer: failed calls skipped the per-window line cap; they now have a cap of their own (as many again, unit tested)
- [ ] Cheat Menu: watches call any property getter four times a second; a watch whose type is gone searches every assembly on each rebuild; stat sliders without a maximum stop at 100
- [ ] Co-op, direct IP: idle connections with the public key hold the transport's slots for up to 5 s each (handshake timeout); limit unfinished handshakes per address
- [ ] Co-op: lobby list results never expire (joining a stale one ends in a lookup timeout); a failed `Connect` leaves LiteNetLib running until the next Stop; restoring a session when Steam never started doesn't leave the Steam lobby

## Expansion mods

Ideas welcome; each is its own mod on top of ObenFramework.

- [ ] Health Conditions (submod, M): the game has a condition system (`BaseHealthCondition`, `PlayerStatsDiseasesAndConditions`, hooks in hygiene and radiation), and the doctor's menu already sells treatments for fungal disease, growths and radiation. But 0.5.13 ships no condition assets, so nothing ever happens: none were loaded even after `Resources.LoadAll` (checked 2026-10-30). A submod could finish it:
  - its own conditions, made at runtime as `BaseHealthCondition` instances: for example lice or a rash from low hygiene, radiation sickness from a high dose, fungal growths from SMV, infected wounds. Each has a strength, a duration and effects through `StatDrain` and `PlayerEffects`;
  - put them into the game's own lists (`lowHygieneConditions`, `highDoseConditions`) after every level and save load, and keep the active ones per save through `ModSaves`. Check first what the game's save does with a runtime condition in `activeConditions` (every save so far stores the lists empty); if it can't store one, keep it out of the save so the save loads without the mod;
  - fix the game's condition bugs it would rely on (in known-bugs.md): `ApplyCondition` never adds a non-stacking condition, `UpdateStats` changes the list it loops over, and radiation's strength is 0 over the threshold;
  - tie the doctor's treatments to them (Fungicide, Growths removal, the radiation painkillers, iodine pills) through `Medical.Treated`;
  - show them: a line on the Character page or a HUD readout, and a notification when one starts or ends.
  - Framework side: `PlayerConditions` only knows the conditions in the game's lists, so it needs a way for mods to register their own (`PlayerConditions.Register`). The Recovery idea in ideas.md would use it too.
