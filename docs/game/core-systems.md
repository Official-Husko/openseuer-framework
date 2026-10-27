# Obenseuer: core systems and lifecycle

Notes for mod authors. Unity 6000.0.77f1, Mono, Windows x64 build (Proton on Linux), Steam app id 951240.

Source pointers are relative to `decompiled/Assembly-CSharp/` and use `File.cs:line`. Game data is in `Obenseuer_Data/`. Everything here comes from reading the decompiled code and the data files; nothing was run. Where I could not confirm something it says so.

Contents:
1. Scenes
2. Singletons and managers
3. Persistence: what survives a scene change
4. Level change and load pipeline
5. Lifecycle: boot, new game, load, level change, back to menu
6. Time, sleep, weather
7. Pause and the controls lock
8. Events you can subscribe to
9. Harmony hook points
10. Gotchas
11. Performance notes

---

## 1. Scenes

There are 124 scenes. The build index is the index in `StreamingAssets/LevelNames/LevelNames.bytes` (`level0` ... `level123` in `Obenseuer_Data/`). The file is an Int32 count followed by length-prefixed .NET strings. `ReadSceneNames` loads it at runtime into `ReadSceneNames.Instance.scenes` (`ReadSceneNames.cs:31`), which the dev console uses to validate scene names. Scene name = level name, and the game always refers to scenes by name.

| Index | Name | Notes |
|---|---|---|
| 0 | `Main Menu` | Boot scene. Has SaveController, DeveloperConsole, PlayerIdentity. No GameController. |
| 1 | `Open Sewer Intro` | New-game cinematic (`IntroStart`, `IntroScene`). Has SaveController, no GameController. |
| 2 | `Open Sewer Tenement` | Outdoor hub for the tenement. |
| 3 | `Interior Start` | Start of a new game; the "skip intro" option in `NewGame` loads it directly (`NewGame.cs:176`). Likely where `StartManager` start selections happen (inferred from names). |
| 4 | `Interior Player Tenement` | The player's apartment. The crazy-neighbor sleep event moves you here (`SleepEventController.cs:341`). |
| 5-30 | `Interior Tenement ...` | Tenement interiors (shops, Gatehouse, Kolhola, Caravan, etc.). |
| 31 | `Under Map` | Probably the fall-through area (there is an `UnderMapTrigger` and an `UnderMapDrop` entrypoint name). |
| 32 | `Test Map Empty` | Test scene. |
| 33 | `Open Sewer Metro` | Outdoor. |
| 34 | `Interior City Gates` | |
| 35, 58-110, 113-122 | `Interior Bazaar ...` | Bank, shops, Casino, Police and Jail, etc. |
| 36-57 | `Interior Mines ...` | Mines interiors. |
| 111, 112 | `Open Sewer Mines`, `Open Sewer Bazaar` | Outdoor. |
| 123 | `Test Map Card Games` | Test scene. |

Pattern: `Open Sewer *` are outdoor areas, `Interior *` are interiors. The full list is in `LevelNames.bytes` (section 12).

Which managers a scene carries (checked by scanning the serialized `level*` files for script names, not by running):

- Index 0 and 1 are the only scenes that contain `SaveController`. They are also the only scenes without `GameController`.
- Every scene from 2 to 123 contains `GameController`, `NPCManager`, `NPCDirector`, `DeveloperConsole`, `PlayerIdentity` and `info_game_logic`.
- So "gameplay scene" = has a GameController. Use `GameController.instance != null` as the test, not the scene name.

The game does not use additive loading for normal play. Every level change is a `LoadSceneMode.Single` load (section 4). The one exception is `LoadSceneAsyncTrigger` (section 4.4).

Scene name lookups: `SceneManager.GetActiveScene().name`, or `NPCManager.instance.ActiveScene` (`NPCManager.cs:25`, cached at NPCManager.Start, falls back to the active scene).

---

## 2. Singletons and managers

Almost everything is `public static X instance`, assigned in `Awake` (sometimes `Start`) and never cleared. There are 198 such types. The ones that matter:

### 2.1 Survive scene changes (created once, in Main Menu)

| Type | Access | Notes |
|---|---|---|
| `SaveController` | `SaveController.instance` | `SaveController.cs:292`. DontDestroyOnLoad. Only exists in scenes 0 and 1 originally; duplicates destroy themselves. `SaveGame` creates one if missing (`SaveController.cs:484`). Runs all level-change coroutines. |
| `LoadingScreen` | `LoadingScreen.instance` | `LoadingScreen.cs:85`. DDOL. Fade canvas used for every level change. |
| `InputManager` | `InputManager.instance` | `InputManager.cs:81`. DDOL. Named actions ("Interact", "Pause Menu", ...) over legacy `Input`. |
| `DeveloperConsole` | `DeveloperConsole.Instance` (capital I) | `DeveloperConsole.cs:301`. DDOL. F1 toggles. See 2.4. |
| `Achievements` | `Achievements.instance` | `Achievements.cs:335`. DDOL and registered in `SaveController.DontDestroyOnLoadObjects`. |
| `NPCDirector` | `NPCDirector.instance` | `NPC/NPCDirector.cs:124`. DDOL and in `DontDestroyOnLoadObjects`. Holds `globalDatabase` (all NPC data from `NPCBehavior.json`) and simulates NPCs that are not in the current scene. |
| `PlayerIdentityMainMenu` | `PlayerIdentityMainMenu.identity` | DDOL. Carries name/age/gender/difficulty chosen in the new-game screen into the first gameplay scene. |
| `ReadSceneNames` | `ReadSceneNames.Instance` | DDOL. |
| `SteamManager`, `SalsaConfigGuard` runner, `NPCPathfinding`, `InterScenePathfindingGraph`, `NodeNetwork`, `WaypointGraph`, `LoadOnLevelIni`, `TestSceneLoading` | various | All call DontDestroyOnLoad. NPC pathing graphs persist so NPCs can path between scenes. |

### 2.2 Per scene (destroyed and recreated on every level change)

Each gameplay scene carries a "Game Logic" prefab (the editor-only `TestGameLogicCreate` loads `Resources/Prefabs/Main/Game Logic`; `GameLogic.Awake` instantiates the pause menu prefab, `GameLogic.cs:13`; `GameLogic.Instance` is declared but never assigned). Its contents register as singletons in `Awake`:

