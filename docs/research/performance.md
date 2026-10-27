# Obenseuer performance notes

Static read of the decompiled game code plus the engine config and the two Player.log files. Nothing here was measured in a running game, so every cost below is an estimate from reading code. Where a number depends on a scene value I could not read (serialized in the level files), I say so. Paths are relative to `decompiled/Assembly-CSharp/` unless they start with `../` (firstpass).

Short version: the code is mostly fine per frame. The expensive things are (1) a handful of event handlers that run about 60 times a second because of how in-game time is dispatched, (2) one per-frame reflection+LINQ path on the crosshair interaction, (3) a lot of per-light and per-portal work that scales with scene size, and (4) GPU cost from the way the quality options map onto shadows, LOD bias and volumetric fog. Save/load is already reasonably tuned by the devs.

## 1. Engine and config

`Obenseuer_Data/boot.config`:

```
gfx-enable-gfx-jobs=1
gfx-threading-mode=6
wait-for-native-debugger=0
hdr-display-enabled=0
gc-max-time-slice=3
single-instance=
```

- `gc-max-time-slice=3` turns on the incremental GC with a 3 ms slice. Unity on Mono uses the Boehm collector, which is non-generational. The incremental mode spreads marking across frames but a fast allocator can still force a full blocking collection. At 144 Hz (6.9 ms frame) a 3 ms slice is nearly half a frame while a collection is in progress, so a smaller slice trades longer collection for flatter frames. This is also settable at runtime from a mod with `UnityEngine.Scripting.GarbageCollector.incrementalTimeSliceNanoseconds`, which is easier to experiment with than editing boot.config. Reducing allocation (sections 4 to 6) beats tuning this.
- `gfx-threading-mode=6` is the "split jobs" mode. Player.log says: `kGfxThreadingModeSplitJobs is not supported on Direct3D 11. Reverting to kGfxThreadingModeClientWorkerJobs instead.` So the setting is silently downgraded to client worker jobs on every start. No loss, but changing it to the mode the engine falls back to would remove the message. Not a perf item.
- `gfx-enable-gfx-jobs=1` is already on. Nothing to gain.
- `hdr-display-enabled=0`: fine.
- Backend is Mono (JIT), not IL2CPP. First call of any method JITs it, so first-use hitches exist for every code path (first door open, first save, first dialogue). Harmony patches are also JIT'd at patch time. A warm-up pass at load (calling hot methods once) is a possible but low-value mod.
- Renderer is Direct3D 11 feature level 11.1, which under Proton is DXVK on RADV (RX 7900 XT, 20224 MB reported). Shader pipeline compilation stutter on first sight of a material is a DXVK/driver concern, not game code.
- The game is on the built-in render pipeline with image effects: Beautify (`BeautifyEffect/Beautify.cs`, `OnRenderImage` at line 3323), Hx volumetric fog (`../Assembly-CSharp-firstpass/HxVolumetricCamera.cs`, `EventOnRenderImage` at line 2108), and Unity PostProcessing v2 (`PostProcessLayer`, referenced in `OptionsMenu.cs:1335-1347`). Several full-screen blits per frame, all at the camera resolution (2560x1440 for this user) unless the effect downsamples.
- `DisplaySave.ApplyFrameRateLimit` (`DisplaySave.cs:90-93`) sets `Application.targetFrameRate` to the preferred refresh rate (144 in Display.json). With VSync on (`QualitySettings.vSyncCount = 1`, `OptionsMenu.cs:719-723`) the engine ignores targetFrameRate, so it is harmless. If a mod exposes a frame cap with VSync off, this is the property to set.

Player settings files in `LocalLow/Loiste Interactive/Obenseuer/`:

- `Quality.json`: `qualityS 6, shadowQ 3, shadowR 3, textureR 0, SSAO 3, antialasing 0, postFX 2, custom false`. This is the top of every slider. See section 8 for what that maps to.
- `Display.json`: 2560x1440, Borderless, VSync on, fov 90, brightness 100. The `language` field reads "Full Screen" and `screenMode` reads "Borderless"; the order of the saved fields looks off but is not a performance matter.

I could not read `globalgamemanagers` (38 MB, serialized Unity assets, no tool installed). That is where `Time.fixedDeltaTime`, physics settings and the default quality level definitions live. Worth checking with AssetStudio or UABEA: if the fixed timestep is 0.02 (default), `FixedUpdate` runs 50 times a second, which matters for section 5.

## 2. Player.log analysis

Both `Player.log` and `Player-prev.log` are about 4.5 KB and stop at the main menu. There is no gameplay in either, so there are no GC lines, no frame timing, no load times for a world, and none of the game's own `Debug.Log` output (the game logs `Saving game!`, `Time: Game ... saved! (Ns)` and `Loading game:` which would be useful once there is a play session). Everything below is startup only.

What is in there:

- Engine `6000.0.77f1`, Mono path under `S:/games/Obenseuer/...`, Windows.Gaming.Input, PhysX 4.1.2 multi-threaded.
- `Loaded All Assemblies, in 1.023 seconds` in the newer log and `0.213 seconds` in the older one. A 5x difference for the same build points at cold file cache or Wine file system latency (or the mod loader doing work: the install has `Mods/` and `ObenLoader/` next to the exe). It is a one-time cost per launch.
- `The referenced script on this Behaviour (Game Object 'Quality') is missing!` once per start. A component on the boot-scene `Quality` object has lost its script. Harmless, but it means whatever that script did is not running (possibly a leftover from the quality system that now lives in `OptionsMenu`/`QualitySave`).
- `MiscAudioPlayer defaultMixerGroup was not set!` once. `AudioSourceMaker.ResolveMixerGroup` (`AudioSourceMaker.cs:21-40`) already has a fallback that loads `Sounds/MainMixer` from Resources.
- `DontDestroyOnLoad only works for root GameObjects or components on root GameObjects.` once. Benign.
- The CSV header line `Key,Id,Chinese (Simplified) (zh-Hans),...` is printed once. It is a localization table dump (`MenuLocalization.cs`/`ExportStringTables.cs`), a single log line.
- `Curl error 7` to `config.uca.cloud.unity3d.com` and `cdp.cloud.unity3d.com`, three at startup and one more later in the newer log (two later in the older one). These are Unity Services / analytics endpoints failing to connect. In the older log two of them took 632 ms and 1028 ms to fail, in the newer log 3 to 15 ms. They run on background threads so they should not hit the frame time, but the repeats show something retries on a timer. Unity Services config is in `StreamingAssets/UnityServicesProjectConfiguration.json`. Blocking the hosts or disabling the services would stop the retries; no game code is involved.
- `DirectoryNotFoundException: Could not find a part of the path '...\Saves'` with a full managed stack trace, from `LoadMenu.Start -> Init -> GenerateCharacterList -> SaveController.GetCharacters` (`SaveController.cs:1239`). It is an unguarded `Directory.GetDirectories` on a first run with no Saves folder. It is thrown once per menu open when the folder is missing, then it stops once a save exists. Not a perf problem; mention only because it is the noisiest thing in the log.

Nothing repeats a lot. The log tells us nothing about steady-state performance, so the first useful thing to do is capture a log from a real session: Autosave timing (`Time: Game ... saved!`), anything with `stuck!` (`NPCController.CheckStuck`, `NPCController.cs:733`), and the `RefreshCubemapInTrigger` tag error described in section 6.

## 3. Census of per-frame code

Counting `void Update()`, `LateUpdate()`, `FixedUpdate()` and `OnGUI()` declarations, excluding BehaviorDesigner samples, Beautify demos, ECM examples, UnityStandardAssets and Sequencer commands:

