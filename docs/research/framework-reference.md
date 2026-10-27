# Reference notes: the Obenseuer Modding Framework (ObenseuerCore)

Notes on `example-framework/` (NomadsReach, MIT) and what we can lift from it for ObenFramework. Written from reading the source, not from building it. Where I give a number it came from grep or a small script over the tree; where it is a guess I say so.

## 0. Quick facts

- Version 0.12.1-beta, plugin GUID `obenseuer.moddingframework`, verified by its author on game 0.5.08 (Steam build 25969064). The game installed here looks like 0.5.13 (that string is in its `globalgamemanagers`), so every game-facing hook needs re-checking.
- `src/` plus `webui/src/`: 527 C# files, 92,336 lines. Split: `src/*.cs` 54 files / 9,989 lines, `src/Api/` 411 / 69,506, `src/Story/` 27 / 5,527, `src/Debug/` 18 / 2,087, `src/Ui/` 9 / 2,498, `webui/src/` 8 / 2,729.
- Everything else: `preloader/` (417 lines, optional BepInEx patcher), `tools/` (Publicize 91 lines, five test harness exes about 3,100 lines, decompile checker in Python), `webui/web/` (about 1,860 lines of HTML/JS/CSS), `webui/native/ObxWebView/` (C++ WebView2 host, about 3,100 lines including its tests), `Examples/` and `Starter/` (small), `docs/` (667 markdown files, 55k lines).
- Target: net472, `LangVersion` 5 (no `?.`, no string interpolation, no expression-bodied members), built twice for BepInEx 5 and BepInEx 6 via a `BEPINEX5` define. Harmony is whatever BepInEx ships (HarmonyX). We are net472 too, but `LangVersion latest` and plain Lib.Harmony 2.3.6, so ported code compiles but should be modernised as we touch it.
- 95 files are over 300 lines and another 28 are 251 to 300. Mean file size is 175 lines, so most of the tree is already near our limit; the oversized ones are a minority but they are the important ones (Core, SaveData, Lobby, NetBlob, Building, ModUi...).
- Patching: no `PatchAll`, no `[HarmonyPatch]` attributes. Everything goes through `CorePatcher` (478 call sites) with one `Harmony` instance per owner string. One exception, `Debug/CoreDebugTrace.cs`, makes its own `new Harmony(...)`.
- The tree is mostly "one static class per game system": a `Foo.cs` with the public API and events, and a `FooPatches.cs` with the Harmony handlers (106 `*Patches.cs` files). The pattern is regular and easy to port mechanically.
- ObenFramework already exists in this repo as a skeleton (`src/ObenFramework/`, about 1,060 lines: `Events/` with `HandlerList`/`GameEvent`/`SaveHooks`/`SceneHooks`/`ReadyWatcher`, `Console/`, `Reflection/MemberAccess`+`MethodInvoker`+`TypeFinder`, `Scenes/SceneQuery`, `Values/JsonValues`). Its events design (unsubscribe through `kit.Track`) is better for hot reload than `Events.cs` here, so Events should be ported into that shape rather than copied.

Method notes: file and line counts come from `wc -l`. The "private access" figures in section 7 come from matching identifiers against the decompile in `decompiled/Assembly-CSharp/` (I assume it matches the installed game, 0.5.13), so they are heuristics. I also checked the 475 distinct `typeof(X), "member"` string pairs in the framework against the decompile: 465 resolve; the 10 that do not are Harmony handler names, a property getter name and inherited methods, not real drift. So names have been stable between 0.5.08 and 0.5.13; signatures I did not check.

## 1. Module inventory

Module ids are mine, for these notes only. File lists are in the second half of this section; a `*` after a line count marks a file over 300 lines. `Foo` means `src/Api/Foo.cs` unless it has a path prefix (`Story/`, `Ui/`, `Debug/`) or is in `src/` directly (the top-level ones: Core, CorePatcher, CoreLog, CoreInput, Events, GameTime, Loader, ModConfig, ModMenu*, PlayerLook*, SaveData, SaveJson*, Survival*, Tweaks*, Wallet, CameraView, ComputerApps*, ConfigTheme, AccessProbe, FrameworkMod*). `w:` means `webui/src/`.

Counts: "Patch files" is the number of `*Patches.cs` files, "Self-test lines" is lines in files named `*SelfTest.cs` or `*SelfCheck.cs`.