| Type | Access | What it is |
|---|---|---|
| `GameController` | `GameController.instance` | Input dispatch and the controls lock (section 7). `GameController.cs:33`. |
| `GameUIController` | `GameUIController.instance` | Game menu (inventory, map, ...) open/close. |
| `PauseMenu` | `PauseMenu.instance` | Pause menu, save/load/quit buttons. |
| `TimeOfDayAzure` | `TimeOfDayAzure.instance` | Clock and calendar (section 6). |
| `WeatherManager` | `WeatherManager.instance` | Weather (section 6). |
| `WaitingController`, `WaitingUI` | `.instance` | Sleep/wait (section 6). |
| `GlobalState` | `GlobalState.instance` | String-keyed flag store, saved in `Globals.tnmt` (`GlobalState.cs`). 11 call sites; used for door locks and relays. |
| `PlayerLocator`, `OpenSewerCharacterController`, `ThirdPersonCameraController`, `ThirdPersonCameraCollision`, `PlayerStats`, `PlayerHandItems`, `Inventory`, `Money`, `PlayerIdentity` | `.instance` / `.identity` | The player. `PlayerLocator.instance.transform` is the player root. |
| `NPCManager` | `NPCManager.instance` | NPCs spawned in the current scene (`GetAllNPCs()`, `FindNpcById(int)`). |
| `PlayerLevelEntrypoints` | `.instance` | List of named entrypoints in this scene. |
| `info_game_logic` | `info_game_logic.instance` | Per-scene settings: prison level name, background radiation, sky override. Duplicates destroy themselves. |
| `TenementController`, `Crime`, `TaskController`, `MailController`, `RelationshipController`, `DifficultyController`, `StartManager`, `SaveSceneManager`, `DestructibleList`, ... | `.instance` | Save-state holders; each is a `SavableScript`. |
| `Death` | `Death.instance` | `Death.dead`, `PlayerDeath(DamageTypeNames)`. |

Not every one of these is in every scene. I could not check each scene's prefab contents from the serialized files. Interiors may lack `WeatherManager` or other outdoor-only objects; null-check before use.

### 2.3 Reaching a singleton safely

- The static field is never cleared. After a scene change it points at a destroyed object until the new scene's `Awake` runs. A destroyed Unity object compares `== null` as true but is not a real null.
- Use `if (X.instance != null)` or `(bool)X.instance`. Do not use `X.instance?.Foo()`, `X.instance ?? ...` or `X.instance is null` for per-scene singletons: those skip Unity's overloaded null check and will call into a destroyed object. (The game itself does this for `Achievements.instance?.`, which is fine only because Achievements is DDOL.)
- `Awake` order between objects in a scene is undefined. Do not touch another manager from your own `Awake`; wait until the world-ready condition in 5.5.
- Some singletons are set in `Start`, not `Awake` (`InteractObjects` at `InteractObjects.cs:100`, `GameUIInstance`). Between Awake and Start they are still the previous scene's stale reference.
- `MainMenu.Instance`, `DeveloperConsole.Instance` and `LoadMenu`-style singletons use a capital-I property or a different name. Grep the type before assuming `instance`.

### 2.4 The built-in developer console

`DeveloperConsole` (F1, or controller View+Menu; `DeveloperConsole.cs:356`) is shipped in release builds. It has 100+ commands: `god_mode`, `noclip`, `kill`, `add_item id amount`, `add_money currency amount`, `change_scene name`, `new_scene name`, `list_scenes`, `change_time`, `set_ingame_time_scale`, `change_weather`, `thunder_storm`, `player_stats ...`, `add_furniture`, etc. Command dispatch is the private `HandleInput(string input)` (`DeveloperConsole.cs:459`), a big `switch (array[0])`. `Print(string)` (`:714`) writes to the console. Opening it sets `Achievements.instance.globalDisable = true` (`:401`) and locks controls through `GameController.ControlsDisabled`.

Useful for a mod framework: a prefix on `HandleInput` lets you add commands, and `Print` gives you an in-game log sink. `DeveloperConsoleRoutines.TryChangingScene` validates names against `ReadSceneNames` and then calls `SaveController.ChangeLevel(name)` (a normal level change) or `LoadingScreen.ChangeScene(name)` (the `new_scene` variant, which skips the save/load pipeline entirely).

---

## 3. Persistence: what survives a scene change

Three different mechanisms:

1. **DontDestroyOnLoad objects** (section 2.1). These keep their C# state across scenes.
2. **The save pipeline.** Every level change writes a save and reloads from it (section 4). Anything not captured by a `SavableScript` is lost with the scene. This is how per-scene objects "persist": `TimeOfDayAzure`, `WeatherManager`, `PlayerIdentity`, `GlobalState`, `StartManager` and others serialize with `SaveController.SerializeData(this, global: true)` into `Globals.tnmt`, and are deserialized into the freshly created scene object.
3. **Static fields.** Plain statics persist for the whole process: `Changelevel.entrypointNameEnter/Exit/entrypointScene`, `SaveController.LastSaveFolderPath`, `LoadSceneAsyncTrigger.currentAsyncScenes`, all the static events in section 8, and the `waitingBlockers` set in WaitingController.

### 3.1 SaveController.DontDestroyOnLoadObjects

`public static List<GameObject> DontDestroyOnLoadObjects` (`SaveController.cs:247`). Objects in this list receive a subset of the save/load callbacks even though they are not in the active scene: `OnMapChanging`, `OnSavingGameSpecial`, `OnLoadingGameSpecial` (`SaveController.cs:507, 517, 802`). `Achievements` and `NPCDirector` register themselves. The list is plain; any GameObject can be added (it must be a root object, since `ExecuteChildSaveLoadFunctions` uses `GetComponentsInChildren<SavableScript>` from roots, `:1441`).

This is the cleanest way for a framework to get save/load callbacks that survive scene changes: add a root GameObject with a `SavableScript` subclass, call DontDestroyOnLoad, and add it to the list. Those three callbacks are the only ones DDOL objects get. `OnMapChanged` and all the other callbacks only run on the active scene's root objects.

### 3.2 SavableScript callbacks

`SavableScript` (`SavableScript.cs`) is the base for anything that saves. Each callback is `public virtual bool OnX()` returning true. Only components that are `activeInHierarchy` are called, and exceptions are caught and logged per component (`SaveController.cs:1441-1515`).

Save order (`SaveController.SaveGame`, `:475`):

```
OnMapChanging (scene + DDOL)        only if changing level
SavingStarted event
OnSavingGamePrimary, Secondary, Tertiary   (scene)
DestructibleList.OnSavingGameDestructibleList
OnSavingGameSpecial                  (DDOL list)
OnSavingGame, OnSavingGameLatePrimary      (scene)
-- files written --
OnSavingFile
SavingDone event
```

Load order (`LoadSaveGameDifferentScene`, `:776`), after the new scene is loaded:

```
LoadingStarted event
OnLoadingGamePrimary, Secondary, Tertiary  (scene)
OnLoadingGameSpecial                 (DDOL list)
DestructibleList.OnLoadingGameDestructibleList
OnLoadingGameDestructibleListCheck   (includes inactive)
-- yield one frame --
OnLoadingGame, OnLoadingGameLatePrimary
OnLoadingGameDestructibleListCheck   (again)
OnMapChanged                         (only if the level actually changed or an entrypoint was given)
MovePlayerToEntrypointDelayed        (starts a 0.1 s real-time delay)
dialogue data applied, Loading = false
LoadingDone event
LoadingScreen.Fade(false)
```

Convention for a saved type: a `public const string GUID = "..."` (or static string) and `[fsProperty]` / `[fsIgnore]` fields; `OnSavingGame` calls `SaveController.SerializeData(this, global)` and `OnLoadingGame` calls `DeSerializeData(this, global)`. The GUID is found by reflection (`GetField("GUID")`), so a mod type needs a field named exactly `GUID`. `global: true` goes to `Globals.tnmt`, `false` to the per-level file.

### 3.3 Save files

`Application.persistentDataPath/Saves/<Character>/<SaveName>/`:

- `Info.tnmt`: header (level, entrypoint, date, app version), empty data list.
- `Globals.tnmt`: all `global: true` data plus the destination level and entrypoint.
- `<Level Name>.tnmt`: per-level data for each level visited.
- `Globals.dialogue`: PixelCrushers Dialogue System state (Lua variables like `Day`, `PlayerFirstName`).

Level changes use the two rotating slots `Autosave` and `Autosave2` (`GetOldestAutosave`, `SaveController.cs:342`). Formats are FullSerializer JSON. `MigrationController.RunMigrations` and `Migrate(...)` handle old versions on load (`SaveController.cs:1522+`).

Under Proton the path is the prefix's `AppData/LocalLow/Loiste Interactive/Obenseuer/Saves/` (company and product name come from `Obenseuer_Data/app.info`).

---

## 4. Level change and load pipeline

### 4.1 Triggers

All of these end up in `SaveController.ChangeLevel`:

- `Changelevel.ChangeLevel()` (doors, ladders, `DoorChangelevel`; `Changelevel.cs:72`). Guarded by `!SaveController.Loading` and `WaitingController.waitingType == None`. It records the `Changelevel.entrypointNameEnter/Exit/entrypointScene` statics first.
- `ChangeScene.Change(string scene)` (`ChangeScene.cs:9`): if the target equals `NPCManager.instance.ActiveScene` it just teleports to the entrypoint instead.
- `Crime` arrest (`Crime.cs:338`), `SleepEventController` (`:341`), `BlackoutTrigger` (`:123`), dev console `change_scene`.

### 4.2 ChangeLevel in steps

`public static void ChangeLevel(string newlevel, string newLevelEntrypoint = "NONE")` (`SaveController.cs:307`) starts the coroutine `ChangeLevelWhenReady` (`:322`):

1. Wait until `Loading` is false. Set `Loading = true`.
2. **`PlayerWillChangeLevel` event.** Game is still running normally.
3. If `newlevel` is empty: `ChangeLevelSame` (`:421`): fade, save in place with no scene load, optionally teleport to the entrypoint. No Loading* events fire in this case.
4. Otherwise `ChangeLevelDelay` (`:403`): fade in the loading screen and wait for it, then `SaveGame(oldestAutosave, newlevel, entrypoint)` (this shows `SavingUI`, which sets `Time.timeScale = 0`), then start `LoadGameWithMigration(characterName, autosaveName)`.
5. `LoadGameWithMigration` (`:1522`): invoke `PlayerWillLoadGame` (wrapped in an empty try/catch, so subscriber exceptions vanish), read and deserialize `Globals.tnmt` and `<newlevel>.tnmt` (migrating if needed), then start `LoadSaveGameDifferentScene`.
6. `LoadSaveGameDifferentScene` (`:776`): `LoadingScreen.LoadLevel(playerLevel, ...)` does `SceneManager.LoadSceneAsync(name)` in Single mode and waits for `LevelLoadDone`. Then the load order in 3.2, then `LoadingDone`, then fade-out.

Loading from the main menu (`LoadMenu.LoadCoroutine`, `LoadMenu.cs:536`) goes straight to step 5 with `fromMenu: true`. There is no `PlayerWillChangeLevel` and no preceding save.

The event order for a normal level change is therefore:

```
PlayerWillChangeLevel -> SavingStarted -> SavingDone -> PlayerWillLoadGame -> (scene unload/load) -> LoadingStarted -> LoadingDone
```

A plain autosave (`SaveController.Autosave(MonoBehaviour)`, `:458`, called by `TriggerAutosave` and `logic_autosave`) raises only `SavingStarted` and `SavingDone`.

### 4.3 Entrypoints

An entrypoint is a named `PlayerLevelEntrypoints.Entrypoint` (name, `Location` transform) in the destination scene. `Changelevel.Awake` inserts its own `ThisEntrypoint` into `PlayerLevelEntrypoints.instance.Entrypoints` (`Changelevel.cs:49`). The entrypoint name travels in the save header (`SaveDataHeader.NewlevelEntrypoint`) as a string; `"NONE"` means "keep the saved position".

After `LoadingDone`, `MovePlayerToEntrypoint(string entrypoint = "NONE")` (`SaveController.cs:840`) runs after a 0.1 s `WaitForSecondsRealtime` and calls `Entrypoint.TeleportPlayer()` (`PlayerLevelEntrypoints.cs:32`), which teleports and fires the entrypoint's `OnPlayerSpawn` output. If the name is not found it logs an error and uses the first entrypoint that is not `dontUseAsDefaultSpawn`.

### 4.4 LoadSceneAsyncTrigger

`LoadSceneAsyncTrigger` (`LoadSceneAsyncTrigger.cs`) is a trigger volume next to a `Changelevel`. When the player (tag `Player`) enters, it calls `SceneManager.LoadSceneAsync(changelevel.OtherLevel, LoadSceneMode.Additive)` and records the operation in the static `currentAsyncOperations` / `currentAsyncScenes` lists. `LoadingScreen.LoadAsynchronously` checks those lists and reuses the operation if the destination was preloaded (`LoadingScreen.cs:207`). It is a preload, not a streaming-world system.

Caveats from reading it, none confirmed in play:
- Scene activation is not deferred, so the additive scene becomes a live loaded scene. The `completed` handler unloads it only if its name is no longer in `currentAsyncScenes`, which it still is at that moment; `OnTriggerExit` removes `changelevel.name` (the GameObject name), not `OtherLevel`. So the preloaded scene may stay loaded until the next Single load.
- Save and load only walk `SceneManager.GetActiveScene().GetRootGameObjects()`, so anything in an additively loaded scene is ignored by saving.
- I could not determine from the serialized scene files which scenes contain this component (the script name did not appear in the level files I searched).
- So: do not assume exactly one scene is loaded. Use `SceneManager.GetActiveScene()`, not "the" loaded scene.