- `Assembly-CSharp`: 221 methods (175 Update, 24 LateUpdate, 14 FixedUpdate, 8 OnGUI) in about 207 classes. Roughly 830 classes in the top-level folder derive from MonoBehaviour or one of the game's base behaviours, so about a quarter of them have per-frame code.
- `Assembly-CSharp-firstpass`: 40 methods (31 Update, 1 LateUpdate, 8 OnGUI), mostly Pixel Crushers Dialogue System and Hx volumetrics.
- 15 of those are empty bodies. Unity still calls an empty `Update`. They are: `Appearance.Update` (`Appearance.cs:255`) and `Appearance.LateUpdate` (`:1102`), `AnimationRigHelper.LateUpdate` (`AnimationRigHelper.cs:34`), `ECM.Controllers/OpenSewerCharacterAIController.LateUpdate` (`:234`), `UnityEngine.AzureSky/AzureWeatherController.Update` (`:112`), and `Update` in `FootStepsAltTrigger`, `NotificationTrigger`, `SceneActivityNodes`, `SoundscapeAudio`, `ComputerProgramPanel`, `SoundSettings`, `PajatsoPlayCoin`, `Compass`, `Trailer`, `StorageItemSpawnDatabase` (all line 9). `Appearance` and the AI controller exist once per character; `FootStepsAltTrigger` is a level trigger and probably numerous. Each empty call costs a native-to-managed transition, well under a microsecond. Even a few thousand of them are not worth a patch. Harmony cannot remove a Unity message method after the type is loaded anyway; the way to stop the calls is `component.enabled = false` on the instance.

Classes with an `Update` that probably exist in large numbers in a scene (from what they are, since instance counts are in the scene files I could not read):

| Class | Where | What it does per frame |
| --- | --- | --- |
| `Door` | `Door.cs:866` | Idle doors take the cheap branch (a few comparisons). Moving doors write `localEulerAngles`. Fine. |
| `LightController` | `LightController.cs:238` | Staggered distance check, see section 6. Every light in the game has one. |
| `Areaportal` | `Areaportal.cs:51` (LateUpdate) | Distance, colour lookup, material colour set. Not staggered. Section 6. |
| `LightFlicker`, `PowerLightFlicker` | `LightFlicker.cs:124`, `PowerLightFlicker.cs:138` | `PowerLightFlicker` calls `SetIntensity(GetFlickerIntensity())` every frame; `LightFlicker` uses a coroutine that reassigns `Renderer.materials` per flicker (`LightFlicker.cs:155`, `:173`), which allocates a material array. |
| `LightTurnOffOn`, `LightTurnOffOnMultiState`, `CandleLight`, `GradientLamp`, `LavaLamp`, `DecorationLights` | various | Fades and patterns, mostly gated on state. `LightTurnOffOn.Update` (`:235`) is a per-frame call that returns quickly when no fade is active. |
| `TelevisionNoise` | `TelevisionNoise.cs:192` | Two `Random.Range` and a `mainTextureOffset` write per active TV per frame; `SetColor` on a counter. |
| `Billboard` | `Billboard.cs:16` | `renderer.isVisible` then a rotation write. Cheap. `Camera.main` is only used when the cached camera is null. |
| `Animal` and subclasses (`Chicken`, `Cat`, `Pig`, `Rat`) | `Animal.cs:357` | About 100 lines of state logic plus two `Animator.SetBool/SetFloat` calls and `ObstacleAvoidance.Tick` (which throttles its probe to every 0.2 s via `checkTimer`, `ObstacleAvoidance.cs:148`, and uses the NonAlloc physics calls). Reasonable. |
| `NPCController` | `NPCController.cs:238` | Activity tick plus `UpdatePathfinding`. Light. |
| `NPC_FOV` | `NPC/NPC_FOV.cs:73` | Coroutine start every 0.5 s per NPC plus a raycast. Section 7. |
| `FaceBlendShapeFollower` | `FaceBlendShapeFollower.cs:54` (LateUpdate) | Copies blend shape weights every frame. Section 7. |
| `PowerMeter` | `PowerMeter.cs:69` | String building each frame per meter. Section 8. |
| `Fish`, `OscillatingMovement`, `RotateObject`, `MaterialScroll`, `WastelandGlowMove`, `WastelandRock` | various | Pure transform or material animation. `MaterialScroll.Update` (`:35`) calls `obj.materials` get and set per frame when `materialIndex != 0` (`MaterialScroll.cs:47-49`). That clones the material array every frame. |
| `SurveilanceCamera` | `SurveilanceCamera.cs:117` (LateUpdate) | While `active`: distance, angle, then a `Physics.Raycast`. Fine unless there are dozens. |
| `PhysicCable`, `Cable_Procedural_Simple` | `PhysicCable.cs:241` | Per-frame transform updates per segment. |
| `Storage`, `ItemData`, `Growing` | various | Gated on state, cheap. |

Entirely absent from the Update list (a good sign): CollectibleItem (items lying around), NPCInfo, doors' locks, interactables as a base class, `Inventory`. Those work by events.

## 4. Per-frame hot spots (always on while playing)

### 4.1 Crosshair interaction: reflection and LINQ every frame

`InteractObjects.Update` (`InteractObjects.cs:107`) runs every frame. In first person it calls `RaycastInteract()`, which does a single `Physics.Raycast`. That is fine. The problem is what happens when the ray hits a usable object (`IsUsableLayer` and within use distance): it calls `GetInteractable(raycastObject)` every frame (`InteractObjects.cs:387`).

`GetInteractable` does:

1. `current.GetInterfaces<IInteractable>()` (`GameObjectExtensions.cs:7-16`). That is `GetComponents<MonoBehaviour>()` (allocates an array), then for every behaviour `a.GetType().GetInterfaces()` (reflection, allocates a `Type[]` every call on Mono) and `.Any(k => k == typeof(T))` with a closure, then `ToArray()`. All of it LINQ with iterator and closure garbage.
2. `interfaces.Where(c => !c.IsInteractDisabled()).FirstOrDefault()` (another iterator and closure per frame).
3. `UseText.instance.ChangeUseText("(Use)")`, then `interactable.UpdateUseMessage()`, which on the 59 `UpdateUseMessage` overrides builds strings and calls `UseText.ChangeUseText(...)` (`UseText.cs:76-110`). That method does `SetActive`, sets two TMP texts, and concatenates `"(" + UseText + ")"` every frame when the text does not start with `(`.
4. `TryGetComponent<IQuickSlotTarget>` and for plants `GetComponentInParent<Growing>()`.

Cost: a few hundred bytes to a few KB of garbage per frame and a handful of native calls, every frame, whenever the player is looking at anything interactive. That is most of the time in a base. It is also easy to cache: the set of `IInteractable` components on a given GameObject does not change from frame to frame.

Third person mode is worse: `ThirdPersonInteract` -> `GetInteractablesWithinSphere` (`InteractObjects.cs:245-345`) runs `Physics.OverlapSphere` (allocating) every frame, `GetComponent<Interactable>()` per collider, up to three raycasts per candidate (`bounds.min/center/max`), a new `List` per call, and `list.Contains`. Only applies if third-person is used.

### 4.2 `Inventory.AllSlots()` allocates every call

`Inventory.AllSlots` (`Inventory.cs:170-185`) builds its result with `Slots.Concat(...).Concat(...).ToArray()`. It allocates an array plus LINQ iterators on every call and is called from 52 places (`CheckForCategoryItem`, `GetItemAmountInInventory`, `GetAllSlotsWithItemWithMeta`, `UpdateAllSlotsNow`, Lua dialogue helpers and so on).

The per-frame caller is `ShowTime.Update` (`ShowTime.cs:39`). When the inventory panel is visible it calls `Inventory.CheckForCategoryItem("Clock Calendar", ...)` and `("Clock", ...)` each frame, and if neither is found it calls `GetAllSlotsWithItemWithMeta<ItemConnectivityData>()` which calls `AllSlots()` again plus a new `List`. When the clock is shown it also runs `UpdateTime` (`:86`), which uses string interpolation and concatenation each frame and assigns the TMP text. So: up to 3 array builds, several strings and a list per frame while the inventory is open. The clock string only changes once per game minute (about once per real second at the default day length).

### 4.3 `TimeOfDayAzure.UpdateTimers`