| Id | Module | Files | Lines | Patch files | Self-test lines | What it does | Depends on |
|---|---|---|---|---|---|---|---|
| M01 | bootstrap | 12 | 1,720 | 0 | 85 | `Core` is the BepInEx plugin: binds settings, then runs 197 `Isolate("name", ...)` blocks (135 are plain `X.Init` calls, the rest optional self-checks and probes), plus the `Update` loop that ticks about 20 subsystems through cached `Action` delegates. `FrameworkMod`/`FrameworkMods`/`FrameworkModScan` are its own mod loader (scan plugin dir for `[ModInfo]` classes, order by `[ModDependency]`). `CoreMod` is a per-mod handle (log, save slot, keybind, patch helpers). `Loader` wraps Chainloader. `ModIds` validates ids and file names. | everything (Init order), M02, M03, M09 |
| M02 | patching | 1 | 311 | 0 | 0 | `CorePatcher`: Harmony wrapper. One Harmony per owner id, patch by type + method name + arg types, reports failure, and if a `core` patch fails it unpatches and disables the whole owner. Also `Prefix/Postfix/Finalizer` helpers for mods. | Harmony, M03 |
| M03 | logging | 4 | 363 | 0 | 0 | `CoreLog.For(modId)`: one file per id under `BepInEx/logs/`, line rate limit, repeat suppression, size cap, `Debug(key, msg)` that logs changes only. `ApiLog.Once` logs a failure once per site. | BepInEx `Paths` |
| M04 | config + tweaks | 14 | 1,206 | 0 | 0 | `ModConfig`/`ModSetting<T>` over a per-mod BepInEx `ConfigFile`. `Tweaks*` are optional gameplay tweaks (drain multipliers, stack size, spoilage, craft speed, day length, camera), each a small Harmony patch gated by a setting. `CameraView` is the third-person/first-person toggle. `ConfigTheme` restyles the ConfigurationManager window. | M02, BepInEx config |
| M05 | input + UI state | 8 | 913 | 2 | 46 | `Keybinds`/`ModKeybind`/`KeyChord` (rebindable keys, modifier chords, shown in the Mods menu), `CoreInput` (hold the game's controls while a mod window is open), `InputCapture` (swallow game input), `UiState` (is the game paused, typing, in a menu), `ModInput` (UI input helpers). | M06, M07, game UI singletons |
| M06 | classic mod menu | 15 | 2,963 | 0 | 152 | The uGUI "Mods" window: tab injected into the pause menu, button on the main menu, rows generated from BepInEx config entries, action rows, keyboard/controller navigation (`ModMenuNav`), tooltips, a small JSON writer shared with the web version. | M07, M05, BepInEx config |
| M07 | UI kit | 8 | 2,316 | 0 | 0 | `ModUi` builders (card, button, slider, toggle, stepper, text field, key binding, tabs, swatches), `ModWindow` (docked or floating window with scope/lifetime), `ModelPreview` (render a 3D model into a RawImage), `UiTheme`, `UiSprites`. | M05, TextMeshPro |
| M08 | WebUi | 8 | 2,729 | 0 | 0 | Optional second assembly. Hosts a WebView2 control (native `ObxWebView.dll`) over the game, serves a local page set, bridges messages. Windows only. Also the web version of the Mods menu. | M06, native DLL, BepInEx plugin |
| M09 | save data | 6 | 1,355 | 0 | 148 | `SaveData`: per-mod JSON files in `<save>/mods/`, registered with `Register<T>`/`Bind<T>`; also `Capture`/`Apply` that snapshot and restore any `SavableScript` object by running the game's own save/load code on it. `SaveJson` is a Newtonsoft contract resolver that fills missing fields and handles Unity structs. `SaveGate` knows whether a load or save is in flight. `SaveSlots` lists/saves slots. | game `SaveController`, M10 |
| M10 | events + time | 8 | 865 | 1 | 0 | `Events` (hour/day/scene/save/load/new game, `WhenReady`), `GameTime`, `WorldTime`, `TimeControl` (scale/clock overrides), `WorldLocation` (visited scenes), `ReadySaveGate`/`RetryGate` helpers. | M09 |
| M11 | survival | 6 | 924 | 1 | 0 | `Survival`: read/write 23 player stats (`Stat` enum), threshold-crossing callbacks, per-mod decay multipliers, consume/damage interception, death/pass-out/breakdown events. | M02, M09 |
| M12 | net / lobby | 9 | 1,986 | 0 | 257 | Steam lobby + P2P layer: `Steam`, `SteamBridge`, `Lobby` (host/join/invite, version-checked), `Net` (channel packets, receive budget), `NetChannels`, `NetPump` (one MonoBehaviour tick), `NetBlob` (chunked, gzip, CRC, acked transfer of big payloads), `Presence` (position broadcast). | Steamworks.NET, M03 |
| M13 | debug + dev menu | 21 | 2,353 | 0 | 99 | `CoreDebug*` is a Harmony-based call tracer for framework APIs (arguments, results, timings). `DevMenu*` is an in-game window for quests, flags, no-clip, watches. Both off by default. | M07, M14, M10 |
| M14 | story | 24 | 5,434 | 3 | 493 | `Quests` (read/set/rewind tasks and objectives), `QuestAuthoring` (create quests), `Dialogue*` (variables, interception, response/branch authoring, Lua bridge), `Law` (crimes, fines), `StoryPump` (deferred event queue). | M09, M10 |
| M15 | NPCs and animals | 44 | 8,269 | 8 | 294 | Query NPCs (`Npcs`), spawn mod NPCs (`ModNpcs*`), appearance/blend shapes, movement/animation/control claims, routines and orders, schedules, phone lines/book, animals/breeding, pursuit, robbers, janitors, surveillance. | M09, M16, M17, M22 |
| M16 | player | 42 | 6,974 | 15 | 0 | `PlayerState`, `PlayerMovement` (speed overrides), `PlayerPick`, sleep/wait, `PlayerLook` (capture/apply appearance), skills/custom skills, effects, conditions, medical, seats, interactions, weapons, wardrobe, notes, books, achievements, jobs. | M09, M17 |
| M17 | items + inventory | 42 | 6,713 | 12 | 502 | `Items` catalogue, `PlayerInventory` (give/take/count/equip), `InventorySources` (extra containers that count as inventory), `Containers`, `WorldItems` (floor items by id), `UnloadedStorage*` (edit containers in scenes that are not loaded), loot pools, resource nodes, spawners, breakables, task items. | M09, M18, M23 |
| M18 | item / recipe registry | 11 | 1,675 | 2 | 63 | Define new items and recipes at runtime (`ItemRegistry.Define`, `RecipeRegistry`), manifest on disk so ids stay stable, placeholders for items from removed mods, held models, icons. | M21, M17, M19 |
| M19 | crafting | 25 | 3,092 | 10 | 225 | Crafting start/finish events, stations, quality, speed, upgrades, extra ingredient sources, recipe filter, recycling, dishwashers, growing stations/plants, autofarms. | M17, M09 |
| M20 | building + furniture | 20 | 3,181 | 2 | 198 | `Building` (place/move/remove furniture, blueprints, managers by scene), furniture registry (custom furniture as a copy of a vanilla piece with an OBJ model), ids, loot, shops, pools. | M09, M21, M25b |
| M21 | models + assets | 16 | 2,892 | 3 | 133 | OBJ/MTL parsers, `ModelBuild` (mesh/material/collider from a file), `ModelRegistry`, `Sounds`/`SoundsWav`, `Localization` bridge, instruments, radios, televisions. | M09, M17 |
| M22 | world props + SceneLook | 41 | 9,596 | 3 | 612 | `WorldProps` (spawn primitive/model props that persist in the save), `SceneLook*` (read, index and re-apply character looks from scene files including rigs and blend shapes; 25 files, the biggest cluster), `SceneState`/`SceneLogic` (state machines, counters, relays), landmarks, signs, `WorldObjects`. | M09, M21, M15 |
| M23 | shops + economy | 32 | 6,273 | 7 | 41 | `Shops`, `ShopStock` (add stock to traders/vending), `Traders`, `BaseShops`, `ShopsPlayer*` (player-run shops), `Cash` (+ `Wallet`), `Bank*` (accounts, ledger, online banking), `Mail*`, invoices, legal services, train tickets, fuel stations. | M17, M09, M14 |
| M24 | doors + transport | 20 | 2,873 | 8 | 0 | Doors/schedules, locks, levels/travel, elevators, ladders, tarp doors, trains/train actions, drones, wait stations. | M09, M16 |
| M25 | world environment | 40 | 5,490 | 16 | 109 | Weather and zones, sky overrides (`SkyApply`/`SkyChannel`), water, radiation, lights/lanterns, hazards, surfaces, tanks, flood gates, power devices, showers, saunas, `GlobalFlags`, `GameSettings`, screen FX. | M09, M17 |
| M25b | housing + property | 10 | 1,811 | 4 | 0 | Tenement/apartments, tenants, hotels, renovations, parcel lockers, mailboxes. | M09, M23, M20 |
| M26 | HUD | 9 | 2,273 | 0 | 172 | `Hud` (notify, subtitle), `HudHints` (hint boxes and prompts, queue, panel, optionally using the game's own tutorial UI), `HudBars`. | M07, M05 |
| M27 | gambling | 7 | 1,403 | 4 | 0 | Casino machines, card games, drinking contests: events and payout scaling (one transpiler in `CardGamesPatches`). | M23 |
| M28 | difficulty + new game | 15 | 2,473 | 3 | 190 | Difficulty read/override, mod difficulty presets, options on the New Game screen (`NewGameOptions*` 516-line screen builder), start choices. | M06, M09 |
| M29 | main menu + console | 3 | 736 | 1 | 0 | `MainMenuButtons` (add buttons to the main menu), `ConsoleCommands` (register commands in the game's developer console), `BuildingLoaded`. | M07 |
| M29b | computer apps | 4 | 1,003 | 0 | 0 | Register apps on the in-game computer desk, standalone app window. | M07 |
| M29c | tutorial | 2 | 171 | 1 | 0 | `TutorialGuide` step events. | none |

The dependency column is rough: I counted references to each module's class names from every other module. Two things stand out. Nearly every gameplay module references `SaveData` (to persist what it adds), and almost nothing outside M01 references M04 or M06 except the menu itself. That means the gameplay modules can be ported without the menu, which is the order I would use.

### 1.1 File lists

Lines per file; `*` = over 300. Webui files are prefixed `w:`.

**M01 bootstrap** (12 files, 1720 lines)

AccessProbe 49, CoreMod 119, CoreModSelfTest 48, ModIds 69, ModIdsSelfTest 37, ObjectPath 76, Core 714*, FrameworkMod 82, FrameworkModScan 322*, FrameworkMods 135, Loader 64, LoaderNamespaces 5

**M02 patching** (1 files, 311 lines)

CorePatcher 311*

**M03 logging** (4 files, 363 lines)

ApiLog 22, HudReport 10, CoreLog 314*, Story/StoryLog 17

**M04 config+tweaks** (14 files, 1206 lines)

HudHintSettings 26, CameraView 258, ConfigTheme 261, ModConfig 63, TweakState 10, Tweaks 135, TweaksCamera 42, TweaksCrafting 89, TweaksDishwasher 31, TweaksNeeds 25, TweaksRenovations 173, TweaksSpoilage 32, TweaksStacks 31, TweaksTime 30

**M05 input+ui state** (8 files, 913 lines)

InputCapture 110, InputCapturePatches 43, KeyChordSelfTest 46, Keybinds 229, UiState 101, UiStatePatches 59, CoreInput 143, Ui/ModInput 182

**M06 classic mod menu** (15 files, 2963 lines)

ModMenu 73, ModMenuActionRow 108, ModMenuActions 204, ModMenuConfigList 354*, ModMenuInput 50, ModMenuJson 133, ModMenuMainMenu 39, ModMenuNav 659*, ModMenuRow 286, ModMenuRowValues 102, ModMenuSelfCheck 152, ModMenuTab 312*, ModMenuTooltip 99, ModMenuWidgets 149, ModMenuWindow 243

**M07 ui kit** (8 files, 2316 lines)

Ui/ModControls 270, Ui/ModPickers 118, Ui/ModTabs 127, Ui/ModUi 601*, Ui/ModWindow 541*, Ui/ModelPreview 491*, Ui/UiSprites 63, Ui/UiTheme 105

**M08 webui** (8 files, 2729 lines)

w:ModMenuWebConfig 466*, w:ModMenuWebHost 627*, w:ModMenuWebKeys 74, w:ModMenuWebModel 229, w:WebUi 598*, w:WebUiNative 265, w:WebUiPlugin 84, w:WebView 386*

**M09 save data** (6 files, 1355 lines)

SaveGate 83, SaveSlot 39, SaveSlots 189, SaveData 644*, SaveJson 252, SaveJsonSelfTest 148

**M10 events+time** (8 files, 865 lines)

TimeControl 202, TimeControlPatches 33, WorldLocation 175, WorldTime 120, Events 203, GameTime 56, Story/ReadySaveGate 17, Story/RetryGate 59

**M11 survival** (6 files, 924 lines)

Survival 223, SurvivalConsumables 86, SurvivalDamage 81, SurvivalDecay 77, SurvivalPatches 351*, SurvivalStat 106

**M12 net/lobby** (9 files, 1986 lines)

Lobby 518*, Net 215, NetBlob 486*, NetBlobSelfTest 257, NetChannels 57, NetPump 104, Presence 125, Steam 65, SteamBridge 159

**M13 debug+devmenu** (21 files, 2353 lines)

DevMenu 127, DevMenuRules 90, DevMenuSelfTest 49, Debug/CoreDebug 117, Debug/CoreDebugEvents 97, Debug/CoreDebugFormat 114, Debug/CoreDebugLimiter 49, Debug/CoreDebugSelfTest 50, Debug/CoreDebugTargets 84, Debug/CoreDebugTrace 368*, Debug/CoreDebugUnity 22, Debug/DevMenuFlag 59, Debug/DevMenuHost 20, Debug/DevMenuMods 69, Debug/DevMenuNoClip 135, Debug/DevMenuQuests 125, Debug/DevMenuState 243, Debug/DevMenuTools 158, Debug/DevMenuWatch 88, Debug/DevMenuWindow 51, Debug/DevMenuWorld 238

**M14 story** (24 files, 5434 lines)

Story/Dialogue 544*, Story/DialogueAuthoring 395*, Story/DialogueAuthoringSelfTest 56, Story/DialogueBranches 366*, Story/DialogueLuaBridge 145, Story/DialoguePatches 69, Story/DialogueResponses 325*, Story/DialogueResponsesSelfTest 58, Story/Law 468*, Story/LawPatches 133, Story/QuestAuthoring 548*, Story/QuestAuthoringSelfTest 163, Story/QuestDebugCommand 26, Story/QuestFollowUp 79, Story/QuestFollowUpPlan 33, Story/QuestRegistry 267, Story/QuestRewind 613*, Story/QuestRewindPlan 145, Story/QuestRewindSelfTest 124, Story/Quests 444*, Story/QuestsGateSelfTest 65, Story/QuestsPatches 225, Story/QuestsSelfTest 27, Story/StoryPump 116

**M15 npcs** (44 files, 8269 lines)

AnimalActions 354*, AnimalActionsSelfTest 53, AnimalTameSave 107, Breeding 165, BreedingPatches 89, Characters 142, Janitors 106, JanitorsPatches 72, ModNpcExtras 249, ModNpcRoutines 337*, ModNpcRoutinesOrders 481*, ModNpcs 650*, ModNpcsLook 100, ModNpcsSaved 196, ModNpcsSelfTest 118, NpcActions 299, NpcAnimation 418*, NpcAppearance 303*, NpcAppearanceShape 343*, NpcControl 487*, NpcMovement 275, NpcRoutine 155, NpcRoutineSelfTest 80, NpcRoutineTable 73, NpcSchedules 158, NpcSchedulesPatches 41, NpcWalkMath 70, Npcs 238, NpcsAnimals 175, NpcsPatches 140, NpcsPhones 60, PhoneBook 145, PhoneLines 293, PhoneLinesPatches 83, PhoneRules 72, PhoneRulesSelfTest 43, PhonesAdd 136, Pursuit 191, PursuitPatches 43, Relationships 155, Robbers 170, RobbersPatches 33, Surveillance 302*, SurveillancePatches 69

**M16 player** (42 files, 6974 lines)

Achievements 155, Books 115, BooksPatches 50, Cleaning 109, Conditions 231, ConditionsPatches 60, CustomSkills 232, CustomSkillsMath 228, CustomSkillsPatches 146, Effects 323*, Interactions 209, InteractionsPatches 58, Jobs 135, JobsPatches 48, Medical 169, MedicalPatches 47, MedicalTreatments 109, Notes 180, NotesPatches 48, PlantActions 188, Player 349*, PlayerMovement 574*, PlayerMovementPatches 51, PlayerPatches 106, PlayerPick 254, PlayerPickPatches 80, PlayerSleep 139, PlayerSubscriptions 107, PlayerWait 111, PlayerWaitMath 48, PlayerWaitPatches 151, Seats 191, SeatsPatches 81, Skills 199, SleepEvents 139, SleepEventsPatches 53, Wardrobe 237, WardrobePatches 110, Weapons 389*, WeaponsPatches 165, PlayerLook 322*, PlayerLookTable 278

**M17 items+inventory** (42 files, 6713 lines)

Breakables 287, BreakablesPatches 150, Containers 259, ContainersPatches 64, Extractables 137, ExtractablesPatches 38, FloorItems 101, FloorItemsSelfTest 47, FuelTypes 66, HudTasks 163, Inventory 461*, InventoryPatches 81, InventorySelfTest 33, InventorySources 323*, InventorySourcesPatches 197, InventorySourcesSelfTest 253, InventoryStack 91, InventoryTakeMath 35, ItemConsumables 385*, ItemGivers 150, ItemGiversPatches 33, Items 293, LootPools 385*, LootPoolsPatches 76, ResourceNodes 211, ResourceNodesPatches 42, Spawn 47, Spawners 229, SpawnersPatches 33, StackActions 142, StoragePanel 206, StoragePanelPatches 32, StoragePanelSelfTest 28, TaskItemsPatches 142, UnloadedStorage 278, UnloadedStorageMath 110, UnloadedStorageReader 159, UnloadedStorageScene 191, UnloadedStorageSelfTest 141, UnloadedStorageTypes 80, WorldItems 324*, WorldItemsPatches 210

**M18 item/recipe registry** (11 files, 1675 lines)

ModelIcons 185, ModelIconsPatches 46, RegistryHeld 138, RegistryIds 55, RegistryItems 394*, RegistryManifest 100, RegistryMissing 108, RegistryPatches 162, RegistryPlaceholders 78, RegistryRecipes 346*, RegistrySelfTest 63

**M19 crafting** (25 files, 3092 lines)

Autofarms 172, AutofarmsPatches 40, Crafting 173, CraftingPatches 56, CraftingQuality 71, CraftingQualityPatches 55, CraftingSources 468*, CraftingSourcesPatches 134, CraftingSourcesSelfTest 196, CraftingSpeed 69, CraftingSpeedPatches 40, CraftingStations 168, CraftingUpgrades 108, Dishwashers 169, DishwashersPatches 35, GrowingPatches 71, GrowingPlants 226, GrowingStations 135, RecipeFilter 82, RecipeFilterPatches 93, RecipeFilterSelfTest 29, Recipes 311*, RecipesPatches 33, Recycling 109, RecyclingPatches 49

**M20 building+furniture** (20 files, 3181 lines)

Building 608*, BuildingAreas 67, BuildingAreasPatches 59, BuildingCatalog 132, BuildingLoaded 28, FurnitureBlueprints 165, FurnitureBuild 418*, FurnitureCategories 85, FurnitureConsole 56, FurnitureFlatpacks 40, FurnitureIds 150, FurnitureIdsSelfTest 89, FurnitureLoot 201, FurnitureManifest 73, FurniturePatches 239, FurniturePlaceableInfo 40, FurniturePools 239, FurnitureRegistry 318*, FurnitureSelfTest 109, FurnitureShops 65

**M21 models+assets** (16 files, 2892 lines)

GuitarSong 49, GuitarSongSelfTest 40, Instruments 379*, InstrumentsPatches 105, Localization 82, ModelBuild 568*, ModelMtl 223, ModelObj 268, ModelPatches 79, ModelRegistry 302*, ModelSelfTest 93, Radios 125, Sounds 307*, SoundsWav 78, Televisions 143, TelevisionsPatches 51

**M22 world props+scene look** (41 files, 9596 lines)

Landmarks 189, LandmarksCustom 566*, LandmarksPatches 82, SceneIds 85, SceneLogic 173, SceneLogicPatches 46, SceneLookApply 393*, SceneLookApplyLive 132, SceneLookApplyMesh 171, SceneLookApplyRig 624*, SceneLookApplyTexture 170, SceneLookBind 286, SceneLookCursor 150, SceneLookData 339*, SceneLookFile 245, SceneLookFiles 85, SceneLookFrame 186, SceneLookIndex 167, SceneLookInfo 35, SceneLookLivePlan 43, SceneLookLiveSelfTest 37, SceneLookLoad 129, SceneLookMeshPlan 94, SceneLookReader 407*, SceneLookReaderMesh 484*, SceneLookRig 329*, SceneLookRigView 81, SceneLookScan 396*, SceneLooks 618*, SceneLooksConsole 127, SceneLooksMenu 15, SceneState 332*, Signs 158, SignsPatches 46, WorldObjects 264, WorldProps 469*, WorldPropsBuild 347*, WorldPropsFile 192, WorldPropsScene 107, WorldPropsSelfTest 575*, WorldPropsTypes 222

**M23 shops+economy** (32 files, 6273 lines)

Bank 374*, BankExchangeMath 28, BankLedger 377*, BankingOnline 303*, BaseShops 408*, FuelStations 121, FuelStationsPatches 64, Invoices 240, InvoicesPatches 97, LawActions 101, LegalServices 284, Mail 382*, MailSchedules 101, MarketShops 109, MarketShopsPatches 38, Money 302*, PayoutFactors 94, ShopHours 112, ShopStock 460*, ShopStockPatches 94, Shops 328*, ShopsPatches 222, ShopsPlayer 413*, ShopsPlayerMath 43, ShopsPlayerMathSelfTest 41, ShopsPlayerPatches 55, ShopsPlayerProbe 40, ShopsPlayerState 248, Traders 555*, TrainTickets 124, TrainTicketsPatches 58, Wallet 57

**M24 doors+transport** (20 files, 2873 lines)

DoorSchedules 106, Doors 180, DoorsPatches 52, Drones 250, DronesPatches 69, Elevators 163, ElevatorsPatches 43, Ladders 104, Levels 206, LevelsPatches 33, Locks 386*, LocksPatches 125, TarpDoors 150, TarpDoorsPatches 41, TrainActions 177, Trains 387*, TrainsPatches 77, Travel 149, WaitStations 142, WaitStationsPatches 33

**M25 world environment** (40 files, 5490 lines)

BlastSites 132, BlastSitesPatches 46, FloodGates 195, FloodGatesPatches 46, GameSettings 249, GameSettingsPatches 42, GlobalFlags 131, GlobalFlagsPatches 58, Hazards 187, HazardsPatches 76, Lanterns 106, LanternsPatches 39, Lights 241, LightsPatches 71, PowerDevices 329*, PowerDevicesPatches 150, Saunas 148, SaunasPatches 54, ScreenFx 263, ScreenFxBlur 217, ScreenFxPatches 81, ScreenFxVignette 191, Showers 154, ShowersPatches 42, SkyApply 292, SkyChannel 75, SkyChannelSelfTest 109, Surfaces 156, SurfacesPatches 63, Tanks 187, WorldPatches 85, WorldPower 108, WorldRadiation 136, WorldRadiationPatches 33, WorldSky 281, WorldSkyPatches 69, WorldWater 187, WorldWaterPatches 64, WorldWeather 185, WorldWeatherZones 212

**M25b housing+property** (10 files, 1811 lines)

Hotels 220, HotelsPatches 59, Housing 327*, HousingPatches 263, Mailboxes 163, MailboxesPatches 47, ParcelLockers 162, Renovations 326*, RenovationsPatches 33, Tenants 211

**M26 hud** (9 files, 2273 lines)

Hud 436*, HudBars 200, HudHintGame 368*, HudHintLogic 98, HudHintPanel 277, HudHintQueue 166, HudHintSource 249, HudHints 307*, HudHintsSelfTest 172

**M27 gambling** (7 files, 1403 lines)

CardGames 242, CardGamesPatches 164, Casino 301*, CasinoPatches 241, Contests 284, ContestsPatches 137, ContestsPayoutPatches 34

**M28 difficulty+new game** (15 files, 2473 lines)

Difficulty 222, DifficultyModPresets 31, DifficultyModPresetsPatches 194, DifficultyPatches 121, DifficultyPresetMap 53, DifficultyPresetSelfTest 68, DifficultyPresets 378*, NewGameInfo 92, NewGameOptionStore 321*, NewGameOptions 180, NewGameOptionsScreen 516*, NewGameOptionsSelfTest 122, NewGamePlacement 8, StartChoices 134, StartChoicesPatches 33

**M29 main menu+console** (3 files, 736 lines)

ConsoleCommands 137, ConsoleCommandsPatches 82, MainMenuButtons 517*

**M29b computer apps** (4 files, 1003 lines)

ComputerDesk 38, ComputerFiles 142, ComputerApps 411*, ComputerAppsDesk 412*

**M29c tutorial** (2 files, 171 lines)

TutorialGuide 136, TutorialGuidePatches 35

**M99 other** (0 files, 0 lines)



## 2. Coupling

### 2.1 BepInEx

It is shallow. 44 of 527 files mention BepInEx, on 94 lines in total. Zero of the 106 `*Patches.cs` files and none of the gameplay modules (M14 to M29c) touch it. What does:

| Where | Files | What is used |
|---|---|---|
| Plugin shell | `Core.cs`, `webui/WebUiPlugin.cs` | `BaseUnityPlugin`, `[BepInPlugin]`, `[BepInDependency]`, `Config.Bind` (49 calls in `Core.Awake`, 42 of them debug self-check toggles), `Info.Metadata.Version`, `Logger.LogInfo` |
| Plugin discovery | `Loader.cs`, `LoaderNamespaces.cs`, `FrameworkMods.cs`, `FrameworkModScan.cs`, `Api/CoreMod.cs` | `Chainloader.PluginInfos` / `UnityChainloader.Instance.Plugins`, `PluginInfo`, `Paths.PluginPath` scan, `Paths.BepInExVersion`, `CoreMod.For(BaseUnityPlugin)`. `LoaderNamespaces.cs` is 5 lines of empty namespaces so BepInEx 5 compiles `using BepInEx.Unity.Mono` |
| Paths | `CoreLog.cs`, `ModConfig.cs`, `Api/Keybinds.cs`, `Api/LandmarksCustom.cs`, `Debug/DevMenuTools.cs` | `Paths.BepInExRootPath` (logs), `Paths.ConfigPath` (cfg), `Paths.PluginPath` (icon sandbox in `LandmarksCustom`) |
| Config entries | `ModConfig.cs`, `Tweaks*.cs` (9), `Api/HudHintSettings.cs`, `Api/DevMenu.cs`, `ModMenuWindow.cs`, `Api/NewGameOptionsScreen.cs`, `Api/DifficultyModPresetsPatches.cs`, `Debug/CoreDebug*.cs`, `Api/Keybinds.cs` | `ConfigFile`, `ConfigEntry<T>`, `ConfigDescription`, `AcceptableValueRange<T>`, `SettingChanged` |
| Config UI | `ModMenuConfigList.cs` (28 references), `ModMenuRow.cs`, `ModMenuRowValues.cs`, `ModMenuActionRow.cs`, `ModMenuSelfCheck.cs`, `Ui/ModControls.cs`, `Ui/ModUi.cs`, `webui/ModMenuWeb*.cs` | `ConfigEntryBase`, `AcceptableValueList`/`Range` through reflection, `KeyboardShortcut` (BepInEx's chord type, used as the data type of keybinds) |
| Tooling | `ConfigTheme.cs` | reflects into the third-party ConfigurationManager plugin to restyle it. Useless to us |
| Version string | `Api/Net.cs` | `CoreVersion` reads the `[BepInPlugin]` attribute off `Core` |

Replacements on our side: `ObenMod.Log`/`LogHub` for CoreLog's sink (keep CoreLog's rate-limit and `Debug(key, msg)` change-only logic, it is worth having), `ObenMod.LoadConfig<T>()` for `ConfigFile`, `Info.Id`/`Info.Directory` for `Paths.*`, `ModRegistry` for `Chainloader` and the whole of `FrameworkMods`. `KeyboardShortcut` needs a small `KeyChord` struct of our own (`Api/Keybinds.cs` already has a `KeyChord` class with the overlap/match logic, which is the part to keep).

### 2.2 Reliance on private game members

Short answer: lower than the publicizer's presence suggests. Details in section 7; the facts that matter here:

- The framework reaches private game fields mostly through `AccessTools.Field(...)` held in `static readonly FieldInfo` fields: 159 `AccessTools.*` calls (149 `Field`, 9 `Method`, 1 `PropertyGetter`) plus 79 `GetField/GetMethod/GetProperty` calls, 169 of which are in `static readonly` initialisers and most of the rest are lazy-cached in an `Init`. About 244 `GetValue/SetValue` call sites use them.
- 112 Harmony `___field` injected parameters in 21 files (e.g. `CasinoPatches.cs` 32, `FurniturePools.cs` 14, `TrainTicketsPatches.cs` 10, `HazardsPatches.cs` 10). These need no publicizer and no reflection at runtime; Harmony generates the access.
- Compile-time access to a non-public member is close to zero. The only place I could prove it is `AccessProbe.cs` (`SaveController.SaveName` and `SaveController.NormalizeFolderPath`), which exists to test whether the publicizer worked, and is off by default.

Concrete private accesses, by module:

| Module | Examples (all are private in the 0.5.13 decompile) |
|---|---|
| M04 | `CameraView.cs`: `ThirdPersonCameraCollision.minDistance`, `.maxDistance`, `.disabledZoomList` via `AccessTools.Field` |
| M05 | `CoreInput.cs`: `ThirdPersonCameraController.moveDisabled`, `.firstpersonMoveDisabled` |
| M06 | `ModMenuTab.cs`: `UIPageButtons.MenuPages` through `ModMenuReflect.Get/Set`, which does a `GetField` on every call (not cached) |
| M09 | `SaveData.cs`: `SaveController.CharacterName`, `.SaveName`, `.tempSavedata_Global`, `.tempSavedata_Level`, nested type `SaveController.ObjectDataHeader` (`GetNestedType` with `NonPublic`) |
| M11 | `SurvivalPatches.cs`: `AccessTools.Method(typeof(CollectibleItem), "GetOrCreateItemData")` and `"CanDrinkLiquid"` |
| M12 | `SteamBridge.cs`: `SteamManager.m_bInitialized` (read until true, then cached) |
| M15 | `Npcs.cs`/`NpcControl.cs`: `FadeGameObject.culled`; `AnimalActions.cs`: `Animal.currentState`; `Spawners.cs`: `Spawner.objects` |
| M16 | `PlayerMovement.cs`: 13 `FieldInfo`s on `OpenSewerCharacterController` (`_runSpeed`, `_walkSpeed`, `_swimmingSpeed`, `_crouchSpeed`, ...) and `ThirdPersonCameraController`; `Seats.cs`: `InteractableChair.disableStopSit`, `.currentCoroutine`; `PlayerLook.cs`: `Appearance.bodyModels` |
| M19 | `GrowingPatches.cs`/`GrowingPlants.cs`: `GrowItem.grow`, `.item`, `.itemAmount`, `.fullGrownAgeFinal`; `Dishwashers.cs`: `manufacturing.craftsInProgresses` |
| M20 | `Building.cs`: `FurniturePlaceable.trash` |
| M23 | `ShopsPatches.cs`: `Trade.storage`, `Trade.GetCurrency`, `PlayerShopManager.PlayerShopData.uncollectedEarnings`; `Hotels.cs`: `Hotel.dailyPricefactor`; `FuelStations.cs`: `GasStation.currentFuelPrice`, `FuelPump.filledAmount` |
| M24 | `TarpDoors.cs`: `TarpDoor.opened`; `ElevatorsPatches.cs`: `___moving` |
| M25 | `Showers.cs`: `InteractableShower.showerOn` (protected), `.waterOn`; `HazardsPatches.cs`: `___playerIn` |
| M26 | `HudHintGame.cs`: `InputManager.keysdefault`, `Tutorial.ShowTutorialButton`, `Tutorial.ClearPrompts` |
| M27 | `CasinoPatches.cs`: `___stakes`, `____currentRunEarnings`; `CardGamesPatches.cs`: a compiler-generated iterator (`<>4__this`) is transpiled |

Two naming gotchas: a Harmony `____x` (four underscores) is a private field literally named `_x`, and `<>4__this` style names are compiler artefacts that break on any game rebuild. Both are in this tree; port them as-is only after checking the names in the current decompile.

### 2.3 Harmony patch setup

- `CorePatcher` is the only entry. Callers pass `(Type, "Method", Type[] args, handler Type, "HandlerName", owner)`. It resolves the target with `GetMethod(name, Instance|Static|Public|NonPublic, args)` and the handler with `GetMethod` on the handler type, once, at install. No per-frame reflection.
- `owner` is a string such as `obenseuer.moddingframework.survival.needs`. Each distinct owner gets its own `new Harmony("core:" + owner)`. Public callers (mods) cannot use an owner starting with the framework's id.
- Failure policy: every framework patch is installed with `core: true`. If one patch in an owner fails (method missing or Harmony throws), `DisableOwner` calls `UnpatchSelf()` on that owner's Harmony and flags it, so a half-installed feature never runs. Each `Foo.Init` then reports `IsAvailable == false` and every `Foo.*` method returns false/empty with one logged line. This is the best idea in the codebase and we should keep it.
- Unsupported game build: `Core.PatchingAllowed` gates every patch, optional hard refusal via `[Safety] RefuseUnknownBuild`.
- Handlers wrap their bodies in `try/catch` and log once per site through `ApiLog.Once`; events go through `Events.Raise(handlers, name, invoke)` which isolates each subscriber.
- One transpiler (`CardGamesPatches.PayoutTranspiler`, rewrites `Mathf.RoundToInt` sites in an iterator `MoveNext`). No `PatchAll`, no attribute patches.
- `Tweaks.Patch` and `CameraView` call `CorePatcher` with their own owner ids; `CoreDebugTrace` builds its own `Harmony` and patches arbitrary framework methods at runtime for the trace log.

Hot reload: nothing here unpatches on unload except `DisableOwner`. There is no global teardown anywhere in the tree (only 40 mentions of `OnDestroy`, `Unpatch` or `Reset()` in total, none of them a shutdown path I could find). There are 207 static collections and 16 `DontDestroyOnLoad` hosts, and `Events.Init` subscribes to game events and never unsubscribes. A hot-reloadable ObenFramework needs `CorePatcher` to return a disposable per owner (or to take the `ObenMod` and use `ObenMod.Harmony`), and every `Init` needs a matching teardown through `Track()`.

## 3. Self-tests

What they are: 37 files named `*SelfTest.cs` or `*SelfCheck.cs` (3,819 lines), another 16 files with an in-class `SelfCheck()`/`SelfTest()` method (e.g. `BankExchangeMath`, `PlayerWaitMath`, `ItemConsumables`, `CustomSkillsMath`, `ObjectPath`, `WorldItems.PathIdSelfCheck`, `NpcWalkMathCheck`, `MailCheck`, `PlayerLookCheck`, `SaveData.SelfCheck`). Each is an `internal static string Run()` that returns `"... self-check ok"` or a one-line failure reason. There is no test framework.

How they run:

1. In game: `Core.Awake` binds 42 `[Debug] <Name>SelfCheck` booleans (default false). If one is true, the check runs at start and the result goes to the framework log. Two are console commands instead (`core_savedata_check`, `core_worldprops_check`) because they change the live save and say so.
2. Off game: `tools/NpcHarness`, `ModelHarness`, `FurnitureHarness`, `SceneLookHarness`, `DebugTraceHarness` are console exes that `Compile Include` a hand-picked set of pure-logic source files (e.g. `Api/ModIds.cs`, `Api/PhoneRules.cs`, `Api/InventoryTakeMath.cs`) with small stubs (`tools/FrameworkModsStub.cs`, `NpcHarness/ApiLogStub.cs`), call the `Run()` methods and return exit code 0/1. `NpcHarness/Program.cs` chains ten checks. CI (`.github/workflows/docs.yml`) only runs the doc link check and `gen-events.py --check`; nothing builds the code or runs a test.
3. `tools/decompcheck/` (Python, about 1,760 lines) parses a decompile and verifies every `AccessTools.*`, `GetField("...")`, `[HarmonyPatch]` and `CorePatcher` target name in the code, plus the docs, resolves. This one is more valuable than any in-game check: it is what catches game updates.

Worth keeping? Split by kind:

- Keep, and move to a real test project (xUnit or plain console, our choice): the 33 files that do not reference `GameObject`, `SaveController` or Harmony. They test logic that we will port anyway: `NetBlobSelfTest` (CRC32 known vector, gzip round trip, chunking, damaged chunk rejected, packet size limit), `ModIdsSelfTest`, `InventorySelfTest`/`InventoryTakeMath`, `UnloadedStorageSelfTest`, `QuestRewindSelfTest`, `QuestAuthoringSelfTest`, `DifficultyPresetSelfTest`, `NewGameOptionsSelfTest`, `ModNpcsSelfTest`, `NpcRoutineSelfTest`, `KeyChordSelfTest`, `HudHintsSelfTest`, `SkyChannelSelfTest`, `ShopsPlayerMathSelfTest`, `PhoneRulesSelfTest`, `FurnitureIdsSelfTest`. Converting them is mechanical (turn `return "reason"` into an assertion). They only compile against the matching production files, so they come along when each module is ported.
- Drop: the runtime `[Debug] *SelfCheck` toggles. 42 config entries that run a test at game start do not belong in a shipped mod, and they take up about 350 lines of `Core.Awake`. If we want in-game checks, one console command that runs a registered list is enough.
- Rewrite: the four that build Unity objects or call the save controller (`SaveJsonSelfTest`, `InventorySourcesSelfTest`, `FloorItemsSelfTest`, and `WorldPropsSelfTest`, which at 575 lines is the largest) and the ones that need a live game (`DevMenuSelfTest`, `StoragePanelSelfTest`, `CoreModSelfTest`, `ModMenuSelfCheck`). Not worth porting as they are; fold into a DevBridge-driven smoke test later.
- Keep as is: `tools/decompcheck`, adapted to our paths and our `decompiled/` folder. `tools/check-names.sh` is also worth taking: it fails when a public framework type shares a name with a game type, which matters because the game's types live in the global namespace and beat a `using` import.

## 4. Public API surface seen by a mod author

Namespace `ObenseuerCore`, everything static. Convention across the board: every area has `IsAvailable`; calls return `bool`/null on refusal and log one line instead of throwing; registrations take a `modId` so `RemoveAll(modId)` can clean up. About 100 areas; these are the 40 I would call the core of it.

| # | Member | What it does |
|---|---|---|
| 1 | `CoreMod.For(plugin)` -> `Save<T>`, `Key`, `Prefix/Postfix/Finalizer`, `Log` | per-mod handle bundling log, save, keybind and patch helpers |
| 2 | `CoreLog.For(modId)` -> `Info/Warn/Error/Debug(key, msg)` | per-mod log file with rate limit and change-only debug |
| 3 | `CorePatcher.Patch/Prefix/Postfix/Finalizer(type, name, args, handlerType, handlerName, owner)` | safe Harmony patching, owner-scoped disable |
| 4 | `Events.HourPassed`, `DayPassed`, `SceneLoaded` | time and scene events |
| 5 | `Events.BeforeSave`, `AfterSave`, `Saved`, `AfterLoad`, `Loaded`, `NewGameStarted(Info)` | save lifecycle events |
| 6 | `Events.WhenReady(Action)` | run now if a save is loaded, else once when it is |
| 7 | `SaveData.Register<T>(modId, onSave, onLoad)` / `Bind<T>(modId, key)` -> `SaveSlot<T>` | per-save JSON file for a mod |
| 8 | `SaveData.Capture(obj)` / `Apply(obj, json)` / `CaptureByGuid` / `ApplyByGuid` | snapshot or restore a game object through its own save code |
| 9 | `SaveData.Busy`, `Folder`, `Peek<T>` | is a load/save running, current save folder, read without registering |
| 10 | `SaveSlots.CurrentName`, `CanSaveNow`, `SaveNow()`, `All()` | save slot info and trigger a save |
| 11 | `ModConfig.Bind<T>(section, key, default, desc[, min, max])` -> `ModSetting<T>` (`Value`, `Changed`) | typed settings (BepInEx-backed) |
| 12 | `Keybinds.Register(modId, id, name, defaultKey)` -> `ModKeybind` (`Down/Up/Held/Key`), `Keybinds.Down(action)` | rebindable keys shown in the Mods menu |
| 13 | `ModMenu.AddPage(modId, title, Action<RectTransform>)`, `WidgetsFor` | a settings page in the Mods window |
| 14 | `ModUi.Card/Button/Slider/Toggle/Stepper/TextField/KeyBinding/Tabs/Segmented/Swatches/ModelPreview` | uGUI builders in the game's style |
| 15 | `ModWindow.Open(modId, title, dock, build)`, `Closed`, `Find` | docked or floating mod window |
| 16 | `MainMenuButtons.Add(modId, id, label, onClick)`, `SetControlsEnabled` | main menu button |
| 17 | `ConsoleCommands.Register(modId, name, help, run)`, `Print`, `Open` | commands in the game's dev console |
| 18 | `GameTime.Day/Hour/Minute/IsDaytime/IsPaused`, `WorldTime.Now`, `SetTimeOfDay`, `SetDate` | game clock |
| 19 | `Wallet.Balance/Has/TrySpend/Give` and `Cash.Balance/Give/TrySpend/Oc/Rm`, `MoneyChanged` | money in the game's two currencies |
| 20 | `Survival.Get/Set(Stat)`, `OnCrossed(modId, stat, threshold, cb)`, `RemoveAll(modId)` | player stats and threshold callbacks |
| 21 | `Survival.Decay.Set(stat, modId, mult, offsetPerHour)`, `InterceptDamage`, `Consumables.Intercept` | change drain rates, veto damage or consumption |
| 22 | `Survival.PlayerDied`, `PassedOut`, `Damaged`, `Breakdown` | player state events |
| 23 | `PlayerState.Position/Rotation/Health/IsAlive`, `Damage`, `Heal`, `GodMode`, `NeedsFrozen` | local player |
| 24 | `PlayerInventory.Give/Take/Count/Has/Items/FreeSlots/EquipHeld`, `ItemAdded`, `ItemRemoved` | inventory |
| 25 | `Items.Get/Find/Search/InCategory/Icon`, `ItemRegistry.Define/IdFor` | item catalogue and runtime definitions |
| 26 | `Containers.All/FindByGuid/GetItems/Give/Take`, `Opened`, `Closed` | containers in loaded scenes |
| 27 | `WorldItems.IdOf/Find/Remove`, `PickedUp`, `Dropped`, `Used` | floor items by stable id |
| 28 | `Npcs.All/Get/Find/InScene`, `Spawned`, `Despawned`, `Died`; `ModNpcs.*` | NPC queries and mod NPCs |
| 29 | `Quests.All/Get/State/Counter/SetTask`, `TaskChanged`, `ObjectiveChanged`, `Rewound`; `QuestAuthoring.Create` | quest read/write/author |
| 30 | `Dialogue.Started/Ended`, `GetVariable*/SetVariable`; `DialogueAuthoring`, `DialogueResponses`, `Law` | conversations and crime |
| 31 | `Shops.Open/Stock/PriceOf`, `Bought`, `Sold`, `PriceQuery`; `ShopStock.AddTraderStock/AddVendingEntry/Remove/Place` | shops and stock |
| 32 | `Building.Place/Move/Remove/Find/GetPlacedFurniture/Managers`, `FurniturePlaced/Removed/Loaded` | furniture |
| 33 | `Hud.Notify/ShowSubtitle/ShowHint/ShowPrompt` | on-screen text |
| 34 | `UiState.IsPaused/IsMenuOpen/IsInputBlocked/IsTyping/CursorVisible`, `PauseOpened` | what the UI is doing |
| 35 | `PlayerLook.Capture/Apply/Live/Randomise/Options`, `Changed` | character appearance |
| 36 | `GlobalFlags.Get/Set`, `Doors.Open/Close/IsOpen`, `Locks`, `Levels`, `Trains`, `Elevators` | world switches and transport |
| 37 | `Lobby.Host/Join/Leave/Invite`, `Status`, `Members`, `Owner`, `IsHost`, `MemberJoined/Left` | Steam lobby |
| 38 | `Net.Send(peer, channel, payload, reliable)`, `Register(modId, channel, handler)`, `Peers`, `Me` | P2P text packets, max 1,024 bytes |
| 39 | `NetBlob.Send(peer, modId, name, bytes)`, `Received`, `Finished`, `Progress`, `Cancel`; `Presence.Publish/Remotes`; `Steam.IsReady/LocalId/Name` | big transfers, position presence, Steam identity |
| 40 | `NewGameOptions.RegisterToggle/Choice/Integer/Float`, `Get*`; `DevMenu.AddSection/AddWatch`; `ComputerApps.Register`; `WebUi.Open` | new game screen options, dev menu, computer apps, HTML windows |

Beyond these there are about 100 other `Foo` areas with the same shape (`Doors`, `Lights`, `Weather`, `Casino`, `Trains` ...). They are all "events plus a few getters/setters over one game class".


## 5. Recommendation per module

Target mods (from `TODO.md`): **ObenFramework** (core APIs everything depends on), **ObenUIKit** (UI that looks like the game's own, mod menu), **ObenSave** (open compressed save format), the **co-op mod** (ObenCoop, transport interface with direct IP first, Steam second), and a **cheat menu**.

Verdicts: **as-is** = port nearly as-is (namespace, logging, owner id; split if over 300 lines). **rework** = port with the changes listed. **skip** = leave it, or replace with ours.

"Mechanical port" below means the usual `Foo.cs` + `FooPatches.cs` pair: swap `CoreLog.For(Owner)` for the mod logger, `CorePatcher.PatchCore(..., owner)` for our patch set, keep the events, `IsAvailable` and one-line refusal behaviour. These pairs are 30 to 250 lines each and the conversion does not need thought.

| Id | Home | Verdict | Notes |
|---|---|---|---|
| M01 bootstrap | ObenFramework | skip, but take 4 pieces | Skip `Core` (714*, it is the BepInEx plugin plus 197 init blocks; the loader and a short ordered init list replace it), `FrameworkMod*`, `Loader*`, `AccessProbe`, `CoreMod` (an `ObenMod` already is the per-mod handle). Take as-is: `ModIds` (69, id and file-name validation including Windows device names), `ObjectPath` (76), the supported-build check (`Application.version` plus Steam build id, warn or refuse), and the `Isolate` idea (one feature's init failing must not stop the rest). |
| M02 patching | ObenFramework | rework | `CorePatcher` is the right shape. Change: take the `ObenMod` (or return a disposable patch set) so `OnUnload` unpatches through `Track()`; keep per-owner disable-on-failure, patch by name + arg types, handlers resolved once. Split the 311 lines: owner/Harmony registry, failure reporting (`Fail`/`Inner`/`Flatten` is 60 lines of exception text), name resolution. |
| M03 logging | ObenFramework | rework | Keep the behaviour (per-second line cap, repeated-error suppression, size cap, `Debug(key, msg)` logs changes only, `ApiLog.Once`), put the sink on `LogHub`/`ModLogger`. Split `CoreLog` 314* into logger + limiter. `HudReport`/`StoryLog` are tiny wrappers, drop. |
| M04 config + tweaks | ObenFramework (optional tweaks mod later) | skip `ModConfig` and `ConfigTheme`; rework `Tweaks*`, `CameraView` | `ModConfig` is a BepInEx wrapper, `ObenMod.LoadConfig<T>` replaces it. `ConfigTheme` restyles a plugin we do not have. The nine `Tweaks*` files (25 to 173 lines each) and `CameraView` (258) are good small mods: ship them as one optional mod, settings in a `ConfigFile<T>` class. `CameraView` already uses cached `FieldInfo`s; switch to delegates. |
| M05 input + UI state | ObenFramework | `UiState`, `InputCapture`: as-is; `Keybinds`, `CoreInput`: rework | `UiState` (101+59) and `InputCapture` (110+43) are pure patches over game singletons; mechanical port. `Keybinds` (229): swap the BepInEx `ConfigFile`/`KeyboardShortcut` store for our config, keep `KeyChord` (match and overlap logic) and the "two mods share a key" warning. `CoreInput` (143): drop the ConfigurationManager window probe, keep `GameBusy()` and `BlockGameplay(owner)`. `Ui/ModInput` (182) goes to ObenUIKit as-is. |
| M06 classic mod menu | ObenUIKit | rework (the largest rework) | Pause-menu tab injection (`ModMenuTab`, 312*), main menu entry, row/navigation code are worth having. Rows are built from BepInEx `ConfigEntryBase`; they need a setting descriptor built once by cached reflection over our config class (field, label, range, kind). `ModMenuNav` (659*) is five types in one file. `ModMenuJson`/`ModMenuActions` mostly serve the web version; keep only what the uGUI side uses. `ModMenuReflect` does an uncached `GetField` per call; replace. |
| M07 UI kit | ObenUIKit | rework | The right starting point: `ModUi` builders, `ModWindow` with a lifetime scope (`UiScope`, closes on error and on owner unload), `ModControls`, `ModTabs`, `ModPickers`, `ModelPreview`. Changes: `KeyboardShortcut` to our chord, `UiTheme` (105 lines of hard-coded colours) to a theme read from the game's pause menu at runtime as the TODO wants, split `ModUi` 601*, `ModWindow` 541*, `ModelPreview` 491*, uncached `GetField` in `ModWindow.cs:298`. |
| M08 WebUi | none | skip | Windows-only WebView2 behind a native C++ DLL, a second plugin, and the game here runs through Proton/umu. The only dependants are the web Mods menu and the reference co-op lobby page, which has a plain fallback panel. Rebuild those two pages in ObenUIKit. |
| M09 save data | ObenFramework (side files); ObenSave (reference only) | rework | `SaveData` (644*): `Register`/`Bind`, retrying reads (3 tries, 50 ms: the game may still hold the file), size and depth limits (64 MB file, 4 MB JSON, depth 256), `Capture`/`Apply`. Split into registration, file IO, capture/apply (the `GameSave` reflection block), and input refusal rules. `SaveJson` (252), `SaveGate` (83), `SaveSlots`/`SaveSlot` (189+39): as-is. For ObenSave the reference helps only a little: it never replaces the write path (it listens to `SaveController` events and writes side files in `<save>/mods/`). Useful there: the `SaveJson` Unity-struct converter and fill-missing-fields, and the list of `SaveController` members it touches. |
| M10 events + time | ObenFramework | rework `Events`; the rest as-is | Do not copy `Events.cs` (203): `src/ObenFramework/Events` already has the better hot-reload-safe design. Port its content into it: `WhenReady` semantics, the new-game-first-level case, the load-aborted case that `SaveGate`/`Events` track, hour and day events from `TimeOfDayAzure`. `GameTime`, `WorldTime`, `TimeControl`, `WorldLocation`: mechanical port. |
| M11 survival | ObenFramework | as-is | Clean, self-contained: `Survival` with `Stat` (23 stats read through public fields), threshold callbacks, decay multipliers, interception. `SurvivalPatches` (351*) is 20 `Hook(...)` lines in six owner groups; split install from handlers. |
| M12 net / lobby | co-op mod (transport layer), ObenFramework (`Steam`) | rework | See 5.2. Short version: the logic is good, the transport is hard-wired to Steam. |
| M13 debug + dev menu | Cheat menu | rework `DevMenu*`; skip `CoreDebug*` for now | `DevMenu*` (window, No clip, flags, state, watches, quest tools, 15 files) is the natural base for the cheat menu once ObenUIKit exists. `CoreDebugTrace` (368*) is a runtime tracer for the framework's own API; only useful if we keep the same API shape. |
| M14 story | ObenFramework | rework, later | `Quests`, `QuestAuthoring`, `QuestRewind`, `Dialogue*`, `Law`: valuable and complete, but 5.4k lines, 8 files over 300, and tied to PixelCrushers Dialogue System Lua registration. Port when a mod needs quests. The reference co-op mod does not use this API. |
| M15 NPCs and animals | ObenFramework | as-is for query APIs; rework `ModNpc*`, `Npc*` control | `Npcs`, `NpcsAnimals`, `Characters`, `PhoneBook`, `PhoneRules`, `Relationships`: mechanical. `ModNpcs` (650*), `ModNpcRoutinesOrders` (481*), `NpcControl` (487*), `NpcAnimation` (418*): rework, they carry per-frame ticks and claim/edit bookkeeping. On demand. |
| M16 player | ObenFramework | as-is: `Player`, `PlayerLook*`, `Seats`, sleep/wait; rework `PlayerMovement` | `Player.cs` (349*, the `PlayerState` API) and `PlayerLook` (322*) + `PlayerLookTable` (278) are needed by the co-op mod. `PlayerMovement` (574*) holds 13 `FieldInfo`s and sets them with `SetValue`: switch to `FieldRefAccess` delegates, split state from patches. The rest (skills, effects, medical, wardrobe, books, achievements, weapons) on demand. |
| M17 items + inventory | ObenFramework | as-is, split three files | `Inventory` (461*), `Items` (293), `Containers` (259), `WorldItems` (324*), `StackActions`, `UnloadedStorage*` (8 files, edits containers in scenes that are not loaded, with a tested merge). Co-op needs `Containers` and `WorldItems`. `InventorySources` (323*) is a hook to let extra containers count as inventory; take it with crafting. |
| M18 item / recipe registry | ObenFramework | rework, later | Define items and recipes at runtime with stable ids (manifest file), placeholders for items from removed mods. Needed once a mod adds items; 11 files, 1.7k lines. |
| M19 crafting | ObenFramework | as-is, on demand | 25 files in the usual pair pattern. `CraftingSources` (468*) is the hard one. |
| M20 building + furniture | ObenFramework | rework `Building`; rest later | `Building` (608*) is four public types and the whole API in one file; the co-op mod uses Place, Move, Remove, GetPlacedFurniture, Managers and the placed/removed events. Custom furniture (registry, ids, loot, shops, manifest) depends on models (M21); later. |
| M21 models + assets | ObenFramework | as-is, later | OBJ and MTL parsers (268+223, pure logic with a self-test), `ModelBuild` (568*), `ModelRegistry` (302*), `Sounds`/`SoundsWav`. |
| M22 world props + SceneLook | ObenFramework | skip for now | `SceneLook*` is 25 files / about 6,000 lines that read character looks from scene files and re-apply them with rigs and blend shapes. Large, niche, and the co-op reference solves its own look sync (`Look.cs`, `LookPin.cs`). Take `SceneIds` (85, used everywhere) as-is. `WorldProps` and `SceneState`/`SceneLogic` on demand. |
| M23 shops + economy | ObenFramework | as-is: `Cash`/`Wallet`, `ShopStock`, `Shops`; rest on demand | `Wallet` (57) + `Money.cs` (`Cash`, 302*) are small and the TODO's "Money" item. `ShopStock` (460*) is used by the co-op mod. `Traders` (555*) is the API plus four helper types in one file. Bank, mail, invoices, legal on demand. |
| M24 doors + transport | ObenFramework | as-is, on demand | Doors, locks, levels, elevators, trains. Mechanical pairs. |
| M25 world environment | ObenFramework | as-is, on demand | Weather, sky, lights, hazards, power, `GlobalFlags`, `GameSettings`. Mechanical pairs; `PowerDevices` 329* only one over the limit. |
| M25b housing | ObenFramework | as-is, on demand | |
| M26 HUD | ObenFramework (`Hud`), ObenUIKit (`HudHintPanel`, `HudBars`) | as-is / rework | `Hud.Notify` and subtitles are a few lines over the game's own notification code (TODO "Notifications"). `HudHints` queue/logic/panel is a good small subsystem, panel part belongs in ObenUIKit; `HudHintGame` (368*) reaches `Tutorial` private methods and is the fragile part. |
| M27 gambling | none | skip | Casino, card games, contests. Niche; two transpiler/iterator hacks (`<>4__this`). |
| M28 difficulty + new game | ObenUIKit (screen), ObenFramework (options store) | rework, later | `NewGameOptions*` registers options shown on the New Game screen and stores values (`NewGameOptionStore` 321*, screen builder 516*). Co-op needs a new-game hook. Rework the screen onto ObenUIKit rows. |
| M29 main menu + console | `MainMenuButtons` ObenUIKit; `ConsoleCommands` ObenFramework | rework / as-is | `MainMenuButtons` (517*) adds a button to the main menu and is used by the co-op mod; split entries/layout/tick. `ConsoleCommands` (137+82) overlaps what `src/ObenFramework/Console` already does; compare and merge. |
| M29b computer apps | ObenUIKit | as-is, later | 1k lines, needs the UI kit. |
| M29c tutorial | ObenFramework | as-is, on demand | 171 lines. |

### 5.1 Assignment summary by target mod

| Target | Take from the reference |
|---|---|
| ObenFramework | M02 patcher, M03 log behaviour, `ModIds`, `ObjectPath`, `SceneIds`, M09 `SaveData`/`SaveJson`/`SaveGate`/`SaveSlots`, M10 time classes and the semantics of `Events.WhenReady`, M11 `Survival`, M05 `Keybinds`/`UiState`/`InputCapture`/`CoreInput`, M16 `PlayerState`/`PlayerLook`/`PlayerMovement`, M17 inventory/items/containers/world items/unloaded storage, M20 `Building`, M23 `Cash`/`Wallet`/`ShopStock`/`Shops`, M15 NPC queries, `ConsoleCommands`, later M14/M18/M21/M19/M24/M25 |
| ObenUIKit | M07 whole, M06 whole (rework), `Ui/ModInput`, `MainMenuButtons`, `HudHintPanel`/`HudBars`, `NewGameOptionsScreen`, `ComputerApps*` |
| ObenSave | almost nothing directly. `SaveJson` converters, the retrying/limit-checking file read in `SaveData`, the list of `SaveController` fields and the nested `ObjectDataHeader`/`tempSavedata_*` swap used by `Capture`/`Apply` (those two will break if ObenSave changes how the controller serialises; check `Capture`/`Apply` after ObenSave hooks the byte path) |
| Co-op mod | M12 as a transport-agnostic core plus a Steam transport; plus the framework APIs in 5.2 |
| Cheat menu | M13 `DevMenu*` (window layout, No clip, flags, watches) on ObenUIKit; the actions are calls into M16/M17/M25 (`GodMode`, `NeedsFrozen`, `Give`, `SetTimeOfDay`, weather) |

### 5.2 What the co-op mod needs, and how the net layer should change

The reference co-op mod (`example-mp/`, 46.8k lines) calls these framework areas, by number of uses: `Lobby` 75 (plus `LobbyStatus` 16), `SaveData` 60 (`Busy` 46, `Capture` 7, `Apply` 7), `NetBlob` 31, `PlayerLook` 25 (+`Look` 5), `ShopStock` 15, `Building` 14, `Steam` 10, `WebUi` 8, `Net` 6, `Presence` 4, and 1 to 3 each of `Containers`, `Events`, `WorldItems`, `MainMenuButtons`, `CorePatcher`, `UiState`, `Keybinds`, `SaveSlots`, `CoreLog`, `Core.GameBuild`. Everything else it does itself (its own hooks for items, doors, quests, shops, trains, sleep).

So the minimum ObenFramework set for co-op is: `SaveData.Busy`/`Capture`/`Apply` (M09), `PlayerLook` (M16), `Building` (M20), `ShopStock` (M23), `Containers` and `WorldItems` (M17), `UiState`/`Keybinds` (M05), `SaveSlots` (M09), `CorePatcher` and the logger (M02/M03); plus M12 and a UI replacement for `WebUi.Open`.

What to change in M12, given the TODO's transport-first plan:

- `Lobby` and `SteamBridge` are the Steam transport; nothing in `Net`, `NetChannels` or `NetBlob` needs Steam except `Net.Send`/`Drain`. `NetBlob` already routes through two replaceable delegates (`PeerCheck`, `Transmit`); make that a real `ITransport` (send, receive callback, peers, local id) and implement direct IP first, Steam second.
- Packets are text: `Encoding.UTF8.GetBytes(channel + "|" + payload)`, base64 for blobs, `string.Split('|')` on receive, `string.Format` in `Presence`. That is fine for rare messages, wrong for per-frame state. Make the payload `byte[]` (segment) on both sides, keep text channels only for diagnostics. Avoid `Net.Peers` building a new `List<ulong>` on every tick.
- Keep as designed: 1,024-byte packet cap, per-peer receive budget (4 per drain) with a bounded queue (1,024) and rate-limited drop logging, 2 ms drain slice, NetBlob's window (32 chunks), ack every 8, resend after 2 s, 60 s total timeout, 16 transfers, 16 MiB cap, CRC32 and gzip. The limits are the hard-won part and `NetBlobSelfTest` pins them.
- It uses Steam's legacy P2P calls (`SteamNetworking.SendP2PPacket`, `P2PSessionRequest_t`). They work in the Steamworks.NET the game ships, but Valve has deprecated them. Isolate them in the Steam transport so replacing them touches one file.
- `Presence` (position broadcast, 125 lines): the co-op reference has its own pose sync; take only if a second mod wants simple presence.
- Version handshake: `Lobby.Host(gameTag, version, ...)` and `Join(...)` take a game tag and version, and the lobby defers a peer's P2P session until its version is verified (`DeferSession`, `IsRefused`). The reference co-op README says a lobby refuses a different version, so keep this.

## 6. Files over 300 lines

95 files are over 300 (marked `*` in the lists above). 25 of them are 301 to 330 lines, usually one extraction away from compliance. The ones that matter, with a split I would use:

| File | Lines | Split |
|---|---|---|
| `Core.cs` | 714 | gone: ordered init list + tick list in the framework entry class; self-checks dropped (about 350 of the lines) |
| `ModMenuNav.cs` | 659 | one file per type: `ModMenuColumn` (enum), `ModMenuUnit`, `ModMenuOrder`, `ModMenuNav`, `ModMenuHint` |
| `Api/ModNpcs.cs` | 650 | API, spawn/despawn, the `Record` store, queries |
| `SaveData.cs` | 644 | registration, file IO + limits, `Capture`/`Apply` + `GameSave` reflection, refusal rules |
| `Api/SceneLookApplyRig.cs` | 624 | `SceneLookApplyRig`, `SceneLookPoseDriver` (skip anyway) |
| `Story/QuestRewind.cs` | 613 | rewind points, rewind apply, patches (a `QuestRewindPlan` already exists) |
| `Api/Building.cs` | 608 | the four types (`FurnitureItem`, `BlueprintInfo`, `FurnitureManagerInfo`, `Building`) in their own files; `Building` into query / edit (Place, Move, Remove) / blueprint parts |
| `Ui/ModUi.cs` | 601 | partial static class in three files: text and cards, inputs (slider, toggle, stepper, text field, key binding), layout and tabs |
| `Api/PlayerMovement.cs` | 574 | info type, speed overrides, camera-lock fields; replace the 13 `FieldInfo`s |
| `Api/ModelBuild.cs` | 568 | mesh, materials, colliders |
| `Api/LandmarksCustom.cs` | 566 | create, save file, icon loading (uses the BepInEx plugins path) |
| `Api/Traders.cs` | 555 | `TraderInfo` and the refusal enum in their own files; the helper types (`StackState`, `Chunk`, `PriceFactor`, a private nested `Wallet`) out of `Traders`; then API / pricing / trade parts |
| `Story/QuestAuthoring.cs` | 548 | definitions, validation, create/edit |
| `Story/Dialogue.cs` | 544 | variables, events and interceptors, actor fields |
| `Ui/ModWindow.cs` | 541 | `ModWindow`, `UiScope`, `UiEvents` (already three types) |
| `Api/Lobby.cs` | 518 | state and session, members, host/join, events |
| `Api/MainMenuButtons.cs` | 517 | entries, layout, tick |
| `Api/NewGameOptionsScreen.cs` | 516 | row builders, layout, patches |
| `Ui/ModelPreview.cs` | 491 | camera rig, render texture, drag input |
| `Api/NpcControl.cs` | 487 | flags/claims, edits, tick |
| `Api/NetBlob.cs` | 486 | public API; `Outgoing`/`Incoming` state; codec (`Gzip`, `Gunzip`, `Crc32`, `Cut`, `Assemble`, `Packet`); `Budget` |
| `Api/Inventory.cs` | 461 | counts/queries, give/take, held items; `InventoryTakeMath` is already split out |
| `Api/ShopStock.cs` | 460 | API, trader records, vending records, save |
| `Api/Hud.cs` | 436 | notify/subtitle, hints, prompts |
| `Api/CraftingSources.cs` | 468 | sources, snapshot/scope, patches glue |
| `Api/FurnitureBuild.cs` | 418 | mesh vs placement parts |
| `Api/Player.cs` | 349 | `PlayerState` plus `PlayerFlagClaims` into its own file |
| `SurvivalPatches.cs` | 351 | install vs handlers |
| `CorePatcher.cs` | 311 | see M02 |
| `CoreLog.cs` | 314 | see M03 |

The self-test files over 300 (`WorldPropsSelfTest`, 575) become test classes and are not subject to the rule once they are in a test project, but split them anyway.

Files I would not bother to split because I would not port them: `webui/*` (4 files over 300), the `SceneLook*` cluster (8 files over 300), `Casino*`, `Landmarks*`, `ConfigTheme`.

## 7. The publicizer question

What the reference does: `tools/Publicize/Publicize.cs` (91 lines, Mono.Cecil) rewrites a copy of `Assembly-CSharp.dll` so every type, nested type, method and field is public (skipping event backing fields), writes `lib/Assembly-CSharp.publicized.dll`, and the csproj references that copy. It is compile-time only. The shipped framework runs against the real DLL. There are two other pieces that exist because the author did not trust this: `AccessProbe.cs` (`[assembly: IgnoresAccessChecksTo("Assembly-CSharp")]` plus a runtime test that touches `SaveController.SaveName` and `.NormalizeFolderPath`, off by default) and an optional BepInEx preloader (`preloader/`, 417 lines) that publicizes the DLL in memory at startup. The preloader has to add `[fsIgnore]`/`[NonSerialized]` to the fields it opens up so the game's FullSerializer does not start writing them into saves; that is a real hazard of runtime publicizing. The csproj also has an `AccessCheck=true` mode that builds against the unmodified DLL.

How much does the code actually depend on it? Much less than the setup suggests. My counts, over `src/` and `webui/src/`:

| Access style | Count | Needs publicizer? |
|---|---|---|
| `AccessTools.*` calls | 159 (149 `Field`, 9 `Method`, 1 `PropertyGetter`) | no |
| `typeof(X).GetField/GetMethod/GetProperty(...)` | 79 | no |
| Of those, in `static readonly` initialisers | 169 (the rest are lazy fields set in an `Init`) | no |
| `FieldInfo.GetValue/SetValue` call sites | about 244 | no |
| Harmony `___field` injected parameters | 112 in 21 files | no |
| Compile-time use of a non-public game member | none I could prove; `AccessProbe.cs` is the one confirmed place (2 members) | yes |

How I looked: from the decompile I built a set of non-public members per class (about 9,400 non-public members in 1,170 top-level types), then searched the framework for `Type.Member` hits, `.member` hits on names that exist only as non-public in the game, and calls to such names. It found 11 `Type.Member` pairs: 9 are inside reflection strings or log messages (e.g. `HudHintGame.cs` names `InputManager.keysdefault` in log text), the other 2 are `AccessProbe.cs`. The instance and call hits I checked by hand were false positives, public members that merely share a name (`NPCInfo.characterHeadController`, `Character.appearance`, TMP's `isFocused`). I also checked that the game keeps nearly everything the framework touches public: of the 22 distinct `SaveController` members the code names directly, 19 are public (and `CharacterSaves` is a nested type). The non-public ones it needs (`CharacterName`, `SaveName`, `tempSavedata_Global`, `tempSavedata_Level`, the nested `ObjectDataHeader`) it reaches with `AccessTools`/`GetNestedType`; the only direct use of a non-public member is `AccessProbe.cs`. The two things I could not exclude are private members reached through inheritance or through variables whose type I cannot infer from a regex. My best guess is zero to a few dozen sites, and probably zero. For an exact number, build the framework once with `-p:AccessCheck=true` (the csproj supports it) and count `CS0122`/`CS0271` errors. I did not run it.

Trade-offs for us:

| | Publicized reference | AccessTools / cached delegates |
|---|---|---|
| Readability | `npc.field` | one declaration per field plus a `Get` call |
| Speed | direct IL access, no boxing | `FieldRefAccess` delegates are close; `FieldInfo.GetValue` boxes value types and is much slower, which matters in `PlayerMovement`-style per-frame code |
| Game update | compile error at build time | `null` FieldInfo at runtime unless checked, or the 244 call sites crash. Needs `decompcheck`-style verification and an `IsAvailable` guard |
| Failure mode at runtime | a missing private field throws when the method using it is JITted, so the whole method or feature fails | one lookup returns null and the feature disables itself cleanly |
| Build complexity | extra Cecil step producing a second reference assembly; every mod author who wants private access needs it; we already have Cecil 0.11.4 in the build | none |
| Runtime assumptions | relies on Mono not enforcing member visibility between assemblies. Common practice, but the reference ships a probe for it, and I cannot confirm it for this Unity 6000 Mono from the source alone | none |
| Hot reload (`AssemblyRewriter`) | no conflict | no conflict |

Recommendation: do not build ObenFramework against a publicized assembly, and never publicize at runtime (the serialisation side effect above). Port to cached accessors:

1. A small typed helper in `src/ObenFramework/Reflection` (the skeleton has `MemberAccess`, `MethodInvoker`, `TypeFinder`): create `AccessTools.FieldRefAccess<TObj, TField>(name)` delegates once in a static initialiser. No per-call lookup, no boxing.
2. Use Harmony `___field` injection inside patches wherever the access is in a handler (112 sites already do).
3. Put every game member name behind one `GameMembers` class per module, checked at install; a missing member sets `IsAvailable = false` for that module only, as `CorePatcher` already does for patches.
4. Run a `decompcheck`-style verification in the build or tests: it reads every `AccessTools`/`GetField`/patch target string from our source and resolves it in `decompiled/Assembly-CSharp/`. The reference already has this tool (`tools/decompcheck/`, Python); our decompile is in the repo.
5. If one module later has dozens of private accesses in a hot path, add a publicized compile-time reference for that project only, generated by a 90-line Cecil tool like theirs. Reference-only. Decide then, not now.

Why this fits the codebase: the reference gets by with 0 to few dozen compile-time private uses, so a publicizer buys almost nothing, while the `static readonly FieldInfo` approach is already how 169 handles are done. What needs fixing is how they are read (boxing `GetValue`) and the handful of uncached lookups (`ModMenuReflect`, `SaveJson` Unity struct fields, `ModWindow.cs:298`).

## 8. Licence

`example-framework/LICENSE` is MIT, "Copyright (c) 2026 NomadsReach" (the co-op reference in `example-mp/LICENSE` has the same line). MIT requires that the copyright notice and permission text stay with "all copies or substantial portions". Source files in the reference carry no headers, so nothing is carried along automatically.

Plan, matching the `TODO.md` line about a `THIRD-PARTY-NOTICES` file:

- `THIRD-PARTY-NOTICES.md` at the repo root. For each source: project name, URL (`https://github.com/NomadsReach/Obenseuer-Modding-Framework`), the copyright line, the full MIT text, and the list of our files or folders derived from it. Add the same entry for the co-op reference if we port from it.
- Ship the file with every release that contains ported code: copy it into the mod folders via `build/Mod.targets` (`<None Include=... CopyToOutputDirectory=...>`), next to `mod.json`, so the notice travels with the DLL.
- A two-line header in every file with substantial ported or closely adapted code:

```csharp
// Adapted from the Obenseuer Modding Framework (ObenseuerCore) by NomadsReach, MIT License.
// Copyright (c) 2026 NomadsReach. See THIRD-PARTY-NOTICES.md.
```

- Files written fresh from ideas (the `Events` rework, anything where only the behaviour was copied) need no header, but list the folder in the notices file anyway.
- Do not copy the docs wholesale; the same licence covers them but they describe BepInEx APIs. If the web UI assets or native WebView2 host are ever taken, the WebView2 SDK has its own Microsoft licence and notice (the reference ships one with its WebUi zip).
- Keep the reference clone read-only and credit it in `README.md` under a short "Thanks" or "Based on" line.

## 9. Order I would port in, and the risks

Order, each step useful on its own:

1. **Plumbing** (about 1,000 lines): patcher, log behaviour, `ModIds`, `ObjectPath`, `SceneIds`, `GameMembers` + `FieldRefAccess` helper, supported-build check. Set up the test project and the decompcheck step in the same pass, and bring over the pure-logic self-tests as tests as each module lands.
2. **Saves** (M09, about 1,350 lines): `SaveData`, `SaveJson`, `SaveGate`, `SaveSlots`. The co-op mod uses `SaveData.Busy` 46 times.
3. **Events and time** (M10): fold into the existing `Events/` skeleton, port `GameTime`/`WorldTime`/`TimeControl`.
4. **Player and items** (M16, M17 subsets): `PlayerState`, `PlayerLook`, `PlayerInventory`, `Items`, `Containers`, `WorldItems`, plus `Cash`/`Wallet`, `Survival`, `Keybinds`, `UiState`, `InputCapture`, `ConsoleCommands`.
5. **Net core** (M12), with the transport interface; direct IP, then the Steam transport. Move `NetBlobSelfTest` over as unit tests first.
6. **Co-op prerequisites in the world** (`Building`, `ShopStock`, `MainMenuButtons`).
7. **ObenUIKit** (M07, then M06), then the cheat menu on top of `DevMenu*`.
8. Everything else only when a mod asks for it (story, registry, crafting, doors, weather, NPCs, SceneLook last or never).

Risks:

- **Game version drift.** The framework was verified on 0.5.08; this game looks like 0.5.13. Member names looked stable (465 of 475 name pairs resolve), but signatures, behaviours, and the compiler-generated names (`<>4__this`, iterator state fields) were not checked. Budget for re-verifying every patch in each module as it is ported.
- **Static state and hot reload.** 207 static collections, 16 persistent hosts, no teardown. Every ported module needs an unload path through `Track()`, and `Events`-style subscriptions to game events must be removed on unload or a reloaded ObenFramework will leave the old copy's handlers attached. The reference was never designed for this; it is the largest source of rework that does not show in a line count.
- **Allocation and reflection in hot paths.** The reference is careful in `Update` (cached `Action` delegates, no LINQ in the tick loop) but not in the net layer (string packets, `Split`, new lists), `PlayerMovement` (boxing `SetValue`), `SaveJson` (uncached `GetField` per Unity-struct read/write), and `ModMenuReflect`. These are known and small.
- **Language level.** The reference is C# 5 on net472: no `?.`, no interpolation, nothing newer. It ports fine, but diffs against upstream will be noisy if we modernise as we go; decide once whether we track upstream or fork.
- **Compiler-dependent hooks.** `CardGamesPatches` transpiles an iterator `MoveNext`, `CasinoPatches` reads `____currentRunEarnings`. Skip these modules or accept regular breakage.
- **Name collisions.** The game's types are in the global namespace; a public type of ours with the same name can lose to the game's after a `using`. `tools/check-names.sh` guards this; port it. Generic names to watch from the reference: `Wallet`, `Cash`, `Items`, `Building`, `Skills`, `Locks`, `Levels`, `Hazards`. (`Look` is safe, it is nested in `PlayerLook`.)
- **Steam P2P is deprecated API,** and the TODO already notes Steam lobbies do not work on every install; direct IP must not depend on any of M12's Steam code.
- **WebUi is a hard dependency of the reference co-op lobby page** (8 uses). Budget a uGUI replacement; the fallback panel in the reference shows what is needed.
- **No automated tests run in the reference's CI,** so "the self-tests pass" is a claim only about the author's machine and 0.5.08. Treat them as specs, not proof.