---

## 5. Lifecycle

### 5.1 Boot

1. Unity starts `level0` = `Main Menu`. `RuntimeInitializeOnLoadMethod` hooks in game code: `Animal.ResetActiveAnimalCount` (SubsystemRegistration), `SalsaConfigGuard.Bootstrap` (AfterSceneLoad; creates a DDOL runner and subscribes `SceneManager.sceneLoaded` to validate lip-sync setup), `PixelCrushers.Tweener.SubsystemRegistration`.
2. `SteamManager.Awake` calls `SteamAPI.RestartAppIfNecessary(951240)` (`SteamManager.cs:67`) and quits if it returns true. Launching the exe without Steam running can make the game relaunch through Steam and exit. Plan loader injection around that.
3. Main Menu objects Awake: `SaveController` (DDOL), `LoadingScreen`, `InputManager` (DDOL; `Start` loads key bindings), `DeveloperConsole`, `PlayerIdentityMainMenu`, `ReadSceneNames` (loads scene list in `Start`), `MainMenu.Instance`. `Time.timeScale = 1` in `MainMenu.Start`.

There is no single "game started" hook. For a loader, the first `SceneManager.sceneLoaded` / first `Update` is the earliest sensible point; `SaveController.instance != null` and `InputManager.instance != null` are true once Main Menu has been through Awake.

### 5.2 New game

1. `NewGame.Done()` validates the name, `StartNew()` calls `SaveCharacter()` which writes name/age/gender/difficulty into `PlayerIdentityMainMenu.identity` (`NewGame.cs:140-146`).
2. `DoneEffect` (`:148`): white fade, 7 s wait, then `LoadingScreen.LoadLevel("Open Sewer Intro", ...)`. With "skip intro" it loads `Interior Start` instead.
3. The intro scene has no GameController or SaveController-driven flow of its own; it leaves through `LoadingScreen` (`ChangeSceneIntro.ChangeScene(string)` is the helper used by its triggers) into `Interior Start` and then normal gameplay scenes. I did not trace the exact scene sequence beyond that.
4. In the first gameplay scene, `PlayerIdentity.Start` calls `GetCharacterEditorIdentity()` (`PlayerIdentity.cs:111`), which copies from `PlayerIdentityMainMenu.identity` once (`mainMenuInitDone`) and then applies difficulty one frame later (`StartDelay`).

A new game does not go through `LoadGameWithMigration` and none of the `SaveController` events fire for it, because there is no save to load. The first save and `LoadingDone` appear only after the first level change or autosave. Mods that wait for `LoadingDone` will miss the initial new-game scene; use the readiness check in 5.5.

### 5.3 Loading a save from the main menu

`LoadMenu.LoadGameSlot` -> `LoadCoroutine` (fade, `GameIsPaused = false`) -> `SaveController.LoadGameWithMigration(folder, saveName, fromMenu: true)` -> the pipeline in section 4. Events: `PlayerWillLoadGame`, `LoadingStarted`, `LoadingDone`. `PlayerWillChangeLevel` is not raised. `OnMapChanged` is skipped when `fromMenu` is true unless an entrypoint was supplied.

### 5.4 Back to main menu, quit

- `PauseMenu.QuitMainMenu()` (`PauseMenu.cs:441`): destroys the scene's `PlayerIdentity`, `Achievements.instance.ResetValues()`, clears `SaveController.LastSaveFolderPath`, `LoadingScreen.LoadLevel("Main Menu", ...)`. No `SaveController` event fires. Credits do it too (`StatsAfterCreditsUI.cs:106`).
- `PauseMenu.QuitDesktop()` (`:452`) is `Process.GetCurrentProcess().Kill()`. `OnApplicationQuit` and `AppDomain.ProcessExit` will not run. If a mod needs to flush on quit, hook `QuitDesktop` itself (and treat the process as killable at any time).
- Death: `Death.PlayerDeath(DamageTypeNames)` (`Death.cs:19`) opens the pause menu in its death variant (`PauseMenu.PausemenuDeath`).

Reliable "returned to main menu" signal: `SceneManager.sceneLoaded` (or `activeSceneChanged`) with `scene.name == "Main Menu"`. `NPCDirector` itself checks `NPCManager.instance.ActiveScene == "Main Menu"` (`NPCDirector.cs:108`).

### 5.5 "World is ready" and "player can move"

There is no single event that is right in every case. What the game itself does is poll `TimeOfDayAzure.instance.updateTimeDisabled` together with `SaveController.Loading` (see `NPCInfo.cs:369`, `NaturalLightSourceChecker.cs:63`, `NPCManager.StartDelay`, `LoadingScreen.cs:233`). A usable readiness check:

```csharp
static bool WorldReady() =>
    GameController.instance != null            // gameplay scene, not menu/intro
    && PlayerLocator.instance != null
    && TimeOfDayAzure.instance != null
    && !TimeOfDayAzure.instance.updateTimeDisabled   // clock running; false during load
    && !SaveController.Loading;                // see gotcha 10.1
```

Event-based, for saved games: `SaveController.LoadingDone` means all save data has been applied. At that moment:
- The loading screen is still opaque; it starts fading right after (`LoadingScreen.Fade(false, ...)`).
- The player has not yet been teleported to the entrypoint (0.1 s real-time later).
- Scene `Start()` methods are not guaranteed to have all run for things that wait on the clock.

"Player can move" is the conjunction of:
- `!GameController.instance.keysDisabled`
- `!OpenSewerCharacterController.instance.ControlsDisabled()` (`OpenSewerCharacterController.cs:198`)
- `!GameController.instance.GameIsPaused`, `!DialogueController.instance.DialogueActive`
- the loading screen's canvas group alpha is 0 (see gotcha 10.3).

The intro/tutorial in `StartOpenSewer.Start` (`StartOpenSewer.cs:50`) calls `ControlsDisabled(base.gameObject)` for the opening cutscene and `ControlsEnabled` when it finishes, so watching `GameController.ControlsEnabled` is the most direct "control handed back" signal for the first moments of a new game.

---

## 6. Time, sleep, weather

### 6.1 TimeOfDayAzure

`TimeOfDayAzure.instance` (`TimeOfDayAzure.cs`, scene-scoped, a SavableScript). Time is driven by an `AzureTimeController` (Azure Sky asset). Fields: `dayLenghtInMinutes = 24` (24 real minutes per in-game day, so one real second is 60 in-game seconds), `timeSpeedOverrideFactor` (speed multiplier), `dayCounter` (days since start), `currentTimeAndDay`, `startTimeAndDay`.