`TimeOfDayAzure.Update` (`TimeOfDayAzure.cs:396`) calls `UpdateTimers()` (`:501`) every frame, and that starts with `timers.ToArray()`. One array allocation per frame, sized by the number of active timers (about 60 files reference `TimeOfDayAzure.Timer`, NPC activities among them). `deltaTime` is zero on most frames (it only changes when the integer second changes), so the loop mostly does nothing but allocate.

### 4.4 `LightsController.FixedUpdate` and `LightCullZone.CheckIfPlayerInZone`

`LightsController.FixedUpdate` (`LightsController.cs:103`) loops every `LightCullZone` and calls `CheckIfPlayerInZone` (`LightCullZone.cs:340`) until one returns true. That method uses `zones.Any(x => IsWithinBoxBounds(...))` and `zones.All(...)`. Each call allocates a closure and delegate (captures `this`) and an enumerator. This is every physics tick, for every zone before the matching one. The zones are indoor/outdoor light boxes, so a level can have dozens. The result only changes when the player moves across a box edge, so it can run at 5 to 10 Hz without anyone noticing, or run as a plain `for` loop with no allocation.

### 4.5 `Areaportal.LateUpdate` per portal per frame

`Areaportal.cs:51-99`. Every portal, every frame:

- `Vector3.Distance` to the player.
- When `useAmbientColor` (the default), `WeatherManager.instance.CurrentOutsideAmbience()` (`WeatherManager.cs:163`). That does `ActiveWeather().FindProperty("Environment Ambient Color")`, a linear scan with string comparison over the weather profile's property list (`UnityEngine.AzureSky/AzureWeatherProfile.cs:35-45`), then evaluates a `Gradient` with the current time/sun/moon values. The value is the same for every portal in the same frame.
- `material.color` read, then possibly written. `Start` calls `GetComponent<Renderer>().material` (`:48`), which instantiates a material per portal.

So per frame cost is N portals times (string scan + gradient evaluation + material property get). Caching the ambient colour once per frame, or once per 0.25 s, and skipping all of it when the portal is far away and its state has not changed, removes nearly all of it. The `onOcclusionUpdated.ForEach(delegate...)` calls only run on state change.

### 4.6 `AzureEnvironmentController.Update`

`UnityEngine.AzureSky/AzureEnvironmentController.cs:39-62`. Every frame it writes five `RenderSettings` ambient values (`ambientIntensity`, `ambientLight`, `ambientSkyColor`, `ambientEquatorColor`, `ambientGroundColor`) even when unchanged. Setting `RenderSettings` ambient values marks the ambient probe dirty; depending on the ambient mode it can force a spherical harmonics update. Guarding the writes with an equality check is free. The same method re-renders the sky reflection probe (`reflectionProbe.RenderProbe()`, `:52` and `:59`) every frame or every `refreshInterval` (default 2 s) with `timeSlicingMode` left at `NoTimeSlicing` by default (the field at `:15` is never applied to the probe in code, only serialized). Which refresh mode the scene uses is a serialized value I cannot read. If it is `ViaScripting` with the default interval, there is a full 6-face probe render every 2 seconds, which is a periodic hitch. If it is `EveryFrame` it is a continuous cost.

Related: `AzureWeatherController.FixedUpdate` (`AzureWeatherController.cs:116`) calls `EvaluateWeatherProfiles()` every physics tick, and `AzureTimeController.FixedUpdate` (`:274`) calls `ComputeCelestialCoordinates()` and `SetDirectionalLightRotation()`. Weather profile evaluation and sun position at 50 Hz is more than needed; once per frame (or at 10 Hz) is enough. These only matter if the fixed timestep is the default 0.02 s, and at high FPS they run less than once per frame.

### 4.7 Smaller per-frame allocations

- `CatEars.Update` (`CatEars.cs:45`): `TurnNearCatsTowardsPlayer` each frame, and `new List<Animal>(lookStates.Keys)` plus `followingAnimals.Contains` on a timer (`scanInterval`). Only while the item is worn.
- `FPSDisplay.OnGUI` (`FPSDisplay.cs:12`): `new GUIStyle()` and a formatted string per OnGUI event (two to three per frame). Only if the counter is enabled.
- `KeepRectTransformOnscreen.LateUpdate` (`../Assembly-CSharp-firstpass/PixelCrushers/KeepRectTransformOnscreen.cs:46`): `new Vector3[4]` per frame; used on dialogue UI.
- `InputManager.OnGUI` (`InputManager.cs:133`) exists solely to catch key events through IMGUI. Having any `OnGUI` active makes Unity run the IMGUI layout and repaint passes every frame. Cost is small (tens of microseconds) but permanent. Replacing it is risky because it exists to handle keyboard layouts, so leave it.
- `Camera.main` is used in `Billboard`, `MoveCamera`, `RelayLookAt` and Pixel Crushers `Selector`/`AlwaysFaceCamera`. It is cached by the engine in Unity 6 and costs next to nothing. Skip.
- `GunShoot.Update` has one raycast and only exists when armed.

## 5. The in-game time system fans out about 60 times a second

This is the most important structural finding, and it is easy to miss because it is not in any `Update`.

`TimeOfDayAzure.Update` -> `CalculateDeltaTime` (`TimeOfDayAzure.cs:442`) computes whole seconds from the Azure timeline each frame. When the integer second changes it calls `UpdateCurrentTimeAndDay`, which fires `SecondsPassed?.Invoke(deltaTime)` (`:464`). The default `dayLenghtInMinutes` is 24 (`TimeOfDayAzure.cs:203`), so `gamesecondsmultiplier = 1440 / 24 = 60` (`:269`): one real second is 60 game seconds. The integer second changes on roughly 40 percent of frames at 144 FPS and nearly every frame below 60 FPS. When the player sleeps or waits the speed factor goes way up, and the event still fires once per frame with a large `sec`.

So every `TimeOfDayAzure.SecondsPassed +=` subscriber runs about 60 times a second. There are 35 of them (grep `SecondsPassed +=`): `Inventory`, `PlayerStats`, `WindowNaturalLight`, `ReflectionProbeUpdater`, `Animal`, `AnimalStats`, `AnimalFoodDetector`, `Chicken`, `Fishspawner`, `grow` (every plant), `Spawner`, `Fuel`, `BatteryPowerSource`, `Sauna`, `InteractableShower`, `LiquidStorage`, `Dosimeter`, `ShopBaseUI`, `ContestController`, `TenementEventController`, `CharacterSlotController`, `Prison`, `WorkableResourceSource`, `InteractableItemGiver`, `InteractableTicketMachine`.

`MinutePassed` fires once per game minute, about once per real second, which is a sensible rate. Most subscribers accumulate `sec` into a counter, which is fine, but a few do real work every call:

1. `Inventory.DeltaSeconds` (`Inventory.cs:303`). If the inventory panel is active in the hierarchy it calls `UpdateAllSlotsNow()` (`:332`): `AllSlots()` (array alloc, see 4.2) plus `SlotController.UpdateNow` -> `Slot.UpdateSlot` (`Slot.cs:297`) on every inventory, backpack, character and foreign slot. `UpdateSlot` calls `CheckItemMetas` (perishing), `ItemData.ChangeItemData`, and `ItemData.UpdateItem` (`ItemData.cs:222`), which toggles five GameObjects, assigns `stackAmountText.text = amount.ToString()` (a string alloc), `duralibityText.text`, and sets the sprite, then walks the meta list. With 30 to 80 slots that is a couple of thousand UI property writes and several thousand allocations per second, plus Canvas rebuilds because the texts and sprites are being touched. It runs only while the inventory screen is open, but that is when the player is staring at a UI that is rebuilding 40 to 60 times a second. The perishing logic inside is timestamp based (`ItemPerishabledata.CountPerishingTime` uses `currentTimeAndDay.TotalMinutes - lastChecked.TotalMinutes`, `OS.Items/ItemPerishabledata.cs:47-90`), so calling it less often changes nothing except how soon a spoiled item turns into its spoiled form on screen. This is the best single target in the project.
2. `PlayerStats.DeltaSeconds` (`PlayerStats.cs:306`). Every call runs `alcohol.UpdateStats`, `duck.UpdateStats`, `sauna.UpdateStats`, `ProcessItemSpecialEffects()` and `BarUpdate()`. `ProcessItemSpecialEffects` begins with `new List<...>(unprocessedSpecialEffects.Keys)` (`:264`), so it allocates a list 60 times a second even when the dictionary is empty. `BarUpdate` loops `playerStatsUIs` calling each `PlayerStatsUI.BarUpdate`. The big stat updates are already gated to 60 accumulated seconds (`LocalSeconds > 59`).
3. `WindowNaturalLight.DeltaSeconds` (`WindowNaturalLight.cs:174-196`) runs `UpdateLights` whenever `timeUntilNextCalc < 1`, and `timeUntilNextCalc` resets to the inspector value `Resolution` (in game seconds). If `Resolution` is small it runs on every event. `UpdateLights` (`:200-233`) calls `WeatherManager.CurrentOutsideAmbience()`, assigns `targetLight.color` for every window light, and then runs `renderer.SetPropertyBlock(propertyblock)` for every window renderer on every call. Note the property block is only modified inside the `textureModifyNext <= 0` branch, but the renderer loop is outside it (`:222-229`), so it re-uploads an unchanged block to every window renderer each time. `Resolution` is serialized so I cannot say the rate; moving the `foreach` into the `if` is a no-risk change either way.
4. `ReflectionProbeUpdater.DeltaSeconds` (`ReflectionProbeUpdater.cs:50-57`) triggers a time-sliced reprobe every 300 game seconds, which is about every 5 real seconds. It already time-slices by face (`IndividualFaces`, one probe at a time with a 30 frame wait). That is well designed, but at 60x speed it re-renders every probe in the scene about every 5 seconds. Whether that matters depends on the probe count. `Awake` calls `RenderAllProbes()` (`:28`) so each scene load renders every probe fully in one go.
5. `grow.DeltaSeconds` (`grow.cs:564`) accumulates to 60 s before doing work. Fine. `Fuel`, `Animal`, `Spawner` just subtract a counter. Fine.

The general fix is to deliver `SecondsPassed` to subscribers in batches (for example at 10 Hz, summing the deltas), which is compatible with every subscriber I looked at because they all take the elapsed `sec` as a parameter and accumulate it. It is a larger change than patching one handler, so the ranked list starts with the specific handlers.

## 6. Lighting, culling, volumetrics

### 6.1 `CullingController`: O(n squared) setup and O(n) moves

`CullingController.cs` backs `LightController` (`CullingType.Light`), `FadeGameObject` (NPC type by default) and `Cull_light`. The set of culling spheres is stored as a `List<CullingItem>` and mirrored into a `BoundingSphere[]`. Problems:

- `AddToCullingGroup` (`:51-64`) rebuilds the whole array with `Select(x => x.bounds).ToArray()` and calls `SetBoundingSpheres` on every add, and starts a `CheckIfVisibleLate` coroutine (`WaitForEndOfFrame`) per add. Every light calls this from `LightController.Start` (`LightController.cs:171-175`). With n lights that is n array rebuilds of average size n/2 and n coroutines on scene load. A scene with 1500 lights does over a million element copies and 1500 allocated arrays, which is a load-time cost and a heap-fragmentation cost.
- `RemoveFromCullingGroup` (`:80-95`) does `IndexOf` plus a full rebuild, once per light destroyed (scene unload, furniture removal).
- `UpdatePosition` (`:97-107`) does `IndexOf` on the list, which is O(n), and it is called every frame for every moving light (`LightController.cs:268-273`, `moving || movingInSingleZone`).
- `cullingGroups`/`boundsArrays` are `Dictionary<CullingType, ...>` keyed by an enum. On older Mono runtimes enum dictionary keys box on lookup. Unity 6's Mono has fixed most of this, so treat as unverified.