Reading time:
- `TimeOfDayAzure.instance.currentTimeAndDay` is a `TimeAndDay` (`TimeAndDay.cs`): `seconds, minutes, hours, weekDay (0 = Monday), day, month, year, currentDay`. It is refreshed once per in-game minute and on date changes, not every frame.
- Live values: `CurrentHours`, `CurrentMinutes`, `CurrentSeconds`, `CurrentTimelineValue` (hours as a float 0-24), `GetDayOfWeek()`, `GetDay()`, `GetMonth()`, `GetYear()`.
- Day counter: `dayCounter` and `currentTimeAndDay.currentDay` (also stored in Lua variable `Day`). `TimeAndDay.TotalMinutes`/`TotalSeconds` give a monotonic total including `currentDay`.
- `IsItDay()` is hours 6 through 20 inclusive (`:424`).

Writing time: `SetTimeline(float hours)`, `SetDate(int year, int month, int day)`, `SetSpeedFactor(float)` (`:433`), `SetFollowTarget`. Set `timeSpeedOverrideFactor` directly or call `SetSpeedFactor`; the dev console `set_ingame_time_scale` does the latter.

How a tick works (`Update` -> `CalculateDeltaTime` `:439` -> `UpdateCurrentTimeAndDay` `:460`): floor of timeline in seconds; when it changes, `SecondsPassed(deltaSeconds)` fires. If the minute changed: `MinutePassed(60)` (the argument is always 60, not the real delta), then if the calendar day changed `IncreaseDayCounter()` (`:526`, fires `DayChanged(dayCounter)`, sets Lua `Day`, updates survival achievements), then `currentTimeAndDay.SetToCurrentTimeAndDay()`, then `CurrentTimeAndDay(timeAndDay)` fires. So inside a `MinutePassed` handler `currentTimeAndDay` is still one minute stale; use the `CurrentTimeAndDay` event for fresh values. Each invocation is wrapped in try/catch and logged.

Gating: `updateTimeDisabled` (`:244`) is true until the first frames after scene start (`UpdateDelay`) and while loading (`OnLoadingGamePrimary` sets `_updateTimeDisabled`, cleared in `OnLoadingGameDelay` when the clock matches the saved time and `Loading` is false). While disabled, no ticks and no events.

Timers: `new TimeOfDayAzure.Timer(Action callback, int seconds)` (counts in-game seconds, saved/restored via `OnSaving`/`OnLoading`), and coroutine yields `WaitForIngameSeconds(int)` and `WaitForIngameMinutes(int)`. Both return immediately if `instance` is null. Note `Timer`'s constructors dereference `instance.timers` with no null check.

### 6.2 Sleep and waiting

`WaitingController.instance` (`WaitingController.cs`). Sleeping and waiting do not skip time; they speed it up. `Sleep`/`Wait` call `TimeOfDayAzure.instance.SetSpeedFactor(PlayerStats.instance.tiredness.SleepTimeSpeed)` (or the `speedFactor` passed to `Wait`), disable controls and the game menu, and show the clock. `Wakeup` resets the factor to 1.

Progress is driven by `PlayerStats.DeltaSeconds` (subscribed to `SecondsPassed`, `PlayerStats.cs:227`) -> `PlayerStatsTiredness` -> `WaitingController.OnWait(sec)` and `CheckEvent(sec)` (`:147`). `CheckEvent` accumulates `sleepingTimeSeconds`, counts down the alarm and any pending `SleepEvent`, and calls `Wakeup` when the alarm or a need interrupts. Consequence: if `PlayerStats.Active` is false or `StatsProgressDisabled` is true, sleep never progresses.

State: `waitingType` (`None, Sleeping, Passout, Waiting, Working, HavingFun, Reading, Playing, StaringWall, Arrest, WatchingTV, Talking, Studying`), `IsSleeping`, `IsWaiting`, `alarmEnabled`, `sleepingTimeSeconds`.

API:
- `bool Sleep(int currentSleepSetTime)` (`:242`), `void Wait(int currentSleepSetTime, Type type, float speedFactor = 0f, bool ignorePlayerNeeds = false, IEnumerable<PlayerStatsTiredness.Reason> ignoredReasons = null)` (`:329`), `void Work(int)`, `void PassOut(bool canBeArrested = true, Type type = Type.Passout)` (`:217`), `void Wakeup(bool alarm = false, bool onlyClearWaiting = false)` (`:372`).
- Callbacks: `SetOnWakeUpCallback(Action)`, `SetTimeWaitedCallback(Action<int>)`, `SetOnWaitingCallback(Action<int>)`. Each holds one delegate and is cleared by `Wakeup`; they are overrides, not events, so setting yours replaces the game's.
- Block waiting: `WaitingController.BlockWaiting(UnityEngine.Object)` / `UnblockWaiting(...)` (static; a destroyed blocker is auto-pruned). `PassOut` ignores blockers only for `Type.Arrest`.
- `CanStartWaitingStaus` is false while `Changelevel.ChangeLevelActive`.

For "a day passed" use `TimeOfDayAzure.DayChanged`. For "player woke up" there is no event; patch `Wakeup` (postfix) or use `SetOnWakeUpCallback`.

### 6.3 Weather

`WeatherManager.instance` (`WeatherManager.cs`), wrapping Azure's `AzureWeatherController`. Weather is indexed by position in the controller's global profile list.

- `SetWeather(int weatherProfile, bool changeSpeed = false, float transitionTime = 0f)` (`:211`), `SetWeather(AzureWeatherProfile, float transitionLength = 10f)`, `SetRandomWeather(float transitionTime)`, `ChangeWeather(float time)`.
- Reads: `ActiveWeather()`, `ActiveWeatherIndex()`, `WeatherCount()`, `WeatherName(int)`, `RainIntensity()`, `WindIntensity()`, `Temperature()` (degrees, `-20..+20` range from a curve), `IsThunderActive()`, `StartThunder()/StopThunder()`.
- Automatic change: every `MinutePassed` subtracts 60 from `currentWheatherLenght` (a 6-24 hour countdown in seconds); at zero a random weather starts with a 50-100 s transition (`:138`). It subscribes in `StartDelay` (0.3 s after Start) and unsubscribes in `OnDestroy`.
- Saved as `savedWeather` (index); `OnLoadingGame` re-applies it with `changeSpeed: true`.
- Events: `WeatherManager.WeatherUpdated` fires at the start of `ChangeWeather`; `WeatherTransitioning` fires every frame for the duration of the transition.

---

## 7. Pause and the controls lock

### 7.1 Pause

Two separate pieces of state:

- `Time.timeScale == 0` (also exposed as `PauseMenu.paused`, `PauseMenu.cs:95`). Set by `PauseMenu.PauseMenuOpen()` (`:216`, also sets `AudioListener.pause`, hides UI, shows the cursor) and by `SavingUI.ShowSavingUI` (`SavingUI.cs:51`). Restored to 1 in `PauseMenuClose()` (`:257`), `SavingUI.HideSavingUI`, `LoadingScreen` after a load, and `MainMenu.Start`.
- `GameController.instance.GameIsPaused` (public bool field): set by `PauseMenuOpen/Close`, `SavingUI`, and cleared by `LoadMenu.LoadCoroutine`.

So the game is also "paused" (timeScale 0, GameIsPaused true) for the duration of every save, including autosaves and the save inside a level change. `PauseMenu.instance.PauseIsActive()` distinguishes the actual pause menu from a save.

Pausing is the Escape key through `GameController.Update` -> `PauseMenu.Pause()`. `Update` still runs at timeScale 0, so anything on `Time.deltaTime` freezes but `Time.unscaledDeltaTime`, coroutines using `WaitForSecondsRealtime`, and input keep working. The main menu scene also reads Escape in `PauseMenu.Update` when `GameController.instance == null`.

### 7.2 ControlsDisabled / ControlsEnabled

The game's reference-counted lock (`GameController.cs:285-368`):

```csharp
public void ControlsDisabled(GameObject gameObject, bool ShowCursor = false, bool disableKeys = true,
    bool writeLock = false, bool lockedFirsperson = false, bool firstpersonSoftLock = false,
    Transform softLockTarget = null, bool disableCamera = true, bool disableMovement = true)
public void ControlsEnabled(GameObject gameObject, bool disableFirstpersonSoftLock = false)
```

Each caller passes its own GameObject as a key. The controller keeps lists (`disabledList`, `disabledKeysList`, `disabledWritelockList`, `disabledCursorList`); flags go back to false only when the list for that flag is empty. `ControlsDisabled` also locks movement via `OpenSewerCharacterController.DisableControls(gameObject, noclip)` (its own `disabledList`), and optionally the camera. `ControlsEnabled` also resets cursor lock to `Locked` when no cursor holders remain.

Rules for mods:
- Pair every `ControlsDisabled(go)` with `ControlsEnabled(go)` using the same GameObject. Use your own persistent GameObject (not a scene object that may be destroyed).
- Destroyed GameObjects are not purged from these lists. A scene object that locked controls and was destroyed before unlocking leaves the lists non-empty in that GameController, but GameController is per-scene, so this resets on the next level change. A DDOL key that is never released locks controls permanently.
- `ControlsDisabled` has side effects beyond a flag (cursor, camera, movement), and `disableKeys` is true by default, which also blocks the game's key handling (`GameController.Update` returns early when `keysDisabled && !GameIsPaused`).
- Related separate locks: `DisableInventory(go)`/`EnableInventory(go)` (blocks the menu hotkeys), `GameUIController.DisableGameMenu()/EnableGameMenu()`, `DisableZoom/EnableZoom`, `InteractObjects.DisableInteracting()/EndableInteracting()` (sic), `PlayerHandItems.itemUseDisabled`.

State to read: `GameController.instance.keysDisabled`, `writeLock`, `inventoryDisabled`, `cursorVisible`, `GameIsPaused`; `GameUIController.instance.IsMenuVisible` (also true while the wait/sleep clock UI is up); `DialogueController.instance.DialogueActive`.

### 7.3 Input

`InputManager.instance` (`InputManager.cs`) maps named actions to `KeyCode`s: `GetKey(string)`, `GetKeyDown(string)`, `GetKeyUp(string)`, `GetKeyWithHold(string, int resetValue = 4)`, `GetKeyCode(string, bool ignoreController = false)`, `SetKey(string, KeyCode)`. Action names are strings: `Forward, Backward, Left, Right, Walk, Crouch, Jump, Interact, Menu, Inventory, Map, Tasks, Tenement, Character, Notes, Pause Menu, Hide Hud, Hints, Primary Use, Auxiliary Use, Head Use, Primary Reload, Auxiliary Reload, Building Menu, Move Left, Move Right, Place Take, Rotate Object, Rotation Mode, Snap Object, Buy Sell, Resource Slots, Move Item, Drag And Drop, Drop, Switch Tab Left/Right, Use Item, Exit Cancel` (defaults in `SetDefaultKeys`, `:291`). `InputManager.inputType` is `Keyboard` or `Controller`; instance event `InputTypeChanged(InputType)`.

Keys are read through both the legacy `Input` class and a `guiKeys*` set filled in `OnGUI` (`:133`) so layout-insensitive keys work. A mod that reads `UnityEngine.Input` directly works, but ignores rebinding and the game's locks. Hardcoded keys elsewhere: F1 (console), F11/F12 (screenshots), Alpha1-4 (equip toggles) in `GameController.Update`.

---

## 8. Events you can subscribe to

Static events (subscribe once, never rely on scene objects to unsubscribe for you):

| Event | Delegate | Raised | Notes |
|---|---|---|---|
| `SaveController.PlayerWillChangeLevel` | `()` | `SaveController.cs:329` | Only for `ChangeLevel`. Before fade and save. |
| `SaveController.PlayerWillLoadGame` | `()` | `:1531` | Every load incl. from menu. Wrapped in try/catch. |
| `SaveController.SavingStarted` / `SavingDone` | `()` | `:512` / `:545` | Every save incl. autosaves and level-change saves. |
| `SaveController.LoadingStarted` | `()` | `:798` | New scene loaded, save data not yet applied. |
| `SaveController.LoadingDone` | `()` | `:821` | Save data applied. Not wrapped in try/catch. |
| `TimeOfDayAzure.SecondsPassed` | `(int seconds)` | `:464` | Near every frame at normal speed. |
| `TimeOfDayAzure.MinutePassed` | `(int minute)` | | Arg is always 60. About once per real second at normal speed. |
| `TimeOfDayAzure.DayChanged` | `(int day)` | `IncreaseDayCounter` | Arg is the absolute `dayCounter`. |
| `TimeOfDayAzure.CurrentTimeAndDay` | `(TimeAndDay)` | after MinutePassed | Fresh time, once per in-game minute. |
| `WeatherManager.WeatherUpdated` / `WeatherTransitioning` | `()` | | Transitioning fires each frame during a transition. |
| `GameUIController.ExitPanels` | `()` | | Game menu closed. |
| `ThirdPersonCameraCollision.CameraModeChange` | `(bool firstperson)` | | |
| `Crime.OnGoingToPrison` | `()` | `Crime.cs:327` | |
| `TaskController.TaskUpdated` / `ObjectiveUpdated` | `()` | | |
| `MailController.MailSent / MailReceived / PackageSent / PackageReceived` | see `MailController.cs:31-37` | | |
| `ItemInfo.ItemDataChanged` | `(ItemData)` | | |
| `PlayerStats.PlayerSoiledPants`, `PlayerAutoConsume.Consumed`, `HintOverlay.HintsToggled`, `Notifications.EventFired`, `WindowNaturalLight.SourcesChanged` | `Action` / `()` | | Minor. |

Instance events (need the object): `InputManager.InputTypeChanged(InputType)`, `Inventory.ItemConsumed(Item item, SlotController slot = null, int amount = 1, int owner = -1, object[] meta = null)`, `PlayerStats.PlayerDefecatedInPublic()`, `FurnitureManager.FurniturePlaced/FurnitureTaken(GameObject)`, NPC events (`NPCInfo.StateChanged`, `NPC_FOV.PlayerDetected` ...). Because these belong to scene objects you must re-subscribe after each level change (do it from `LoadingDone` or from a postfix on the owner's `Awake`).

Other hooks the game provides:
- `SavableScript` callbacks via a component in `SaveController.DontDestroyOnLoadObjects` (3.1).
- `SceneManager.sceneLoaded` / `activeSceneChanged` (standard Unity; the game only uses `sceneLoaded` in `SalsaConfigGuard` and `LoadOnLevelIni`).
- Dialogue System Lua functions registered via `Lua.RegisterFunction` (see `StartManager.RegisterDialogue`, `NPCManager.RegisterDialogue`) are an existing extension point for conversation scripting.

Game code subscribes to these in a way worth copying: subscribe in `OnEnable`/`Start` and unsubscribe in `OnDisable`/`OnDestroy` (see `MarketShop.cs:181`, `BlackoutController.cs:48`). A handler that is never removed is the leak described in 10.7.

---

## 9. Harmony hook points

All of these are plain Mono, so Harmony works normally. `private` methods are patched by name string. Signatures are exactly as in the decompiled source.

### Lifecycle

| Member | Signature | Use |
|---|---|---|
| `SaveController.ChangeLevel` | `public static void ChangeLevel(string newlevel, string newLevelEntrypoint = "NONE")` | Observe or redirect a level change (prefix; set `newlevel`/`newLevelEntrypoint` by ref-named args). Only runs the coroutine when `instance` exists. |
| `SaveController.SaveGame` | `public static void SaveGame(string savename = "", string newLevel = "", string newLevelEntrypoint = "NONE")` | Every save. Synchronous. Blocks the main thread while writing. |
| `SaveController.LoadGameWithMigration` | `public static IEnumerator LoadGameWithMigration(string characterName = "", string saveName = "", bool fromMenu = false)` | Prefix to see which save is being loaded. A postfix runs when the iterator is created, not when loading ends. |
| `SaveController.MovePlayerToEntrypoint` | `public void MovePlayerToEntrypoint(string entrypoint = "NONE")` | Postfix = player has just been placed after a level change. |
| `PlayerLevelEntrypoints.Entrypoint.TeleportPlayer` | `public void TeleportPlayer()` | Nested class; use `AccessTools.Method(typeof(PlayerLevelEntrypoints.Entrypoint), "TeleportPlayer")`. |
| `LoadingScreen.LoadLevel` | `public void LoadLevel(string sceneName, bool soundsMuted, bool isWhiteScreen, bool manualFade = false, bool changeHint = true)` | Every non-`ChangeLevel` scene load including new game and quit-to-menu. |
| `PauseMenu.QuitMainMenu` | `public void QuitMainMenu()` | Return to menu (prefix, before teardown). |
| `PauseMenu.QuitDesktop` | `public void QuitDesktop()` | Last chance before `Process.Kill`. |
| `Death.PlayerDeath` | `public void PlayerDeath(DamageTypeNames reason)` | Player died. |
| `Changelevel.ChangeLevel` | `public void ChangeLevel()` | Door/ladder transition; has `OtherLevel` and `OtherEntrypoint`. Prefix can cancel by returning false. |

### Time and world

| Member | Signature | Use |
|---|---|---|
| `TimeOfDayAzure.IncreaseDayCounter` | `public void IncreaseDayCounter()` | Day rollover. Prefer the `DayChanged` event. |
| `TimeOfDayAzure.SetSpeedFactor` | `public void SetSpeedFactor(float newValue)` | Sleep/wait speed-up (and dev console); a good place to clamp or log. |
| `TimeOfDayAzure.SetTimeline` / `SetDate` | `public void SetTimeline(float value)` / `public void SetDate(int year, int month, int day)` | Time jumps. |
| `WaitingController.Wakeup` | `public void Wakeup(bool alarm = false, bool onlyClearWaiting = false)` | Postfix = player woke. Note it can return without doing anything. |
| `WaitingController.Wait` / `Sleep` / `PassOut` | see 6.2 | Start of waiting/sleep. |
| `WeatherManager.SetWeather` | `public void SetWeather(int weatherProfile, bool changeSpeed = false, float transitionTime = 0f)` | Every weather change funnels here (also `SetRandomWeather`, and the `AzureWeatherProfile` overload calls it). |

### Control and UI

| Member | Signature | Use |
|---|---|---|
| `GameController.ControlsDisabled` / `ControlsEnabled` | see 7.2 | Detect lock changes. Called many times per frame in some UIs. |
| `GameController.Update` | `private void Update()` | Master input dispatch. Prefix returning false suppresses all gameplay input for the frame (not recommended; prefer the controls lock). |
| `GameUIController.ShowGameMenu` / `HideGameMenu` | `public void ShowGameMenu(bool forceOpen, bool playSound, MenuPanels MenuPanelName, MenuPageNames MenuPageName, bool ignoreDialogue = false)` / `public void HideGameMenu(bool forceHide = false)` | Menu open/close. |
| `PauseMenu.PauseMenuOpen` / `PauseMenuClose` | `private void PauseMenuOpen()` / `private void PauseMenuClose()` | Real pause menu open/close (not saves). |
| `DeveloperConsole.HandleInput` | `private void HandleInput(string input)` | Add commands. `Print(string)` is public. |
| `InputManager.GetKeyDown` / `GetKey` / `GetKeyUp` | `public bool GetKeyDown(string key)` etc. | Rebind or suppress an action. These are on hot paths (called tens of times per frame), so keep the patch tiny. |

### Persistence

| Member | Signature | Use |
|---|---|---|
| `SaveController.SerializeData` / `DeSerializeData` | `public static bool SerializeData(UnityEngine.Object data, bool global = false)` / `public static bool DeSerializeData(UnityEngine.Object data, bool global = false)` | Called per saved type; patching these can intercept every object's save. |
| `SavableScript.OnSavingGame` etc. | `public virtual bool OnSavingGame()` ... | Virtual, so patch the subclass, not the base. |

Patch targets that are `IEnumerator` (`LoadGameWithMigration`, `ChangeLevelWhenReady`, `LoadSaveGameDifferentScene`) need a prefix/postfix on the creation call, or a transpiler on the generated `MoveNext` of the compiler-generated class. For "load finished" use the `LoadingDone` event, not a patch.

---

## 10. Gotchas

1. **`SaveController.Loading` is not "a load is in progress".** `LoadGameWithMigration` starts `LoadSaveGameDifferentScene` and then sets `Loading = false` at the end of its own body (`SaveController.cs:1732`), long before the scene has loaded. It is true only during save-file reading/migration and the wait at the start of `ChangeLevelWhenReady`. For a true "level is loading" flag, set your own between `PlayerWillLoadGame` and `LoadingDone`.
2. **Exceptions in `LoadingDone` (and `SavingStarted`, `SavingDone`, `LoadingStarted`, `PlayerWillChangeLevel`) are not caught.** They propagate into the SaveController coroutine. A throw in `LoadingDone` skips `LoadingScreen.Fade(false, ...)`, leaving a black screen forever; later subscribers also don't run. Wrap every handler in try/catch. Only `PlayerWillLoadGame` swallows errors.
3. **`LoadingScreen.Active` is useless.** It is `Canvas.enabled`, which `LoadLevel` and `Fade` set to true and nothing sets back to false (`LoadingScreen.cs:125, 258`). As a result `Changelevel.ChangeLevelActive` is effectively just `changeLevelDelayActive`. For "screen is visible" read the `CanvasGroup` alpha and `blocksRaycasts`; `FadeActive` and `Fading` only cover the `Fade()` path, not `LoadLevel`.
4. **A level change always saves first and reloads from the save**, even to the same scene. Anything that is not in a `SavableScript` (or a DDOL object) is gone. Dead NPC state, spawned objects, mod-added components on scene objects: all lost unless serialized. Your mod's state needs its own persistence, hooked to the callbacks in 3.2, and must tolerate being saved with the mod absent later (unknown types in `GlobalState.data` untested).
5. **Level changes are Single loads of a completely new scene.** Every `GameObject` you added to the scene is destroyed. Re-apply patches' per-scene state in `LoadingDone` or in postfixes on `Awake`.
6. **Entry teleport happens after `LoadingDone`.** `MovePlayerToEntrypoint` waits 0.1 s real time after `LoadingDone` (`:834`). Anything that records the player position at `LoadingDone` after a door transition gets the pre-teleport position.
7. **Static events outlive subscribers.** If a scene object subscribes to `TimeOfDayAzure.MinutePassed` or `SecondsPassed` and is destroyed without unsubscribing, the delegate stays and throws (caught and logged per invocation for time events; not for SaveController events). `TimeOfDayAzure.DebugDelegateCount()` (`:369`) logs subscriber counts. A leaked mod handler holding a destroyed Unity object will keep firing, so null-check `this`.
8. **`SecondsPassed` fires almost every frame** at normal speed: 60 in-game seconds per real second means a new second at ~60 Hz, with the `deltaTime` argument usually 1. During sleep (speed factor) it fires with larger deltas. Keep handlers trivial; use `MinutePassed` (~1/s) or `DayChanged` where possible.
9. **`GameIsPaused` / `timeScale 0` also mean "saving".** Do not treat them as "pause menu open". Check `PauseMenu.instance.PauseIsActive()`.
10. **`TimeAndDay` oddities:** the constructor `TimeAndDay(float seconds, float minutes, float hours)` assigns `minutes` and `hours` from `seconds` (`TimeAndDay.cs:57`), so use object initializers. `GetMonthNameShort` maps month 8 and month 10 to "Sep". `weekDay` is 0 = Monday. `IncreaseDayCounter` special-cases `GetDay() == 1` by calling `SetDate(year, month, 3)` (`TimeOfDayAzure.cs:539`); do not assume the calendar is a plain Gregorian count.
11. **`WeatherManager` rain/wind accessors log.** `LightRainIntensity()` and siblings call `MonoBehaviour.print` whenever the value is above zero (`WeatherManager.cs:244+`). `RainIntensity()` calls three of them. Calling these per frame floods the log and the dev console (which captures Unity log output).
12. **`QuitDesktop` kills the process**, so no quit callbacks run (5.4).

---

## 11. Performance notes

- Per-frame hot spots that a mod can affect: `GameController.Update` (dispatch, ~210 lines of branches, dozens of `InputManager.GetKeyDown(string)` calls per frame, each resolving the binding and checking keyboard and controller codes); `InputManager.OnGUI` (runs per GUI event, so multiple times a frame); `InteractObjects.Update` (a raycast every frame, plus an `OverlapSphere` in third person, `InteractObjects.cs:107, 250`); `ThirdPersonCameraController.LateUpdate`; `WeatherManager.Update`; `TimeOfDayAzure.Update` (calls `timeController.SetNewDayLength(actualSpeed)` every frame).
- The time events are the main fan-out. About 35 files subscribe to `SecondsPassed` (storages, spawners, animals, grow plants, batteries, shops, `PlayerStats`, `Inventory`, `TenementController`, `Flashlight`, ...), 14 to `MinutePassed`, 13 to `CurrentTimeAndDay`. Every scene object of those types adds a delegate invoked at frame rate, so a mod that replaces or adds to this should do so as a coarse-grained timer rather than another `SecondsPassed` handler.
- `SaveController.SaveGame` and the load path run synchronously on the main thread (the save itself is a stall of whatever length the JSON serialization takes), with `timeScale` at 0. A big save is a visible hitch; don't add heavy work to `SavingStarted/SavingDone` or the `OnSaving*` callbacks.
- `SaveController.GetObjectByGUID(string guid, Type typ)` (`:1330`) uses `FindObjectsOfType` plus reflection per object. Do not call it per frame.

---

## 12. Appendix: quick lookup

The scene list is stored as an Int32 count followed by .NET `BinaryWriter` strings (7-bit length prefix), so a ten-line script reads it.

Quick map from section 5 to the code a framework would touch first:

| Question | Answer |
|---|---|
| Am I in a gameplay scene? | `GameController.instance != null` |
| Did a level change start? | `SaveController.PlayerWillChangeLevel` |
| Is any load starting? | `SaveController.PlayerWillLoadGame` |
| Is the new scene's data in place? | `SaveController.LoadingDone` |
| Has the player been placed? | postfix `SaveController.MovePlayerToEntrypoint` |
| Did a day pass? | `TimeOfDayAzure.DayChanged` |
| Back at the main menu? | `SceneManager.sceneLoaded` with name `Main Menu` |
| Are controls locked? | `GameController.keysDisabled` / `OpenSewerCharacterController.ControlsDisabled()` |
| Real pause menu open? | `PauseMenu.instance.PauseIsActive()` |