A pre-sized array with capacity doubling, `SetBoundingSphereCount` for the live count, and a stored index per item (swap-remove and fix the moved item's index) turns all three into O(1). `CullingGroupEvent.index` is the index into that array, so any scheme must keep the item list and the sphere array in the same order.

### 6.2 `LightController.Update`

`LightController.cs:238-400`. This is already staggered: a random frame offset 0..8 chosen in `Awake` (`frame = Random.Range(0, 9)`, `:134`), then the distance work only runs when `framecount == frame`, or when `SmoothTransition` or `forceUpdate` is set. It also returns early for culled lights and ignores distance changes under 0.05. Good design. Where it still costs:

- Lights with `SmoothTransition` do the distance and intensity work every frame, plus volumetric intensity and enable flips.
- The `disabled` branch (`:240-262`, set by `LightsController.DisableAllLightControllers`/`ShopBaseUI.cs:208`) re-applies about six light properties through native getters each frame, forever. It could apply once and stop.
- `LightController.Start` (`:138-176`) loops every `LightCullZone` and every bounds box for every light (L x Z x B), at load.
- Shadow changes at runtime: crossing `ShadowOffDistance` toggles `cullLight.shadows` and `volumetricLight.enabled`, and `UpdateShadowResolution` (`:402`) changes `shadowResolution` by distance. Changing a light's shadow resolution forces a shadow map re-allocation. Doing this by distance bands rather than continuously is what the code already does, but the band edges are per light and not hysteresis-protected beyond the 0.05 tolerance.

The per-light tuning fields (`TurnOffDistance`, `ShadowOffDistance`, `ShadowFullResolutionDistance`, `ShadowLowestResolutionDistance`, `DynamicShadowResolution`, `NoShadows`) are public fields set in the editor, so a mod can multiply them at `Start` to trade quality for GPU time. That is the cheapest big GPU lever (section 8).

### 6.3 `WindowNaturalLight`

Covered in section 5.3. Also: `BuildWindowsList` (`:76`) does `FindObjectsByType<Renderer>` for every renderer in the scene at `Start`, then `TryAddRenderer` tests each with `CompareTag` and `GetSharedMaterials`. One-time load cost proportional to the scene's renderer count, and it is repeated per scene since the object is per scene. `AppendWindowsList` (called from `TenementGeneralPrefabs.cs:300`) does a `GetComponentsInChildren<Renderer>` per spawned prefab. `HasObstruction` (`:167`) uses `Physics.RaycastAll` (allocating) and is called per window light per plant checker through `NaturalLightSourceChecker.HasSun` (`NaturalLightSourceChecker.cs:78`), refreshed through the `SourcesChanged` event whenever a window light is added or removed, which is a load-time burst (coalesced by the `check` coroutine).

### 6.4 Volumetric fog and post effects

- Hx volumetrics are on `HxVolumetricCamera` (public fields at `../Assembly-CSharp-firstpass/HxVolumetricCamera.cs:271-405`): `resolution` (default `half`), `SampleCount = 4`, `DirectionalSampleCount = 8`, `MaxDirectionalRayDistance = 128`, `MaxLightDistance = 128`, `TemporalSampling = true`, `ShadowFix = true`, `NoiseEnabled`, `TransparencySupport`, `ParticleDensitySupport`, `densityResolution`, `blurCount`, `UpSampledblurCount`. The game only ever changes `resolution` (via `EffectsController.ChangeHxVolumetricResolution`, `EffectsController.cs:28`) and the enable flag. All the other knobs stay at the prefab values and are free to tune.
- `HxVolumetricCamera.Update` (`:1407-1433`) is cheap per frame except `ShadowFix`, which does a `Graphics.DrawMesh` of a large box every frame to work around a shadow caster issue. Turning `ShadowFix` off saves a draw but may reintroduce the artifact the fix exists for.
- Each `LightController` can have an `HxVolumetricLight`; turning `volumetricLight.enabled` off is what the distance logic already does beyond `ShadowOffDistance`. Because the light cost is per light in view, scaling `ShadowOffDistance` also scales the volumetric cost.
- `Beautify` (`BeautifyEffect/Beautify.cs`) is a heavy image effect at `BestQuality`. Its `LateUpdate` (`:3095`) is cheap. `UpdateMaterialPropertiesNow` (`:3872-4140`) has one trap: when `_sunFlares` is on and `_sun == null` it runs `FindObjectsOfType<Light>()` (`:4111`) each time it is invoked. That is every property change, so it is slow in interiors that have no directional light if a script is also changing Beautify properties each frame. `SMVEffects` (mushroom effects, `SMVEffects.cs:257-332`) does that with `WaitForSecondsRealtime(0.001f)` loops that call `EffectsController.ChangeFlaresEffect` and `ChangeVignettingFade`, so Beautify rebuilds its material state several hundred times a second during mushroom trips. It is an effect-only case.
- `cakeslice/OutlineEffect.cs` (`OnEnable`, `:240`) does `FindObjectsOfType<Outline>()` on enable only.
- `RefreshCubemapInTrigger` (`RefreshCubemapInTrigger.cs`) renders a reflection probe every frame while `update` is set, but the tag check is `other.CompareTag("Player)")` with a stray parenthesis (`:21`, `:29`). `CompareTag` with an undefined tag logs an error (and throws in the editor), so each trigger enter/exit on those volumes probably writes an error line to Player.log and never enables the update. Whether it is ever hit in play is unknown. Fixing the tag would turn on a per-frame `RenderProbe`, so leave it.

## 7. AI, NPCs and animals

How NPCs work: not BehaviorDesigner. `BehaviorDesigner.*` is the stock library and only `BehaviorManager_DialogueSystem` plus the movement tasks are referenced; the NPCs run on a custom `Activity` system (`NPC/Activity.cs`, `Act_Wander`, `Act_Patrol`, `Act_Police`, `Act_Robber`, `Act_Follow`, `Act_Sleep`, `Act_sit`, `Act_Stand`, `Act_Tenement`) driven by `NPCController.Update` (`NPCController.cs:238`) and `NPCDirector`/`Schedule`. Movement uses the A* Pathfinding Project (`AstarPath`, `RichAI`/`AIPath` through `IAstarAI`), with precomputed graphs loaded from `StreamingAssets/NavigationData/*.nav` (108 files, the biggest being `Open Sewer Mines` 1.7 MB, `Under Map` 1 MB, `Open Sewer Industrial` 0.7 MB). The world is split into many scenes (about 124 level files, most of them interiors), so NPC and light counts per scene are bounded by the scene, not the world. The big ones are the exterior `Open Sewer *` scenes and a few large interiors (level112, level111, level2 are the largest scene files at 214, 193 and 150 MB).

`AstarPath` settings (thread count, graph update frequency) are serialized in scenes so I cannot read them. If A* is running with zero threads (path processing on the main thread), path searches show up in frame time; worth checking in the editor or with a profiler.

Findings:

- `NPC_FOV.Update` (`NPC/NPC_FOV.cs:73-96`): per NPC, every `cooldown` (0.5 s) it starts a `DetectionCooldown` coroutine (an enumerator plus `WaitForSeconds` plus a `Coroutine` object), runs the player check (distance, angle, one raycast), copies `targetsToDetect` with `.Copy()`, and does `RemoveAll` with a closure. It also evaluates `NPCsDetected.GetInvocationList().Length` (allocates the invocation array) before checking NPCs. A float timer instead of a coroutine removes the coroutine garbage. There is also a bug: `CheckIfNPCsAreDetected` (`:108`) calls `npcInfo.GetComponent<NPC_FOV>()` on the NPC's own `npcInfo` inside a loop over all NPCs instead of `item` (`:116`), so it checks the same head position N times. Only `Act_Robber` subscribes to `NPCsDetected` (`Act_Robber.cs:173`), so this is rare in practice. Also `Random.Range(0, 1)` on integers (`:162`) is always 0, so the detection roll is deterministic.
- `Act_Wander.GenerateNewDestinationCO` (`NPC/Act_Wander.cs:50-66`), the same pattern in `Act_Police.cs:933-939`, `Act_Robber.cs:819-825` and `Animal.GenerateNewDestinationCO` (`Animal.cs:836-850`): build a `ConstantPath` (range 10000 for Wander/Police, 25000 for animals, in A* cost units where 1000 is roughly one world unit), wait for it, then call `PathUtilities.GetPointsOnNodes(path.allNodes, path.allNodes.Count)` and take one random element of the result. `GetPointsOnNodes` samples a random point on every node, so it does N random-point calculations and allocates N Vector3s to throw away N-1 of them. The fix is to pick a random node first and call `RandomPointOnSurface()` on it. The cost scales with the node count inside the radius and happens per wandering NPC and animal on each new destination (after `waitTime` of 0..120 game seconds, so it is common).
- `NPCController.UpdatePathToOtherMap` (`NPCController.cs:614`) calls `FindObjectsByType<Waypoint_LevelChange>(Include)` on each cross-scene path request. Infrequent.
- `NPC/Act_Sleep.cs:156` and `NPC/Act_sit.cs:144` call `FindObjectsOfType<Sleep>()` and `FindObjectsOfType<InteractableChair>()` when an NPC picks a bed or chair. Per NPC, per activity start, over the whole scene. Caching the list per scene removes it.
- `SaveController.GetObjectByGUID(string, Type)` (`SaveController.cs:1330-1340`) does `FindObjectsOfType(typ)` and then three `GetType().GetField("GUID")` calls per object, and it is used by the `NodeReference.node` getter (`NPC/NodeReference.cs:26`), `NodeGroupReference`, `SleepReference`, `InteractableChairReference`, `ActionReference` and the `SavableObjectReference.GetObject<T>` helper (`SavableScript.cs:20-44`, which does cache after the first lookup). The `NodeReference.node` getter does not cache: when `_node` is null and `_nodeGUID` is set, every access runs a scene-wide `FindObjectsOfType<Node>()`. There is a fast path (`GetObjectByGUID(string)`, `:1360`, a dictionary lookup in `Guids`) that is only used for loaded save objects. Checking `Guids` first (and caching the result in `_node`) would avoid the search.
- `NPCManager.FindNpcById` and `FindNpcControllerById` (`NPCManager.cs:141-157`) use `Where(...).ToList()` (and, in the first, `FirstOrDefault` twice). `NPCData.controller` (`NPC/NPCData.cs:45`) calls the second. Callers are dialogue, shops and tenement code, so this is event rate, not per frame. A dictionary by id is trivial but low value.
- `FaceBlendShapeFollower.LateUpdate` (`FaceBlendShapeFollower.cs:54-114`): once the mapping is built, `CopyWeights` loops every binding and does two native `GetBlendShapeWeight` calls per binding per frame, with a conditional write. Face rigs often have 50+ shapes, so that is 100+ native calls per character per frame, including for characters that are off screen or far away. Skip when the target renderer is not visible (`isVisible`) or throttle with distance.
- `AnimalController` caps animals (`animalLimit`, `AnimalController.cs`) and `Animal.Update` is the largest per-animal method but only does small work. I would not expect animals to be a problem unless a scene spawns dozens.
- `ObstacleAvoidance` (`ObstacleAvoidance.cs`) is clearly newer, well-behaved code: NonAlloc capsule casts, 0.2 s probe timer, stuck timeout.
- `NPCController.CheckStuck` (`NPCController.cs:720-745`) runs from `UpdatePathfinding` up to twice a frame per active NPC and rounds `ai.remainingDistance` with `Math.Round` as a float equality check; on stuck it calls `Debug.Log("NPC ... stuck!")`. Debug.Log in a player build writes a stack trace to the log, so a stuck NPC repeating this every 5 s is a (small) steady cost and will show up in future Player.logs.

CullingGroup for NPCs: `FadeGameObject` (`FadeGameObject.cs:20`, `:51`) registers with `CullingType.NPC` by default; the callback toggles visibility. This is the only NPC culling system. NPC `Update` code itself is not culled: `NPCController.Update`, `NPC_FOV.Update` and the animators run whether or not the NPC is visible, unless the Animator culling mode (serialized) is set to cull. Animator `cullingMode` is worth checking on NPC prefabs.

## 8. UI

The UI is Unity UI with TextMeshPro. The main finding is the inventory refresh in section 5.1. Others, ordered by how often they run:

- `ShowTime.Update` (section 4.2). Per frame, with string building. Needs a minute-change check.
- `PlayerStatsUI.Update` (`PlayerStatsUI.cs:87`) toggles an icon scale every 0.05 s (20 Hz) via `SetIconScales`, and fades a damage overlay. Cheap but constant while the HUD is up.
- `HandItemsUI.Update` (`HandItemsUI.cs:47`): per frame, for two hand slots, `SetActive` calls, a sprite assignment, a fill update and a text update. Setting an unchanged sprite or text is mostly cheap, but `SetActive` and component lookups are native calls.
- `PowerMeter.Update` (`PowerMeter.cs:69-120`): per meter per frame, `Math.Pow`, `Math.Round(...).ToString()`, a `new char[]`, `Array.Reverse`, `new string`, then the text assignment. That is two or three allocations per frame per meter. One meter per apartment/panel, so the count could be dozens in a tenement scene. The value (`powerGrid.cumulativeBalance`) changes at most once per second of game time; updating at 4 Hz is invisible. The rotation of `disc` is fine to keep per frame.
- `TextUpdate.Update` (`TextUpdate.cs:23-31`): while active, assigns the text, sets `maxVisibleCharacters`, and calls `UIText.ForceMeshUpdate()` every frame, then hides untyped characters through `textInfo`. That is a full TMP mesh generation per frame during dialogue typing. Only runs during voiced dialogue, so it is an event-rate cost, but it is a visible one on long lines.
- `ApartmentRentUI.Update` (`ApartmentRentUI.cs:55-80`) and `UpgradeButtonUI.Update` (`UpgradeButtonUI.cs:280`): per frame string concatenation and text assignment during eviction/upgrade timers. They build `"Moving: "` then `+=` twice, so three assignments of three different strings per frame, each marking the TMP dirty.
- `LiquidStoragePanelUI.Update` (`LiquidStoragePanelUI.cs:122`): throttled by `currentFillingTime < 0.01f`.
- `QuickSlotBar.Update` (`QuickSlotBar.cs:186`) refreshes at 0.5 s, fine. `TooltipUI.LateUpdate` (`TooltipUI.cs:48`) repositions the tooltip each frame and calls `GetWorldCorners` into a cached array, plus `GetComponent<RectTransform>` per frame in controller mode. Minor.
- `KeyBindingsMenu.OnGUI` (`KeyBindingsMenu.cs:178-200`) does `GetChild(0).GetComponent<TextMeshProUGUI>()`, `GetComponent<TextColor>()` and `GetComponent<TextButtonColorEffectTMP>()` per key event while rebinding. Menu only.

Canvas rebuilds: I did not find a per-frame layout group trigger in code, but a layout group on the inventory grid combined with the per-second text rewrites in `ItemData.UpdateItem` would dirty the canvas continuously. I cannot see the prefab layout from here.

## 9. Save and load, and load-time work

`SaveController` is the most tuned part of the project. It already has: a pooled `FsStreamWriter`/`FsStreamReader` pair (`SaveController.cs:684-700`, `freeSaveWriters`), a GUID lookup dictionary with per-queue positions (`UpdateLookup`, `:895-929`), cached reflection field lists (`replaceFields`, `fieldRules`, `:1028-1100`), and a 64 KB buffered file stream with `SequentialScan` (`:670-680`). Reflection is cached for the replace path.

What is left:

- `SaveGame` (`:475-550`) runs on the main thread inside one call. It calls `ExecuteSaveLoadFunctions(SceneManager.GetActiveScene().GetRootGameObjects(), ...)` eight times (OnSavingGamePrimary, Secondary, Tertiary, OnSavingGame, LatePrimary, OnSavingFile, plus OnMapChanging when changing level), and `ExecuteChildSaveLoadFunctions` (`:1441`) does `GetComponentsInChildren(typeof(SavableScript), includeInactive)` for each root every time and then a `switch` per component. The hierarchy gets walked about eight times per save and again for each load phase (nine phases). Gathering the `SavableScript` array once per save pass and reusing it across the phases is the obvious saving, with the caveat that earlier phases could create or destroy components, so recollect only if a phase reports changes. `SerializeData` and `DeSerializeData` (`:711`, `:869`) call `data.GetType().GetField("GUID")` per saved object; a `Dictionary<Type, FieldInfo>` removes that.
- This is the autosave hitch. `SaveController.Autosave` (`:458`) is triggered by `TriggerAutosave`/`logic_autosave` and the autosave mode (`AutosaveMode`: default, every day, every hour). The coroutine waits two frames for the "Saving" UI and then blocks the main thread for the whole save, including the JSON write. The game logs the duration as `Time: Game <name> <save> saved! (Ns)..` (`:549`), so a play-session Player.log will give the real number.
- `GetOldestAutosave` (`:342-400`) loads and parses two `Info.tnmt` files with `FullSerializer` on every save to pick between `Autosave` and `Autosave2`. Small.
- Startup: `ItemDatabase.Awake` (`ItemDatabase.cs:19-45`) deserializes `Items.json` (3.1 MB) with FullSerializer, then for every item calls `Appearance.LoadSprite()` (a `Resources.Load<Sprite>` each) and `LoadItemReferencedAssets`. `RecipeDatabase.LoadDatabase` (`RecipeDatabase.cs:81`) reads `Recipes.json` (6.3 MB, UTF-16) and loads sprites too. FullSerializer is a reflection-heavy serializer; it is a one-time cost at the first scene (and `Reload()` redoes it). It is not worth patching unless startup time is the complaint.
- `SalsaConfigGuard.ValidateScene` (`SalsaConfigGuard.cs:44-60`) runs on every `sceneLoaded` and again one second later (`:36-40`). Each run does three `FindObjectsByType<...>(Include)` calls (Salsa, Emoter, Eyes) and validates every instance. It is a flat added cost on every scene transition, doubled. Running it once per scene, or only for newly added characters, would cut it in half.
- `ReflectionProbeUpdater.Awake` renders every probe in one go on scene load (see 5.4).
- `LightController.Start` O(L x Z x B) zone assignment and `CullingController` O(n squared) (see 6.1, 6.2) are the biggest scene-load costs I can see in code.
- `PlayerLevelEntrypoints.BuildEntryPoints` has a reflection walk over every `MonoBehaviour`, but it starts with `if (!Application.isEditor) return;`, so it does not run in the player. Ignore.
- `Resources.Load` at runtime: `Item.Loadprefab` (`Item.cs:225-260`) and `Item.cs:162-172` load by path per item; `CollectibleItem.cs:487` loads the use sound each time an item action is used; `PerCardOutline.cs:99` loads and instantiates two materials per card outline. All event-rate. `Appearance.Start` (`Appearance.cs:259-275`) calls `Resources.LoadAll<Wearable>` for twelve wearable folders (heads, beards, glasses, hairs, hats, shirts, pants, shoes, socks) on every `Appearance` instance, so each character spawn (every NPC, plus the player and the character editor) repeats twelve Resources scans that return the same assets. The result arrays are instance fields, so a static per-folder cache is a drop-in fix and a decent NPC-spawn-time win; it is not in the ranked list only because I could not tell how many `Appearance` components are instantiated at once.

## 10. Quality options and what a performance mod could expose

`OptionsMenu.applyQualitySettings` (`OptionsMenu.cs:1253-1382`) maps the sliders onto Unity settings. Using the user's current top-end values:

| Option | Value | What it sets |
| --- | --- | --- |
| qualityS 6 | `QualitySettings.SetQualityLevel(6, applyExpensiveChanges: true)` | Starting point for everything below, called before the overrides. Re-applying it resets other settings. |
| shadowQ 3 | `shadows = All`, `shadowDistance = 150` | Soft shadows out to 150 m. (Lower tiers: Hard only at 30 m or 10 m.) |
| shadowR 3 | `shadowResolution = VeryHigh`, `shadowCascades = 4`, `pixelLightCount = 4` | Four-cascade directional shadows at very high resolution. |
| textureR 0 | `globalTextureMipmapLimit = 0`, `anisotropicFiltering = ForceEnable`, `lodBias = 3` | Full-resolution textures, forced anisotropic. **`lodBias = 3` is tied to the texture option.** It makes every LOD switch three times farther away, so many more high-poly meshes are drawn. This is unrelated to texture memory and probably costs more than the textures do. |
| antialasing 0 | `PostProcessLayer.Antialiasing.None` | Off. |
| postFX 2 | `Beautify BestQuality`, Hx volumetrics enabled at `half` resolution, `particleRaycastBudget = 4096`, `softParticles = true`, `realtimeReflectionProbes = true` | The most expensive post stack. (Tier 1: BestPerformance + Hx quarter. Tier 0: Basic + Hx off + realtime probes off.) |
| SSAO 3 | stored in `QualitySave.qualitySave.SSAO` only | **Nothing in code applies it.** `grep SSAO` hits only `OptionsMenu`, `QualitySave` and `quality`. The slider has no effect (AO, if any, lives in the PostProcessing volume profiles or Beautify). |

Things a mod can do cheaply:

1. Postfix `OptionsMenu.applyQualitySettings` to override `lodBias`, `shadowCascades`, `shadowDistance`, `pixelLightCount`, and `particleRaycastBudget` from its own config, so a user can keep full texture resolution without paying for `lodBias = 3`.
2. Postfix `EffectsController.ChangeHxVolumetricResolution` (or the Hx camera `OnEnable`) to apply `SampleCount`, `DirectionalSampleCount`, `MaxLightDistance`, `MaxDirectionalRayDistance`, `TemporalSampling`, `blurCount`.
3. Postfix `LightController.Start` to scale `TurnOffDistance`, `ShadowOffDistance` and the two shadow-resolution distances by a config factor, and optionally force `NoShadows` on lights beyond a count. Point-light shadows cost a six-face cubemap render per light in range; this is likely the largest GPU term in an interior with many lights.
4. Expose a "Volumetric fog on/off" toggle independent of the post-FX tier (`EffectsController.DisableHxVolumetric`/`EnableHxVolumetric`, `:36-50`).
5. Decide on a frame cap and `GarbageCollector.incrementalTimeSliceNanoseconds` in one place.
6. `QualitySettings.realtimeReflectionProbes` is tied to postFX; `AzureEnvironmentController` and `ReflectionProbeUpdater` are the probe drivers. A mod can scale `ReflectionProbeUpdater.updateInterval`-equivalent (it is hard-coded `300` in `DeltaSeconds`, `ReflectionProbeUpdater.cs:50-57`, so patch the method) to lower the re-probe rate.

## 11. Things that look suspicious but are not worth patching

- `Door.Update`, `LightTurnOffOn.Update`, `Billboard.Update`, `Compass.Update` and the empty methods in section 3. Cheap bodies.
- `GunShoot`, `Selector` and `Camera.main`. Cached or rare.
- `ObstacleAvoidance`, `SaveController` replace path, `AudioSourceMaker` mixer resolution. Already optimized.
- `InputManager.OnGUI`. Intentional.
- Coroutines with short waits: `NPCInfo.OnLoadDelay` (0.1 s, one shot), `Crime.CreateNotifications` (0.33 s, one shot), `FurniturePlaceable.OnLoadDelay` (0.05 and 0.03 s, one shot), `BuildingSystem` (0.01 and 0.1 s, building mode only), `SMVEffects` (0.001 s, mushroom effect only), `AudioFade`/`SoundInfo` (0.05 s fades). No `InvokeRepeating` anywhere in the game code. No `while (true)` polling loops with short sleeps outside of effects.
- `Physics.*All` and `OverlapSphere`: only 11 call sites, mostly explosions and item checks. The ones on a periodic path are `InteractObjects` (third person only) and `WindowNaturalLight.HasObstruction`.
- `FindObjectOfType`: 43 sites, nearly all in load, spawn or editor code paths (`EventSystemChecker`, `DeleteOtherDialogueManagers`, `TurnOffAllLights` is a debug helper, `BuildingSystem.cs:741/752` runs when previewing a furniture placement and scans every `CraftingBase`/`CraftingUpgrade` in the scene each time it is called, which is only in building mode).
- `LightFlicker` coroutine `Renderer.materials = Mats` (`LightFlicker.cs:155`, `:173`): allocates per flicker, but flicker lights are a small subset.
- No `SendMessage` in hot paths (`UseTriggerMoney.cs:113` is per use).

## 12. Top 15 opportunities, ranked by payoff against risk

Risk here means the chance of changing behaviour or breaking a save, not difficulty. "Throttle" means a Harmony prefix that returns false when the interval has not elapsed. A prefix still pays the native-to-managed call, so patching a Unity message method saves its body, not the call.

| # | Target (exact method) | Cost | Fix | Payoff | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | `Inventory.DeltaSeconds(int)` (`Inventory.cs:303`) | `UpdateAllSlotsNow` on every inventory slot about 60 times a second while the inventory screen is open; thousands of UI writes and allocations per second, constant canvas rebuild | Prefix: when the panel is active, only run the original if at least 0.25 s of unscaled real time has passed since the last run (and always run when a flag from `AddItem`/`RemoveItem` marks it dirty; those paths already call `Slot.UpdateSlot` themselves, `SlotController.cs:94`). Perishing is timestamp based so nothing is lost | High (CPU and GC, only while inventory is open) | Low |
| 2 | `InteractObjects.GetInteractable(GameObject)` (`InteractObjects.cs:387`), via `GameObjectExtensions.GetInterfaces<T>` (`GameObjectExtensions.cs:7`) | Reflection + LINQ + allocations every frame while the crosshair is on a usable object, plus string concat in `UseText.ChangeUseText` | Prefix on `GetInteractable`: cache the `IInteractable[]` per GameObject instance id (clear on target change, scene change, or after `Destroy`); recompute only `IsInteractDisabled()` each frame; run `UpdateUseMessage()` only when the target changes or every ~0.1 s. A cheaper variant: replace `GetInterfaces<T>` with a per-`Type` cached `bool` for "implements T" and avoid the LINQ chain | High (every frame of play, GC) | Low to medium (use-text freshness; lock state changes need a refresh) |
| 3 | `TimeOfDayAzure.UpdateCurrentTimeAndDay` / `SecondsPassed` dispatch (`TimeOfDayAzure.cs:460-470`) | 35 subscribers called about 60 times a second at default day length | Coalesce: accumulate `deltaTime` and invoke `SecondsPassed` at 10 Hz (or when accumulated >= 5), passing the sum. Leave `MinutePassed`, `CurrentTimeAndDay` and `UpdateTimers` unchanged | High (many small costs at once) | Medium (any subscriber that needs per-second granularity or relies on being called in the same frame as the clock; all that I read take `sec` as an elapsed amount, but 35 handlers need a skim before shipping) |
| 4 | `PlayerStats.ProcessItemSpecialEffects` (`PlayerStats.cs:262`) and `PlayerStats.DeltaSeconds` (`:306`) | `new List<...>(dict.Keys)` 60 times a second, `BarUpdate()` 60 times a second | Prefix `ProcessItemSpecialEffects` to return when `unprocessedSpecialEffects.Count == 0`; throttle `BarUpdate()` to 10 Hz. If #3 ships, this is mostly covered | Medium | Very low |
| 5 | `LightsController.FixedUpdate` (`LightsController.cs:103`) / `LightCullZone.CheckIfPlayerInZone` (`LightCullZone.cs:340`) | LINQ `Any`/`All` with closures per zone per physics tick | Prefix `FixedUpdate`: run at 10 Hz and replace with a plain `for` loop over `zones`; keep `LightsController.CurrentZone` semantics | Medium (GC every tick, CPU scales with zone count) | Low |
| 6 | `Areaportal.LateUpdate` (`Areaportal.cs:51`) | Per portal per frame: string scan + gradient evaluate (`WeatherManager.CurrentOutsideAmbience`) + `material.color` read | Prefix: cache `CurrentOutsideAmbience()` once per frame (static, keyed on `Time.frameCount`) or per 0.25 s; skip when `num > distance` and the portal is already closed with alpha 1 and the cached colour is unchanged; throttle each portal to every 3rd frame | Medium to high on large scenes | Low (portals fade over 10 percent of their distance, 3 frames is invisible) |
| 7 | `CullingController.AddToCullingGroup` / `RemoveFromCullingGroup` / `UpdatePosition` (`CullingController.cs:51`, `:80`, `:97`) | O(n) array rebuild + a coroutine per add (n squared on load), O(n) `IndexOf` per moving light per frame | Replace with a capacity-doubling `BoundingSphere[]`, `SetBoundingSphereCount` for the live count, a `Dictionary<CullingItem,int>` for indices and swap-remove; one shared `CheckIfVisibleLate` after a batch | High load-time win on big scenes; small per-frame win | Medium (index consistency with `CullingGroupEvent.index`) |
| 8 | `WindowNaturalLight.UpdateLights` (`WindowNaturalLight.cs:200`) | `SetPropertyBlock` on every window renderer and `targetLight.color` on every window light each call, even when nothing changed | Prefix/transpile: only run the renderer loop inside the `textureModifyNext <= 0` branch; skip setting `targetLight.color` when it equals the new colour (epsilon). Optionally throttle the whole call to 1 Hz real time | Medium (rate depends on serialized `Resolution`) | Low |
| 9 | `Inventory.AllSlots(bool, bool)` (`Inventory.cs:170`) and `ShowTime.Update` (`ShowTime.cs:39`) | LINQ `Concat().ToArray()` allocation on every call (52 call sites); `ShowTime` calls it up to three times and builds strings each frame | `AllSlots`: prefix that returns a reused buffer when the source array lengths are unchanged (write contents each call, allocate a new array only when the length changes). `ShowTime.Update`: run at 4 Hz and rebuild the clock string only when the minute changes | Medium (GC) | Low |
| 10 | Lighting knobs: postfix `LightController.Start` (`LightController.cs:138`) and `OptionsMenu.applyQualitySettings` (`OptionsMenu.cs:1253`) | Shadow distance, `lodBias = 3`, 4 cascades, 150 m shadow distance, Hx quality are all at maximum and tied together | Config for: light `TurnOffDistance`/`ShadowOffDistance` scale, decoupled `lodBias`, `shadowCascades`, `shadowDistance`, Hx `SampleCount`/`MaxLightDistance`/volumetric toggle, `pixelLightCount` | High on GPU-bound setups (likely the biggest actual FPS gain) | Low (settings only) |
| 11 | `FaceBlendShapeFollower.LateUpdate` (`FaceBlendShapeFollower.cs:54`) | 2 native blend shape calls per binding per character per frame, also for off-screen NPCs | Prefix: return false when `cachedTargetRenderer.isVisible` is false; otherwise run every 2nd frame for characters beyond ~15 m | Medium (scales with NPC count) | Low (a stale face for one frame on becoming visible) |
| 12 | `NPC_FOV.Update` (`NPC/NPC_FOV.cs:73`) | Coroutine alloc every 0.5 s per NPC, `GetInvocationList()` alloc, `.Copy()` and `RemoveAll` closure | Prefix: replace the coroutine gate with a float timer, null-check `NPCsDetected` without `GetInvocationList`, skip when the NPC is asleep (already) or the player is farther than `maxDistance` (skip the raycast work entirely) | Medium (scales with NPC count) | Low to medium (keeps `activeCooldown` semantics that other code reads) |
| 13 | `Act_Wander`/`Act_Police`/`Act_Robber.GenerateNewDestinationCO` and `Animal.GenerateNewDestinationCO` (`NPC/Act_Wander.cs:50`, `Animal.cs:836`) | `PathUtilities.GetPointsOnNodes(all nodes, all nodes)` then pick one: N random points computed to use one | Replace the iterator (Harmony prefix returning a custom `IEnumerator`): pick `path.allNodes[Random.Range(0, count)]` and call `RandomPointOnSurface()` | Medium (spike per wander/animal destination) | Medium (it replaces a coroutine; keep `pathGenerator = null` and the callback exactly) |
| 14 | `SaveController.ExecuteChildSaveLoadFunctions` (`SaveController.cs:1441`), `SerializeData`/`DeSerializeData` (`:711`, `:869`), `GetObjectByGUID(string, Type)` (`:1330`) | Hierarchy walked about 8 times per save and 9 times per load; uncached `GetField("GUID")`; scene-wide search per `NodeReference.node` access | Cache the `SavableScript` array per pass; `Dictionary<Type,FieldInfo>` for the GUID field; check `Guids` before the scene search and cache into `_node` | Medium (autosave hitch, load time) | Medium (component lists must be refreshed if a phase spawns objects; roll out behind a config switch and compare saves) |
| 15 | `SalsaConfigGuard.ValidateScene` (`SalsaConfigGuard.cs:44`), `ReflectionProbeUpdater.Awake` (`ReflectionProbeUpdater.cs:28`), `ItemDatabase.Awake` (`ItemDatabase.cs:19`) | Fixed load-time costs: six `FindObjectsByType` per scene change, full probe renders on scene load, 3 MB JSON + sprite preload at start | Skip the delayed second `ValidateScene`; time-slice `RenderAllProbes`; defer `LoadSprite` for items until first use | Low to medium (scene-change time) | Low to medium |

Items that did not make the list but are cheap if there is appetite: `TimeOfDayAzure.UpdateTimers` (`timers.ToArray()` per frame, skip when `timers.Count == 0` or reuse a scratch list), `AzureEnvironmentController.Update` (equality-guard the five `RenderSettings` writes), `AzureWeatherController.FixedUpdate`/`AzureTimeController.FixedUpdate` (move to 10 Hz), `PowerMeter.Update` (4 Hz and a string cache), `TextUpdate.Update` (only `ForceMeshUpdate` when text or typed count changed), `MaterialScroll.Update` (cache the `Material[]`), `LightController.Update` disabled branch (apply once), and the empty `Update` stubs in section 3.

## 13. What to measure first

Static reading cannot say which of these matter in a real scene, so before writing patches:

1. Capture a Player.log from a real session of an hour or so that includes a few autosaves, opening the inventory, and walking through an exterior. Look for `Time: Game ... saved!` durations, `stuck!` lines, and any repeated error lines (the `Player)` tag error in section 6.4 is the one I expect).
2. Check `Time.fixedDeltaTime` and the A* thread count in `globalgamemanagers`/scenes with AssetStudio or UABEA.
3. Count `LightController`, `Areaportal`, `NPCController`, `Animal` and `grow` instances in the biggest exterior scenes. The ranking above assumes lights and portals are in the hundreds to low thousands there.
4. Profile with the Unity profiler attached to the Mono player (needs a development build, which this is not) or use an in-process Harmony stopwatch around `Inventory.DeltaSeconds`, `InteractObjects.Update`, `Areaportal.LateUpdate`, `LightController.Update` and `TimeOfDayAzure.UpdateCurrentTimeAndDay` to log per-second totals. That is the cheapest way to confirm sections 4 and 5.
5. For GPU cost, compare frame time with `lodBias` forced to 1.5, shadow distance 60, and Hx off before touching any code; if that moves FPS a lot, the lighting knobs in #10 are the answer and the CPU patches are secondary.
