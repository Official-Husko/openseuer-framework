# World: building, apartments, interactables, utilities, transport

Notes on how the world side of Obenseuer works, written for mod/framework/co-op work.
All paths are relative to `decompiled/Assembly-CSharp/` and line numbers are from the current
decompile (Unity 6000.0.77f1, Mono). Types live in the global namespace unless stated.

Short version of what matters most:

- Almost every world object is a `SavableScript` (a MonoBehaviour) with a public `string GUID` field.
  The save system finds objects by that field name via reflection. A GUID is the only stable id.
- Player interaction goes through one raycast in `InteractObjects` and ends in a virtual
  `Interactable.Interact()`. State changes are then applied by plain public methods on the concrete
  class (`Door.Open`, `Button.Press`, `LightTurnOffOn.turnLightOff`, ...), usually followed by a `Relay` fire.
- A great deal of game logic is wired in the editor through `Relay` / `output` objects, not code.
  `Relay.triggerOutputs` is a useful choke point but also very noisy.
- Time based logic hangs off static events on `TimeOfDayAzure` (`SecondsPassed`, `MinutePassed`,
  `DayChanged`), so most economy/power/fuel ticks are driven by game-clock time, not frames.
- Level changes save and reload the whole scene. Co-op has to treat a scene change as a hard reset
  of every GUID-addressed object.

## 1. Shared plumbing

### 1.1 SavableScript and GUIDs

`SavableScript.cs` is the base for nearly everything below. It exposes empty virtual hooks that
`SaveController` calls on every active `SavableScript` in the scene (and on DontDestroyOnLoad roots):
`OnSavingGamePrimary/Secondary/Tertiary/OnSavingGame/OnSavingGameLatePrimary`,
`OnLoadingGamePrimary/Secondary/Tertiary/OnLoadingGame/OnLoadingGameLatePrimary`,
`OnMapChanging`, `OnMapChanged`, plus the DestructibleList variants.

Order, from `SaveController.SaveGame` (`SaveController.cs:475`): MapChanging, Primary, Secondary,
Tertiary, (DontDestroyOnLoad: Special), OnSavingGame, LatePrimary. Inactive objects are skipped
(`ExecuteChildSaveLoadFunctions`, `SaveController.cs:1441`).

Key facts:

- `SaveController.SerializeData(UnityEngine.Object data, bool global=false)` (`SaveController.cs:707`)
  reads `data.GetType().GetField("GUID")` and stores a header under that GUID. No GUID, no save.
  `global:true` puts it in the cross-level save file instead of the per-level one.
- `SaveController.GetObjectByGUID(string guid)` (`SaveController.cs:1360`) is a dictionary lookup
  filled while loading. The overload `GetObjectByGUID(string, Type)` (`SaveController.cs:1330`) does
  `FindObjectsOfType(typ)` plus reflection on every object. Avoid it in anything per-frame.
- Serialization uses FullSerializer. `[fsIgnore]` marks fields that come from the prefab/scene;
  `[fsProperty]` forces private fields in. So the set of fields saved on a class is the exact
  state the game itself considers important, which makes it a good first guess for what to sync.
- GUID shapes you will see:
  - Scene-baked objects (doors, buttons, lights, elevators, segments): GUID set in the editor, stable.
  - Objects inside an instantiated prefab: `{parentGUID}_{iter}_{TypeName}_{index}`
    (`InstantiatedPrefabRef.AssignSubGUIDs`, `InstantiatedPrefabRef.cs:98`).
  - Objects inside a tenement apartment prefab: `"APT_" + apartmentNumber + "_" + original`
    (`TenementApartmentPrefabs.ProcessGUIDs`, `TenementApartmentPrefabs.cs:568`).
  - Placed furniture: `{FurnitureManager.GUID}{n}`, assigned at save time only (see 4.3).
- Many singleton-ish controllers use a constant GUID: `BuildingSystem` ("BuildingSystem"),
  `TenementController` ("TenementController"), `FurnitureBlueprintController`, `BlackoutController`,
  `AnimalController`, `PowerGridsController`.

### 1.2 Relay and output

`Relay.cs` (SavableScript) holds an `output[] outputs` array (`Outputs/output.cs`). Each `output` wraps a
UnityEvent. `Relay.triggerOutputs()` / `triggerOutputs(object a)` (`Relay.cs:99`, `Relay.cs:104`) runs every
non-disabled output, with optional per-output delay coroutines. `Relay` also has `fireOnceOnly`,
`firedOnce` and required task item/objective/dialogue-variable gates, so firing it can be a no-op.

Most "events" on doors, lights, buttons, elevators, power sources are private `Relay` fields
(`onOpen`, `onTurnOff`, `onPowered`, ...), so they cannot be reached from outside without reflection.
A Harmony postfix on `Relay.triggerOutputs(object)` sees all of them but has no idea which
component owns it except through `__instance.gameObject`/`GUID`.

### 1.3 Time events

`TimeOfDayAzure` (static events, `TimeOfDayAzure.cs:315`): `SecondsPassed(int)`, `MinutePassed(int)`,
`DayChanged(int)`. `sec` is game seconds elapsed since the last call, not real seconds.
`TenementController`, `PowerGrid` (cumulative), `Fuel`, `BatteryPowerSource`, `LiquidStorage`, `Animal`
all subscribe. `TimeOfDayAzure.instance.updateTimeDisabled` is true during loading and several
load coroutines spin on it, so state applied before it flips can be overwritten.

## 2. Interaction system

### 2.1 Types

- `IInteractable` (`IInteractable.cs`): `Interact()`, `StopInteract(bool stopHold)` (returns the
  interactable to keep as "last used"), `UpdateMoney()`, `UpdateUseMessage()`, `IsInteractDisabled()`,
  `SetInteractDisabled(bool)`.
- `Interactable : SavableScript, IInteractable` (`Interactable.cs`) is the base class for 56 types.
  Public fields: `title`, `useMessage` (wraps text in parentheses), `interactDisabled`,
  `extraInteractDistance` (0..5, added to the 1.5 m base use distance). Virtuals: `Interact`,
  `StopInteract`, `UpdateMoney`, `UpdateUseMessage`, `IsInteractDisabled`, `SetInteractDisabled`,
  `EnableInteract`, `DisableInteract`.
- Only one class implements `IInteractable` directly: `WorkableResourceSource : DamageReceiver`
  (`WorkableResourceSource.cs:9`). Anything querying "all interactables" must use the interface,
  not `Interactable`.
- Subclasses of `Interactable` worth knowing: `Door`, `FakeDoor`, `DoorLockBypass`, `Button`, `Keypad`,
  `InteractableChair`, `InteractableRelay`, `InteractableLadder`, `InteractableFastTravel`,
  `Changelevel`/`DoorChangelevel`, `PowerMeter`, `Storage`, `LiquidStorage`, `CraftingBase` (and so
  `Generator`), `Collectible`/`CollectibleItem`, `Sleep`, `Radio`, `Phone`, `VendingMachine`,
  `MarketShop`, `BottleRecycling`, `InteractableFurnitureDisplay`, `TrackTrainInteractableCoupler`,
  `MinitrainFuelTank`. Full list: `grep -l ": Interactable"`.

### 2.2 Player pipeline

`InteractObjects` (`InteractObjects.cs`, singleton `InteractObjects.instance`, on the player):

1. `Update()` (`InteractObjects.cs:107`) runs every frame. First person: `RaycastInteract()`.
   Third person: `ThirdPersonInteract()` which also does an `OverlapSphere`.
2. `RaycastInteract(bool firstPerson = true)` (`:133`) casts from the camera along the player forward
   with `MaskList`, distance `max(maxDistance, useDistance + 5)`. A hit only counts if its layer is one
   of the "usable" layers (17, 23, 15, 20, 29) and the hit distance is within `1.5 + extraInteractDistance`.
3. `GetInteractable(GameObject)` (`:387`) does `current.GetInterfaces<IInteractable>()` and takes the first
   that is not `IsInteractDisabled()`. It stores it in `InteractObjects.interactable` and calls
   `UpdateUseMessage()` on it (this is every frame while looked at, so `UpdateUseMessage` overrides are hot).
4. `GameController.InteractControls()` (`GameController.cs:252`) polls the "Interact" key and calls
   `InteractObjects.instance.UseInteractable()` (`:415`). That sets `lastUsedInteractable` and calls
   `interactable.Interact()`. If a `savedForceInteractable` is set it wins; `savedInteractable` is the
   fallback while seated or on a ladder (chair/ladder/toilet set it).
5. Releasing / holding goes through `StopUseInteractable(bool stopHold)` (`:447`), which calls
   `StopInteract` on the last used one. `Button` uses this for hold-press.

Third person: `InteractableList` (`InteractableList.cs`, `UINavigationCore`) is only the pooled UI list of
nearby interactables; selecting an entry calls `InteractObjects.GetInteractable` (`InteractableListItem.cs:89`)
and then the same `UseInteractable()` path applies. It holds no gameplay state and needs no sync.

`Usable` (Pixel Crushers Dialogue System) still exists as a separate legacy path (`InteractObjects.usable`),
not covered here.

### 2.3 Hook points for the interaction layer

| Goal | Hook | Notes |
|---|---|---|
| Catch every local player use | Prefix/Postfix `InteractObjects.UseInteractable()` | `public void UseInteractable()`. Read `__instance.interactable` (the `IInteractable`) in a prefix; cast to `Component` for GUID. |
| Catch every use of a specific class | Postfix `X.Interact()` | Virtual on `Interactable`; patch the concrete override, patching the base misses overrides. |
| Block local use (e.g. remote player holds it) | Prefix returning false on `UseInteractable` or on the class `Interact` | |
| Use remote player's interaction on host | Call the concrete state method (below), not `Interact()` | `Interact()` assumes the local player: it reads `PlayerLocator.instance`, opens UI, checks local inventory/keys. |

## 3. Interactable classes and how state is applied

### 3.1 Door (`Door.cs`, 974 lines)

Fields that are saved (`[fsProperty]`): `savedAngles`, `savedPosition`, `saved`, `position` (0..1),
`moving`, `direction`, `timetoclosing`, `ReversedDir`, plus public `State` (`State.Open/Closed`) and
`_Lock` (`Lock` class: `Locked`, `Lockpickable`, `Difficulty`, `LockAfterTime`, timer).
`GUID` is `[fsIgnore]` public string. Save is `OnSavingGame()` `Door.cs:243`, load `OnLoadingGame()` `:252`.

Call graph:

- Player: `Interact()` `:282` -> `UseDoor(bool bypassLock)` `:355`. `UseDoor` decides which side the
  player is on (`Vector3.Dot(player - door, door.right)`), then calls `ToggleInterrupt(opposite, bypassLock)` `:591`
  or `Open/OpenOpposite/ForceOpen/ForceOpenOpposite` directly when `ManualClose` is false.
- `Toggle(bool opposite, bool bypassLock)` `:627` picks `Open()` `:461`, `OpenOpposite()` `:526`,
  `ForceOpen()` `:480`, `ForceOpenOpposite()` `:549`, or `Close()` `:576`.
- Non-forced `Open*` call `_Lock.Use(...)` first, which checks the local player's task items for the key
  (`Lock.cs:105`, `TaskItemsManager.instance.CheckIfPlayerHasTaskItem`) and may start an unlock delay
  coroutine, or call `LockedUse()` and play the locked sound. `ForceOpen*` skip the lock.
- All open paths converge on private `OnOpen()` `:689` (sets `direction=true`, `doorIsOpen=true`, `moving=true`,
  fires `onOpen`). Closing converges on `Close()` which sets `direction=false`, `moving=true`, fires `onClose`.
- Motion is in `Update()` `:866`: integrates `position` with `Time.deltaTime * OpenSpeed`, rotates or slides
  the transform, and ends in `OnFullyOpen()` `:717` / `OnFullyClosed()` `:736` (these set `State`,
  fire `onFullyOpen/onFullyClosed`, and re-lock if `lockWhenClosed`). The door has no authoritative angle
  setter other than `SetPosition(float)` `:661`.
- NPCs open doors with `ForceOpen()` (`NPC/Waypoint_Door.cs:75,90,115,128,133`). The build menu auto-opens doors
  next to the player every 0.25 s with `UseDoor` (`BuildingSystemMenu.TryAutoOpenNearbyDoors`, `:296`).
- Lock: `Lock()` `:781`, `Unlock()` `:787`, `ToggleLock()` `:768`, `LockAndClose()` `:570`, property `Locked`.
  They set `_Lock.Locked` and fire `relay_Lock/relay_Unlock`.
- Auto close: `OnTriggerExit` closes when the player leaves the close-distance trigger (`Door.cs:967`), and `AutoClose` + `timetoclosing` in `Update` restarts motion without calling `Close()`.

Opposite-direction doors: `OpenOpposite` negates `OpenAngle` and sets `ReversedDir` before `OnOpen()`; this is
undone in `EnsureNormalDirection()`/`OnFullyClosed()`. A remote peer applying "open" must also pass the same
side flag or the door swings through the wrong way.

Hook points:

```
[HarmonyPostfix] Door.OnOpen()        // private, no args; all opens
[HarmonyPostfix] Door.Close()         // public void, every close start (incl. trigger exit)
[HarmonyPostfix] Door.OnFullyOpen()/OnFullyClosed()   // settled state
[HarmonyPostfix] Door.Lock()/Unlock()/ToggleLock()    // lock state; Locked setter is a property on Lock
```

`OnOpen` does not know the side; read `__instance.ReversedDir` (field is private, use `AccessTools`)
or compare `OpenAngle` sign. Apply on the receiving side with `ForceOpen()` / `ForceOpenOpposite()` /
`Close()` / `Lock()` / `Unlock()` under a re-entrancy flag so your own postfixes do not echo.
`SetPosition` is only for late join snapshots.

### 3.2 Button / switches (`Button.cs`)

`Button : Interactable` is the generic wall switch/lever/button. Saved: `PressedIn`, `unpressCooldownActive`,
`_Lock`. Config: `Toggle`, `HoldPress`, `PlayerCanUnpress`, `AutoUnpressTime`, `OnPressed[]`, `OnUnpressed[]`
(`output[]`, these are what drive lights/doors/power), delays and cooldown.

Flow: `Interact()` `:281` -> coroutine `WaitPress` -> `Use()` `:566` (lock check via `_Lock.Use(Trigger, ...)`)
-> `Trigger()` `:601` (plays sound/animation, starts `DelayPress` or `DelayUnPress`, `CoolDown`) ->
`Press(bool)` private -> `PressEffects()` `:692` which sets `PressedIn=true` and fires `OnPressed`.
Unpress: `Unpress(bool,bool)` -> `UnpressEffects()` `:769` fires `OnUnpressed`.

Hooks: postfix `Button.PressEffects()` and `Button.UnpressEffects()` (public, no args) see every transition
including auto-unpress and external `Press()`/`Unpress()` calls. Remote apply: call `Press()` / `Unpress()`
(both skip the player lock; `Press` returns early if `Locked || PressedIn`). `PressSkipRequirements()` exists
for ignoring required items. `Lock()`/`Unlock()` `:528,:533` exist on the button too.

### 3.3 Keypad, relay interactables, chairs, lights

Keypad (`Keypad.cs`): saved state is only `open` and `noPower` (global save). `Interact()` -> private `Use()`:
if closed it opens the keypad UI (`GameUIController.ToggleGameMenu(..., MenuPanels.Keypad, ...)` and
`KeypadUI.instance.SetKeypad(this, type)`), if open it locks. The code is `code[]` (`[fsIgnore]`, from the prefab).
The UI validates the code and then calls `TriggerSuccess()` `:151` / `TriggerFailure()`. Hooks: postfix
`Keypad.TriggerSuccess()`, `Keypad.Lock()` `:133`, `Keypad.SetOpen(bool)` `:146`, `Keypad.ChangePowerState(bool)` `:172`.
The keypad UI is local-only, so only the outcome needs syncing.

`InteractableRelay` (`InteractableRelay.cs`): just fires `onInteract` after cooldown/required-item checks. Not saved.
Sync the Relay fire, or the effects it causes.

`InteractableChair` (`InteractableChair.cs`): saved `playerSitting`, `occupied`, `currentNpcInfo`, `entryRotation`, etc.
Public API: `Sit(bool immediately=false)` `:375`, `StopSit()` `:577` / `StopSit(bool)` `:582`, `SitToggle()` `:353`,
`ForceStandUp()` `:267`, `static StandUpFrom(GameObject root)` `:255`, `NPCSit(...)` overloads `:679-696`, `Occupied()`.
`Sit` moves the camera via coroutine, freezes the player body and registers `InteractObjects.instance.savedInteractable`.
It assumes the local player; for remote avatars you want `ChangeOccupied(bool)` `:292` (sets `occupied`, which makes the
local player see "Occupied") plus your own avatar pose. `occupied` is only meaningful as a flag; the chair does not
track who sits. Hooks: postfix `Sit(bool,bool)` (private overload, `:380`) and `StopSitEnd()` (private) or the public
`StopSit` pair. `BuildingSystem.Move` and `FurniturePlaceable.Take` call `StandUpFrom` before moving furniture.

Lights, three separate mechanisms:

- `LightTurnOffOn : SavableScript` (`LightTurnOffOn.cs`): the fixture switch. Saved `on`, `noPower`.
  `turnLightOn()` `:116`, `turnLightOff()` `:126`, `TogglePower(bool)` `:133`, plus fade variants
  `TurnLightOnFade(bool)`, `TurnLightOffFade()`. Real light visibility = `on && !noPower`. This is what buttons
  and `PowerGridListener` outputs call. Hook postfix on the three public methods.
- `LightTurnOffOnMultiState` (`:258 Toggle(bool)`, `:272 SetState(int)`): multi-state variant, also saved.
- `LightsON : MonoBehaviour` (`LightsON.cs:157 Toggle()`, `:169 TurnON()`, `:238 TurnOFF()`): older switch that is
  not a SavableScript and stores nothing. Treat it as event-only.
- `LightController` (`LightController.cs`) is added to every `Light` at runtime/placement. It is a distance and
  zone culler (see section 9), not a gameplay switch. `LightsController` is its manager (static lists, zones).
  `LightsController.UpdateLightOverrideStatus()` is the static "re-evaluate everything" call.
- `TimedLights` flips lights by time of day via `SecondsPassed`, so peers with synced clocks will agree on their own.

### 3.4 InteractableFastTravel, Changelevel (brief)

`InteractableFastTravel.Interact()` just calls `FastTravelController.instance.ToggleMenu()` (`FastTravelController.cs:12`),
which opens `MenuPanels.FastTravel` through `GameUIController.ToggleGameMenu`. The panel's buttons live in the scene UI
and ultimately call `SaveController.ChangeLevel`. See section 7.3.

## 4. Building and furniture

### 4.1 Data model

- `Furniture : SavableScriptableObject` (`Furniture.cs`): the definition asset. `Id` (`:356`, falls back from
  `id` -> `addressableAssetPath` -> `title`), `category`, `prefab`, `previewPrefab`, `skins`, `alternativePrefabs`,
  `restrictedAreas` (`Furniture.BuildingArea`), `dismantleItems`, prices. `GiveItem()` (`:514`) adds one to the inventory.
  Saved by reference: `SavePath()`/`LoadFromPath()` store and resolve an Addressables path (`addressableAssetPath`).
  So the cross-peer id of a furniture type is `Furniture.Id` / `addressableAssetPath`.
- `BuildingSystem.FurnitureInfo` (`BuildingSystem.cs:13`): one inventory/placed entry. Fields: `furniture`,
  `taskItem`, `gameObject` (`[fsIgnore]`), `amount`, `meta` (`Meta`: `currentSkin`, `currentAlternativePrefab`,
  `customImages`), `itemStack` (set when the "furniture" is really a world item), `gameObjectGUID`,
  `gameObjectSaved`, `gameObjectFurnitureManagerGUID`, `gameObjectPosition`, `gameObjectMainRotation`,
  `gameObjectFurnitureRotation`, `gameObjectSubGUIDs`. `SavePath(bool placedItem)` / `LoadFromPath(bool placedItem, FurnitureManager)`
  convert between object and saved form; `LoadFromPath` re-instantiates `furniture.prefab` when `gameObjectSaved`.
- `FurniturePlaceable : SavableScript` (`FurniturePlaceable.cs`): component on the root of every placed prefab. Public
  fields: `GUID`, `global`, `furniture`, `meta`, `manager`, `managerGUID`, `parent`/`parentGUID`,
  `childFurnitureGUIDs`, `attachedto`/`attachedtoGUID`, `attachedFurnitureGUIDs`, `isPreplacedFurniture`,
  `preplacedFurnitureSecondary`, `cantTake`, `decal`, `item` (CollectibleItem when it is an ordinary item sitting
  on a surface). Private `furnitureInfo` is exposed via `GetFurnitureInfo()` `:347`.
  Convention: child 0 of the root is the visual that gets rotated; root transform holds position and base rotation.
- `FurnitureManager : SavableScript` (`FurnitureManager.cs`): one per buildable zone (apartment, workshop, shop, ...).
  Has `GUID` (editor, `[fsIgnore]`), `placedFurniture`, `preplacedFurniturePrimary/Secondary`, `powerGrid`,
  `area`, `areaType`, `managerDisabled`, `notFullyExamined`, `tenementApartmentPrefabs`. C# events
  `FurniturePlaced(GameObject)` and `FurnitureTaken(GameObject)` are raised by `PlacedFurniture(GameObject)` `:342`
  and `TookFurniture(GameObject)` `:347`.
- `BuildingArea` (`BuildingArea.cs`): trigger volume (layer 25) that sets `BuildingArea.currentManager` and
  `BuildingSystem.activeManager`/`inBuildingArea` on `Enter()` `:45` / `Exit()` `:128`. Only the player layer 10 triggers it.
  Remote players do not touch these statics, which is what you want.
- `ApartmentFurniture` (`ApartmentFurniture.cs`) is a tiny static lookup table (`FurnTypeID`, prefab, `RoomData.RoomDesignation`
  per `RoomID`) and `Apartments` is an empty MonoBehaviour. Neither carries gameplay logic.
- `FurnitureBlueprintController` (`FurnitureBlueprintController.cs`, GUID "FurnitureBlueprintController", global save):
  tracks which blueprints have been spawned/collected (`spawnedBlueprints`). `GetBlueprintToSpawnIndex()`,
  `GetRandomBlueprints(string category, int amount)`, `BlueprintIsCollected(TaskItemBlueprint)`. It is random world-gen
  state: in co-op the host should own it, or two clients will each hand out "unique" blueprints.
- The inventory of unplaced furniture is `BuildingSystem.availableFurnitures` (a list of `FurnitureInfo`) plus a
  `TaskItem` per type created through `Furniture.CreateTaskItem`. Adding/removing keeps `TaskItemsManager` in step.

### 4.2 How things are placed, taken, moved

All of it is in `BuildingSystem` (singleton `BuildingSystem.instance`, 1852 lines), driven by `BuildingSystemMenu`.

Input path: `BuildingSystem.Update()` (`:595`) polls "Building Menu"/"Place Take"/"Rotate Object" keys only when
`inBuildingArea` is true -> `BuildingSystemMenu.Press()` (`BuildingSystemMenu.cs:329`) -> one of
`BuildingSystem.Place()`, `Move()`, `Take(out GameObject)`, depending on the selected slot type.
While the menu is open `StartPreview()` (`:878`) runs every frame (see section 9).

- Place: `public void Place()` `BuildingSystem.cs:1458`. Requires `ObjectPreview.isPlaceable`. Reuses
  `currentFurniture.gameObject` if the entry still has a saved GameObject (furniture that was taken with contents),
  otherwise `Instantiate(furniture.GetPrefab(altIndex), currentpos, rot, activeManager.transform)`. Then: reconnect power
  (`ReconnectPower` `:1665`), add `LightController` to lights, `FurniturePlaceable.SetFurnitureInfo(currentFurniture)`
  (`FurniturePlaceable.cs:365`, which sets `manager = activeManager` and appends a new `FurnitureInfo` to
  `manager.placedFurniture`), parent/attach links (`AddChildFurniture`, `AddAttachedFurniture`), rotate child 0, then
  `RemoveFurniture(currentFurniture, 1, ...)` `:1813` to decrement inventory. Note `Place()` never calls
  `activeManager.PlacedFurniture(...)`; only `PlaceMovingItem` does. Do not rely on the `FurniturePlaced` event for fresh placements.
  Wallpaper and surfaces take a different branch: `FurnitureSurfaceArea.TrySetWallpaper/TryRemoveWallpaper` and no GameObject is created.
- Take: `public List<FurniturePlaceable> Take(out GameObject savedGameObject)` `:1014` raycasts via the stored `hit`, checks
  `FurniturePlaceable.CanTake()` `:534` (inventory space, required items, trash), then `FurniturePlaceable.Take(GameObject, List<FurniturePlaceable>, out GameObject, bool removeParent=true, bool keepItem=false)` `:387`.
  That recursively takes children and attached furniture, stands the player up from chairs, calls `manager.TookFurniture(go)`,
  then `BuildingSystem.Take(Furniture, Meta, GameObject, out GameObject)` `:1637` -> `AddFurniture(...)` `:1756`, which either keeps
  the GameObject parked under `inventoryLocation` (if `HasSaveableContent`: storage, liquids, crafting progress, fuel) or destroys it.
  Then `Destroyed()` `:327` registers the GUID in `DestructibleList.DestroyMapItem` and `OnTaken()` clears `manager`.
  A plain item on a surface (`item != null`) is picked up via `item.PerformPickup()` instead and does not go through `AddFurniture`.
- Move: `public void Move()` `:1042`. Two flavours. For furniture it is literally Take then auto-select the same entry in the build
  menu (`BuildingSystemMenu.FindFurnitureAndSelectIt`, `:394`), so the move is Take + Place and fires both. For loose items
  (`componentInChildren != null && furniture == null`) it deactivates the object (`movingPlacedItem`) and shows a preview; the finish is
  `PlaceMovingItem()` `:1598`, which re-parents under `activeManager`, calls `manager?.RemovePreplacedFurniture(obj)` and
  `activeManager.PlacedFurniture(obj)`.
- Cancel/leave: `DestroyCurrentObject()` `:701` (reactivates a half-moved item), `ToggleMenu()` `:689`.

Adding furniture without the UI (shops, mail, dev console): `BuildingSystem.AddFurniture(Furniture furniture, int amount = 1, FurnitureInfo.Meta meta = null)` `:1750`
and `RemoveFurniture(FurnitureInfo, int amount = 1, bool updateMenu = false)` `:1813`. Callers: `ShopBaseUI`, `MailController`, `CollectiblePackage`,
`FurnitureCollectable`, `BundleMail`, `InteractableFurnitureDisplay`, `DeveloperConsoleRoutines.AddFurniture`.
`PlaceFurniture.Place()` (`PlaceFurniture.cs`) is a scripted placement used by scene content; it duplicates the instantiate logic without going through `BuildingSystem`.

### 4.3 Identification and saving of placed furniture

- Type: `Furniture.Id` / `addressableAssetPath`. Variant: `meta.currentSkin`, `meta.currentAlternativePrefab`, `meta.customImages[].path`.
- Instance: `FurniturePlaceable.GUID`. A freshly placed instance has an empty GUID until the next save. During
  `FurnitureManager.OnSavingGameTertiary()` (`FurnitureManager.cs:89`) it walks `placedFurniture` from the end and assigns
  `component.GUID = manager.GUID + n` with `n` counting in that reverse order, and rebuilds `placedFurnitureGUIDS`. So placed-furniture
  GUIDs are NOT stable across saves: removing one item shifts the numbers of others. Do not use them as network ids.
  Use your own id, stored e.g. in a component you add, keyed by manager GUID plus a counter you control.
  Pre-placed furniture (`isPreplacedFurniture`) keeps its prefab GUID, and sub-components get `{GUID}_{iter}_{Type}_{n}`.
- Position/rotation are saved on `FurnitureInfo` (`gameObjectPosition`, `gameObjectMainRotation`, `gameObjectFurnitureRotation`)
  and per-component state is saved under the sub-GUIDs (`gameObjectSubGUIDs`) so storage contents, crafting state and lights persist.
- Inventory side: `BuildingSystem.OnSavingGameSecondary()` (`:513`) gives parked GameObjects GUIDs `"BuildingSystem" + n` and saves
  `availableFurnitures` globally (`SerializeData(this, global: true)`).
- On load: `FurnitureManager.OnLoadingGameSecondary()` (`:168`) -> `FurnitureInfo.LoadFromPath(placedItem:true, manager)` re-instantiates each,
  `FurniturePlaceable.OnLoadingGameLatePrimary()` (`:177`) resolves manager/parent/child/attached by GUID after a 0.05 s delay coroutine.

### 4.4 State to sync (building)

Per placement/removal event: manager GUID, `Furniture.Id`, `Meta` (skin, alt prefab, custom image paths), world position, root rotation,
child-0 local rotation, parent and attachment-point links, and for "item on surface" placements the `ItemStack`. Per inventory:
`availableFurnitures` is player-global, so decide whether co-op shares one inventory (host owns it) or each client has its own.
Custom images are local files (`customImages[].path`), they will not exist on the other machine.

### 4.5 Hook points (building)

```
[HarmonyPrefix]  BuildingSystem.Place()                     // void, uses private hit/currentpos; read via AccessTools
[HarmonyPostfix] BuildingSystem.Place()                     // new object is the last entry of activeManager.placedFurniture
[HarmonyPostfix] BuildingSystem.Take(out GameObject)        // public List<FurniturePlaceable> Take(out GameObject savedGameObject)
[HarmonyPrefix]  FurniturePlaceable.Take(GameObject, List<FurniturePlaceable>, out GameObject, bool, bool)  // catches children recursion; __instance is the removed one
[HarmonyPostfix] BuildingSystem.PlaceMovingItem()           // private, loose items only
[HarmonyPostfix] FurnitureManager.PlacedFurniture(GameObject) / TookFurniture(GameObject)   // events, but PlacedFurniture misses normal Place()
[HarmonyPostfix] BuildingSystem.AddFurniture(Furniture, GameObject, out GameObject, int, FurnitureInfo.Meta)   // inventory in
[HarmonyPostfix] BuildingSystem.RemoveFurniture(FurnitureInfo, int, bool)                                       // inventory out
[HarmonyPostfix] FurniturePlaceable.ChangeSkin(int) / UpdateCustomImage()                                       // variant changes
```

Best single choke points: `FurniturePlaceable.SetFurnitureInfo(FurnitureInfo, FurnitureManager)` for "a placed instance now exists"
(it is called by `Place`, by `FurnitureManager.AdoptFurniture` and by load paths, so filter by caller or by a flag), and
`FurniturePlaceable.Take(...)` for removal. 
To apply a remote placement without the UI, mimic `Place()`: `Instantiate(furniture.GetPrefab(alt), pos, rot, manager.transform)`,
`placeable.SetFurnitureInfo(new FurnitureInfo(furniture, meta, taskItem, go), manager)`, set child 0 local rotation,
`ReconnectPower` equivalent (private; replicate for `CraftingBase`, `PutItemSelection`, `PowerGridListener`), and do not call
`RemoveFurniture` (the remote player's inventory is theirs). To remove: `placeable.Take(go, new List<FurniturePlaceable>(), out _)`
will also push it into the local player's `availableFurnitures` via `BuildingSystem.instance.Take`, which is wrong for a remote
removal; either patch around it or destroy the object and call `Destroyed()` yourself.

## 5. Tenement and apartments

### 5.1 Structure

- `TenementController : SavableScript` (`TenementController.cs`, 1361 lines, `TenementController.instance`, GUID "TenementController",
  global save). Holds `general` (`TenementGeneral`), `apartments` (`List<TenementApartment>`), `allResidents`,
  `evictedResidents`, `lastPayment` (`TimeAndDay`), `currentUpgradeProgresses`, `powerMain` (`PowerGrid`), `residentPowerConsumption`
  (600 W each). It subscribes to `TimeOfDayAzure.SecondsPassed` in `Start` (`:248`) and `DayChanged`.
- `TenementApartment` and `TenementGeneral` are plain `[Serializable]` classes, not MonoBehaviours. They are serialized inside
  the controller. `TenementApartment` (`TenementApartment.cs`): `apartmentNumber`, `apartmentFloor`, `examined`, one `current*`
  enum + `*Progress` per category (electricity, water, heating, bathroom, walls, balcony, balcony2, kitchen), `resident`,
  `onlyFurniture`, `moveStarted/moveSecondsLeft`, `evictionStarted/evictionSecondsLeft`, `silentRent`, `deposit`, `locked`,
  `cleaningTimerCompleteDay`, furniture counters. `TenementGeneral` is the building-wide equivalent (electricity, water, heating,
  stairs, elevator, facade, roof, basement, sauna, shrooms, workshop, mine, courtyard, ...).
- Apartment 1 is the player's own (`ApartmentRentUI.ShowRentButton` rejects `apartmentNumber == 1`).
- The physical rooms are `TenementApartmentPrefabs : SavableScript` components in the scene (`TenementApartmentPrefabs.cs`) and
  `TenementGeneralPrefabs` for common areas. On `Start` each registers itself with `TenementController.AddAndUpdateApartmentPrefab`.
  `UpdateApartmentPrefabs(TenementApartment)` `:238` destroys and re-instantiates the per-category prefab (electricity, water,
  heating, walls, bathroom, balconies, kitchen, furniture) from `content` (`TenementApartmentPrefabsContent`) based on the current
  upgrade level and resident, and rewrites GUIDs with the `APT_n_` prefix (`ProcessGUIDs` `:568`). Each apartment has a
  `FurnitureManager furnitureManager`.
- `TenementUtilities` (`TenementUtilities.cs`, requires `PowerGrid`): per-apartment power grid. `AssignToPowerGrid()` `:40` parents
  its grid to `TenementController.powerMain` only if the apartment's `currentElectricity != Off`. Static
  `UpdateApartmentPower(int)` `:66` uses `FindObjectsByType` (called on electricity upgrade completion).
- `TenementResident : SavableScriptableObject` is the tenant asset (`GetMonthlyRent`, `CheckRequirements` returning `CantRentReason`).
  `CharacterRent`/`CharacterRentDatabase` are data tables, `RentTrigger` is an empty stub.
- There is no `TenementRentHooks` type. Rent is driven by `TenementController` itself.

### 5.2 Rent, residents, upgrades: who changes what

Rent:

- Collection: `TenementController.DeltaSeconds(int)` (private, `:371`) runs on every game-second tick and calls
  `UpdateRentPayment(int)` `:385`, `UpdateProgesses(int)` `:423`, `UpdateEvictions(int)` `:446`.
  `UpdateRentPayment` asks `DifficultyController.instance.currentDifficultyInfo.GetRentPayment(lastPayment)` (`DifficultyInfo.cs:169`);
  by default payment fires from 19:00 on the pay day (Daily / Weekly day 6 / Biweekly / Monthly variants), sums
  `apartments[i].GetOcIncome()` and `GetRmIncome()`, shows a "Rent paid" notification and calls `Money.instance.AddMoney(oc, RM:false)` and
  `AddMoney(rm, RM:true)`. Money is local; a co-op mod must decide who gets paid.
- Renting: `ApartmentRentUI.OnRentButtonPressed()` (`ApartmentRentUI.cs:83`) -> `TenementController.RentApartment(TenementResident, TenementApartment, float moveTimeInHours, bool silentRent=false)` (`:802`).
  It adds the resident, sets `moveStarted`, `moveSecondsLeft = hours*3600` (18 h from the UI), computes `deposit`, pays collateral, grants achievements and starts
  `RentFade` (5 s black-screen coroutine that locks controls). Overload by apartment number: `RentApartment(TenementResident, int, float, bool)` `:792`.
  `TenementRentApartment` is the scene-scripted version (`rentAtStart`, `RentApartment()`, `EvictResident(int)`).
- Move-in completes in `UpdateEvictions` -> `MoveResident(TenementApartment)` `:855`, which refreshes the apartment prefabs, queues the tenant's `TenementEvents`
  and calls `UpdateResidentPower()` `:1204`.
- Eviction: `StartEviction(TenementApartment, float hours)` `:875`, `CancelEviction` `:882`, `EvictResident(TenementApartment, bool silent=false, bool updatePrefabs=false, bool leaveFurniture=false)` `:889`
  (refunds `deposit` or creates an invoice, removes the tenant, optionally destroys the NPC, `onlyFurniture` mode keeps their stuff).

Upgrades:

- Start: `TenementApartment.StartUpdate(TenementUpgrade, int requiredTimeHours=-1, bool silentUpgrade=false)` `:411` and
  `TenementGeneral.StartUpdate(...)` `:214` take items/money (`upgrade.TakeRequiredItemsAndMoney()`), build a `TenementUpgradeProgress`, store it in the
  category field and call `TenementController.StartUpgrade(TenementUpgradeProgress, bool)` `:306`. UI entry is the upgrade panels;
  scene scripts use `TenementUpgradeStart.Upgrade()`.
- Timer: `UpdateProgesses` decrements `TimeSecondsLeft` per game second; at zero `OnUpgradeDone(progress)` `:346` -> `TenementUpgradeProgress.OnUpgradeDone()`
  (`:107`, marks `upgradeReady`, adds a "final payment" objective).
- Finish: player pays the final bill -> `TenementUpgradeProgress.FinishUpgrade(bool silentUpgrade=false)` (`TenementUpgradeProgress.cs:180`) ->
  `TenementApartment.Update(TenementUpgrade)` `:494` or `TenementGeneral.Update(TenementUpgrade)` `:351`. These set `current*`, null the progress, and call the
  `prefabs.Update*` methods that swap the prefab. Electricity also calls `TenementUtilities.UpdateApartmentPower`.

Utilities coupling: `TenementController.UpdateUtilities()` `:1198` -> `CheckElectricity()` `:1227` sets `powerMain.SetBaseBalance(0/8000/15000/100000/1000000)`,
`unlimitedCapacity` and `unstableSupply` from `general.currentElectricity` and the `"TenementElectricity"` subscription. Water in the building is the
`TenementWater` upgrade prefab plus `LiquidStorage` taps (see 6.4).

### 5.3 State to sync (tenement)

Everything is inside `TenementController`'s saved state: `general` upgrade levels + progress, each `TenementApartment` (levels, progress with `TimeSecondsLeft`,
resident, move/eviction timers, deposit), `allResidents/evictedResidents`, `lastPayment`, plus `FurnitureManager.placedFurniture` per apartment. Timers all derive from
the shared game clock, so syncing the clock and the discrete actions (rent, evict, start upgrade, finish upgrade) is enough; do not stream the countdowns.
Because apartment contents are destroyed and re-instantiated when anything changes, any mod component attached to an object inside an apartment prefab will vanish on upgrade.

### 5.4 Hook points (tenement)

```
[HarmonyPostfix] TenementController.RentApartment(TenementResident, TenementApartment, float, bool)
[HarmonyPostfix] TenementController.StartEviction(TenementApartment, float) / CancelEviction(TenementApartment)
[HarmonyPostfix] TenementController.EvictResident(TenementApartment, bool, bool, bool)
[HarmonyPostfix] TenementController.MoveResident(TenementApartment)
[HarmonyPostfix] TenementController.StartUpgrade(TenementUpgradeProgress, bool)       // returns TenementUpgradeProgress
[HarmonyPostfix] TenementUpgradeProgress.FinishUpgrade(bool)
[HarmonyPrefix]  TenementController.UpdateRentPayment(int)  // private; skip on clients so only the host pays out
[HarmonyPostfix] TenementApartmentPrefabs.UpdateApartmentPrefabs(TenementApartment)   // fires whenever the room's prefabs are rebuilt
```

Apply on a remote: `RentApartment(resident, apartmentNumber, hours, silentRent:true)` (silent skips the achievements and the 5 s fade, but also
skips collateral payment; money must be handled separately). `ResidentRented(...)`, `CanRentApartment(double)` and `GetApartmentFromNumber(int)`
are the lookups. Rent payment should run on exactly one peer.

## 6. Power and utilities

### 6.1 Model

- `PowerGrid : SavableScript, IPowerGridListener` (`PowerGrid.cs`, 596 lines). A node with `BaseCapacity`, `GridEnabled`, `unlimitedCapacity`, `unstableSupply`, an
  optional `ParentGrid`, and a dictionary of subscribed `IPowerGridListener -> bool powered`. `GUID` and `global`. Saved: `GridEnabled`, `BaseCapacity`,
  `unlimitedCapacity`, `cumulativeBalance`, `savedTimeAndDay`. Child grids export surplus to the parent. Tenement main grid is `TenementController.powerMain`.
- `IPowerGridListener` (`IPowerGridListener.cs`): `IsOn`, `PowerBalance` (negative = consumes, positive = generates), `OnPowerGridUpdated`, `OnGridPowerOff`, `OnGridPowerOn`.
  Implemented by `PowerGrid`, `PowerSource`, `PowerGridListener` (lights, appliances), `TenementResidentPower`.
- `PowerGrid.UpdateGridStatus(bool gridtoggled=false)` `:469` -> private `RecalculateGrid(bool)` `:516`: sums balances, and when over capacity switches off the most
  recently switched-on listeners first (`listeners[key]=false`, `OnGridPowerOff`), otherwise switches back on in first-on order. It re-enters up to 8 times and
  recurses into child grids. Which listener loses power is therefore order dependent (`switchOnOrder` counter) and not deterministic across peers unless events arrive in the same order.
- `PowerSource` (`PowerSource.cs`) is a `[Serializable]` helper held in `CraftingBase._Power` (and used by `Generator`). `Powered` is `PowerInput` for plain devices or `_Fuel.active` when
  `RequiresFuel`. `isGenerator`/`powerConsumption` negative = generation. `TogglePower(bool)` `:362`, `ToggleInput(bool)` `:346`, `ToggleFuel(bool)` `:377`, `FuelStateChange(bool)` `:386`,
  `JoinGrid(PowerGrid)` `:208`, `LeaveGrid()` `:198`, `OnPowerOn()` `:286`, `OnPowerOff()`.
- `Fuel` (`Fuel.cs`): burn timer for generators/stoves. `Toggle(bool)` `:122`, `StartFuelCounter()` `:311`; ticks via `TimeOfDayAzure.SecondsPassed` (`DeltaSeconds` `:323`) decrementing
  `BurnTimeRemaining` and consuming the fuel slot item.
- `CraftingBase : Interactable` (`CraftingBase.cs`, 1800+ lines) is the base for machines. `Toggle(bool on)` `:1577` is the user-level on/off: calls `_Power.TogglePower(on)`, `OnSetPowerOn/Off`, sets `On`, and
  `PowerGrid.UpdateGridStatus()`. Grid-driven changes go through `OnPowerOut()` `:1614` / `OnPowerOn()` `:1637`; `Break()` `:1652`.
- `Generator : CraftingBase` (`Generator.cs`, 96 lines): `powergeneration` (default 500 W) set as `_Power.powerConsumption = -powergeneration` in `Start` `:40` when `linkToGrid`.
  Switching a generator on is `CraftingBase.Toggle(true)`; its fuel burn then supplies the grid. `Generator.Update()` `:28` only refreshes UI while `manuActive`.
- `PowerGridListener : SavableScript` (`PowerGridListener.cs`): generic consumer (lights, radios). Saved `On`. `Toggle(bool)` `:199`, `JoinGrid`/`LeaveGrid`, `SetPowerConsumption(int)` `:185`. Fires `OnPowerOn/OnPowerOff` `output[]`
  (wired to `LightTurnOffOn.TogglePower` etc). Adds a `PowerLightFlicker` when the grid `Flickering`.
- `PowerMeter : Interactable`: read-only display of a grid's `cumulativeBalance`; `TogglePowerGrid(bool)` `:124` is the main breaker call (`powerGrid.Toggle`). `PowerGrid.Toggle(bool)` `:430`, `SetBaseBalance(int)` `:440`.
- `BatteryPowerSource : SavableScript` (`BatteryPowerSource.cs`, needs a `Storage`): power state derived from fuel items inside its storage (ammo-data or liquid-data metadata). `CheckItems()` `:111` decides
  `PowerON()` `:172` / `PowerOFF()`; `ToggleDrain(bool)` `:166` enables draining; `DeltaSeconds` drains the item metadata per game second. Output is only `Relay[] RelaysON/RelaysOFF`, there is no grid connection. On load it
  catches up using `savedTimeAndDay`. A battery's real state is the metadata on the `ItemStack`s in its `Storage`, so syncing storage contents syncs the battery.
- `PowerGridsController` (`PowerGridsController.cs`, GUID "PowerGridsController"): saved snapshot list (`SavedGrid`: net balance, cumulative, last update) so unloaded levels can report numbers.

### 6.2 Blackout

There is no power blackout system. `BlackoutController` / `BlackoutTrigger` / `BlackoutEvent` implement the player passing out:
fade to black, optional teleport/scene change, sounds, steal money/items (`stolenOCPercent` ...), then a wake-up animation (`BlackoutController.SetScheduleWakeUp`, `WakeUpSequence`, `FinishWakeUp`; pending state saved
so it survives the scene change). `BlackoutTrigger.StartBlackout()` is the entry point. That is player-state and local; relevant for co-op only to keep one player's blackout from freezing the other.
Power outages are simply grid over-capacity or `GridEnabled=false` (`PowerMeter.TogglePowerGrid`), plus `unstableSupply`, which only makes lights flicker (`PowerGrid.Flickering` -> `PowerLightFlicker`).

### 6.3 What to sync (power)

Discrete: `PowerGrid.GridEnabled`, `BaseCapacity`, parent link changes; each `CraftingBase.On`/`broken`; `Fuel` state (`fuelCounterOn`, `BurnTimeRemaining`, fuel slot contents); `PowerGridListener.On`;
`LightTurnOffOn.on`/`noPower`. The "powered or not" outcome of `RecalculateGrid` is derivable, but because shedding order depends on switch-on order it is safer for one peer (host) to run grid math and
broadcast listener results (`OnGridPowerOff/On`). If every peer runs its own grids, apply remote toggles with the same game-event ordering or accept drift.

### 6.4 Hooks (power)

```
[HarmonyPostfix] PowerGrid.Toggle(bool)  /  PowerGrid.SetBaseBalance(int)  /  PowerGrid.SetParentGrid(PowerGrid)
[HarmonyPostfix] PowerGrid.UpdateGridStatus(bool)           // very frequent; use only to snapshot
[HarmonyPostfix] CraftingBase.Toggle(bool)                   // machine/generator on-off by player
[HarmonyPostfix] CraftingBase.OnPowerOut() / OnPowerOn()     // grid-forced changes (return bool)
[HarmonyPostfix] CraftingBase.Break()
[HarmonyPostfix] PowerGridListener.Toggle(bool)  /  PowerGridListener.OnGridPowerOff(PowerGrid) / OnGridPowerOn(PowerGrid)
[HarmonyPostfix] PowerSource.JoinGrid(PowerGrid) / LeaveGrid()
[HarmonyPostfix] Fuel.Toggle(bool)
[HarmonyPostfix] LightTurnOffOn.turnLightOn() / turnLightOff() / TogglePower(bool)
[HarmonyPostfix] BatteryPowerSource.CheckItems()  /  BatteryPowerSource.ToggleDrain(bool)
```

Placing or taking powered furniture re-subscribes listeners via `BuildingSystem.ReconnectPower`/`DisconnectPower` (private, `:1665`/`:1646`), which call `PowerSource.JoinGrid/LeaveGrid` and `PowerGridListener.JoinGrid/LeaveGrid`.

### 6.5 Water

- `WaterController` (`WaterController.cs`) is only the player's swimming/wading state (submersion, splash prefabs, `playerIsSwimming`). Nothing to sync except as player animation state.
- `WaterSourceController` (`WaterSourceController.cs`): a per-scene lookup of "global" `LiquidStorage`s with `globalStorage` property (`:24`) preferring `info_water_source.instance.globalLiquidStorage`.
  `LiquidStorage` instances with `Capacity < 0` and no external output use it as an infinite tap (`LiquidStorage.cs:252`). So tap water is not simulated; it is gated by the tenement water upgrade swapping prefabs.
- Real liquid state lives in `LiquidStorage : Interactable` (`LiquidStorage.cs`): `SetLiquid(...)` `:676-706`, `AddLiquidFromItem(ref ItemStack)` `:648`, `FillItem(ref ItemStack)` `:776`, `EmptyTank()` `:546`, `ClearLiquid()` `:836`,
  `DeltaSeconds(int)` `:553` (evaporation/process ticks). Hook `FillItem`, `AddLiquidFromItem`, `SetLiquid`, `ClearLiquid` if tanks must be consistent.

## 7. Transport

### 7.1 TrackTrain and Track_Segment

Two layers. `TrackTrain : SavableScript` (`TrackTrain.cs`, 2191 lines) is a kinematic rail vehicle. `Track_Segment : SavableScript` (`Track_Segment.cs`, 1281 lines) is a spline segment with branches. `TrainController`
(`TrainController.cs`, `TrainController.instance`, `[ExecuteInEditMode]`) keeps dictionaries of trains, segments and fuel tanks by GUID: `GetTrackTrain(string)`, `GetSegment(string)`, `TrackTrains`.
`Minitrain` (`Minitrain.cs`) is the player-driven cab on top of a `TrackTrain` (levers, fuel, lights). `MinitrainSwitch` is the track switch.

`TrackTrain` public state (all saved unless noted): `active`, `braking`, `currentSpeed`, `targetSpeed` (the property `TargetSpeed` returns 0 when `!hasFuel`; `hasFuel` is `[fsIgnore]` and set each `FixedUpdate` by `Minitrain`),
`frontAxle`/`backAxle` (`TrainAxle`: `currentSegment` as a `TrackSegmentReference` with GUID, `directionOnSegment`, `positionOnSegment`), `frontCoupler`/`backCoupler`, `currentPosition`, and `train` (`TrainData`: wagon list and the driving loco).
Movement is purely `Update()` (`:1316`) -> `CalculateSpeed()` (`:1369`, gravity, power, drag, braking) -> `CalculatePositionAndRotation()` (`:1684`) using `Time.deltaTime`. Only the "driving" loco of a consist computes position
(`train.IsDriving(this)`); wagons follow via `ApplyValuesOnWagons()`. Speed and position therefore depend on each machine's frame times, so two peers simulating the same train will drift.

Control API: `Toggle(bool on)` `:1030` (activates/deactivates the engine), `ApplyBrakes(bool)` `:1044`, `ToggleBrakes()` `:1065`, `SetSpeed(float)` `:1086` (writes `TargetSpeed`, clamped), `SetSpeedInstantly(float)` `:1099`, `ForceStop()` `:1125`,
`SetAsDriving()` `:1120`, `CoupleWithFront/Back(TrainCoupler)` `:1176/:1181`, `UncoupleWagons()` `:2052`, `Crash(GameObject other, bool dontinvert=false)` `:1549`, `ResetTrackTrain()` `:1791`.
Player input: `Minitrain.FixedUpdate()` (`Minitrain.cs:186`) reads the Forward/Backward/Brake/Neutral keys while `playerUsing` and calls `train.SetSpeed`/`ApplyBrakes` every FixedUpdate. `Minitrain.StartMinitrain()` `:342`,
`StopMinitrain()` `:355`, `ChangePlayerUsingState(bool)` `:385`.

Track switching: `Track_Segment.SetBranchIndex(int)` `:1097`, `SetBranchIndexDontSetPrevious(int)` `:1109`, `SetNextSegment(Track_Segment)` `:1117`, `SetPreviousBranch(...)` `:1126/:1134`. Player-facing:
`MinitrainSwitch.ChangeTrack(int)` `MinitrainSwitch.cs:112` (refuses if the segment is occupied) and `ChangeTrackForce(int)` `:133`. Segment fields: `activeBranchIndex`, `activePreviousSegment`, `occupied`. `output[] OnPass/OnOccupied/OnFree`
fire from the train as it passes (`TrackTrainSpeedRelay` hooks `OnPass`).

Sync: authority should be a single peer per train. Replicate the discrete controls (`Toggle`, `braking`, `TargetSpeed`, `SetAsDriving`, coupling, switch branch indices) and send periodic `{front/back axle segment GUID, positionOnSegment, directionOnSegment, currentSpeed}` snapshots; on
receivers either set `TargetSpeed` and let the local sim run with corrections, or freeze the local sim and drive `CalculatePositionAndRotation` yourself. After a snapshot apply, the game's own `RepairPlacementIfInvalid()` (private `:1925`) shows
that axle placement can become invalid, so validate `Track_Segment.TrackSegmentReference.segment != null`.

Hooks:

```
[HarmonyPostfix] TrackTrain.Toggle(bool) / ApplyBrakes(bool) / ToggleBrakes() / SetSpeed(float) / SetSpeedInstantly(float) / ForceStop()
[HarmonyPostfix] TrackTrain.CoupleWithFront(TrainCoupler) / CoupleWithBack(TrainCoupler) / UncoupleWagons() / Crash(GameObject, bool)
[HarmonyPostfix] Track_Segment.SetBranchIndex(int) / SetNextSegment(Track_Segment) / SetPreviousBranch(uint)
[HarmonyPostfix] MinitrainSwitch.ChangeTrack(int) / ChangeTrackForce(int)
[HarmonyPostfix] Minitrain.StartMinitrain() / StopMinitrain() / ChangePlayerUsingState(bool)
```

`SetSpeed` is called every FixedUpdate while a key is held (see above), so rate-limit; sync the target only when it changes by more than a threshold.

### 7.2 Elevator (`Elevator.cs`)

`Elevator : SavableScript`, GUID `[fsIgnore]` public field. Config: `Floors[]` (`FloorData`: `position`, `door`, `onFloorReached`, `enabled`), `Speed`, `QueueMax`, `WaitTime`, `AutoOpenCloseDoors`, `powered`. Saved: `Position` (float floor index), the queue
(`List<float> Queue`), `direction`, `moving`, `doorsclosed`, `StopTime`, `powered`. Motion is in `FixedUpdate()` `:183` using `Time.deltaTime` and a state machine on `moving` (-1/0/1). Control: `Call(int floor)` `:374` (adds to queue, sorted; ignored when unpowered or floor disabled),
`MoveToOneFloorUp/Down(bool dontTriggerOnMove)` `:366/:358`, `SetFloorEnabled/Disabled(int)` `:411/:419`, `TogglePower(bool)` `:492`, `ForceCloseDoors()` `:476`, `SetPosition(float)` `:294`.
The player ride relies on physics parenting, not on Elevator code. Sync `Call` as events plus a periodic `Position` snapshot; hook `Elevator.Call(int)` and `TogglePower(bool)`. `StopTime` uses `Time.time`, so it is not portable between machines; resync by re-calling rather than copying it.
The elevator doors are normal `Door`s and sync through section 3.1.

### 7.3 FastTravelController and scene changes

`FastTravelController` (`FastTravelController.cs`) only has `ToggleMenu()`, which opens `MenuPanels.FastTravel` (and `GameController.cs:144` closes it on Interact/Menu). It does no travelling. The actual transition is
`SaveController.ChangeLevel(string newlevel, string newLevelEntrypoint = "NONE")` (`SaveController.cs:307`): waits for `Loading` to clear, raises `PlayerWillChangeLevel`, runs `OnMapChanging` on everything, writes an autosave (`SaveGame`),
then loads the scene and the save (`LoadGameWithMigration`). `Changelevel.ChangeLevel()` (`Changelevel.cs:72`) is the usual caller. Hook `SaveController.ChangeLevel(string, string)` (static) to learn that the local player is leaving; every scene object
(including your mod's per-object ids) is rebuilt afterward.

## 8. Animals and other world entities

- `Animal : VersionedMonoBehaviour` (`Animal.cs`): wildlife/pets on the A* `AIPath`. Public `tame`, `pathingDisabled`, `escapeTarget`; internal state machine `AnimalState` (Default, Attacking, Eating, Sleeping, Sitting ...) and `IdleAction`.
  Behaviour in `Update()` `:357` (flee from player within 1.5 m, obstacle avoidance, destination selection) and public actions `Attack(Stats)`, `Sit()`, `Sleep(bool)`, `Eat(CollectibleItem)`, `HuntTarget(Transform)`, `SetDestination(...)`, `ChangeTameStatus(bool)`.
  Movement is local AI with `PlayerLocator.instance` hard-wired as the only threat/target, so in co-op a remote player is invisible to the AI unless you patch that.
- `AnimalStats : Stats` (`AnimalStats.cs`) holds hunger/growth (`UpdateHunger(int)`, `UpdateGrow(int)` on `SecondsPassed`), `GUID`, `savedTimeAndDay`, and `GetAnimalStatsMeta()/SetAnimalStatsMeta(ItemAnimalData)` which is how an animal is serialised into an item.
- Animals are items: an animal GameObject also carries `CollectibleItem`, so "pick up animal" is an inventory item with `ItemAnimalData` meta.
- `AnimalController : SavableScript` (`AnimalController.cs`, GUID "AnimalController"): `animalLimit = 30`, `obstacleAvoidanceAnimalLimit = 50`, `followingAnimals`. On `OnMapChanging()` (`:36`) it serialises tame followers into `savedFollowingAnimals` and destroys them; on `OnMapChanged()` it respawns them near the player
  (`RespawnFollowingAnimals`). Followers are per-player and need per-player ownership in co-op.
- `AnimalSpawner : CollectibleItemSpawner` (`AnimalSpawner.cs`, 17 lines): on save, copies each spawned animal's transform into `spawnPositions/spawnRotations`; spawn logic is the base `CollectibleItemSpawner`/`Spawner` (random point/radius spawn, items saved by index).
  Spawning uses `UnityEngine.Random`, so peers diverge unless one peer spawns and replicates. `AnimalBreeder` (`AnimalBreeder.cs`) adds offspring over game time per breeder GUID.
- Hook ideas: `Animal.ChangeTameStatus(bool)`, `Animal.Attack(Stats)`, `AnimalController.OnMapChanged()`, and for spawn authority `Spawner.PointSpawn/RadiusSpawn` overrides on `CollectibleItemSpawner` (`:107`, `:126`).
  State to sync for a wandering animal is position, yaw, `AnimalState`, `tame`, current target; or make the host authoritative and drive remote animals with `pathingDisabled = true` and interpolation.

## 9. Per-frame and heavy code (performance notes)

Every one of these runs for each instance in the loaded scene unless noted:

- `InteractObjects.Update()` -> `RaycastInteract()` every frame, and `UpdateUseMessage()` on the focused interactable every frame (some overrides call `UseText.ChangeUseText`). Third person additionally runs
  `GetInteractablesWithinSphere` (`:245`): `Physics.OverlapSphere` with a ~6.8 m radius, `GetComponent<Interactable>()` for every collider, and up to 3 raycasts per candidate, every frame. Several allocations (new lists) per call.
- `Door.Update()` (`Door.cs:866`) runs on every door each frame. When idle it still evaluates the `State/position/AutoClose` branches (cheap, but it is hundreds of doors). `Door.Awake` adds a `GraphUpdateScene` and `Start` adds a trigger `CapsuleCollider` per door.
- `LightController.Update()` (`LightController.cs:238`) runs on every `Light` that has one. Distance checks are throttled to every 9th frame (random per-light offset) unless `SmoothTransition` or `forceUpdate` is on. Note that `Place()`, `PlaceFurniture` and `FurnitureInfo.LoadFromPath` try to add a `LightController` with `SmoothTransition = true` when `light.GetComponents<LightController>() == null`, but `GetComponents` never returns null, so that branch is dead code; prefab lights are expected to carry the component already.
  `LightsController.FixedUpdate()` (`:103`) iterates all `LightCullZone`s each physics step calling `CheckIfPlayerInZone()`.
  `LightFlicker.Update` / `PowerLightFlicker.Update` per flickering light.
- `BuildingSystem.StartPreview()`/`ShowPreview()` (`:878`/`:1174`): only while the build menu is open, but each frame does a raycast, `FurnitureAttachmentPoint.FindClosest` over every attachment point (`FurnitureAttachmentPoint.cs:43`, linear scan with vector math),
  `GetComponentInParent` calls, material swaps (`renderer.materials` allocates per call in `ShowRemovableObject`/`SetColorToNormal`).
- `Elevator.FixedUpdate()` and `TrackTrain.Update()` (+ `RotateWheels`, `ApplyValuesOnWagons`, `Select/Sum` LINQ in `CalculateSpeed` every frame) for each active train. `Minitrain.FixedUpdate()` per cab.
- `PowerMeter.Update()` per meter: string formatting with `Math.Pow`/`Math.Round` each frame when `textdisplay` is set. `Generator.Update()` only with its panel open.
- `Animal.Update()` (`Animal.cs:357`) per animal (up to 30 plus followers), with `Vector3.Distance` to the player and an animator update; `ObstacleAvoidance.Tick` for up to 50.
- Event-driven but heavy when it fires: `PowerGrid.RecalculateGrid` (LINQ `OrderBy`, `ToArray`, recursion into children, up to 8 passes); `TenementController.DeltaSeconds` every game second (cheap unless a rent/upgrade/eviction is due);
  `TenementUtilities.UpdateApartmentPower` and `SaveController.GetObjectByGUID(guid, Type)` use `FindObjectsOf*`.
- Saves: `FurnitureManager.OnSavingGameTertiary` and the whole save pipeline touch every `SavableScript` via `GetComponentsInChildren` on all scene roots. Hooking `OnSavingGame` for each type is cheap per call but the number of calls is large.

## 10. Gotchas

1. Interact() is local-only. Do not drive remote actions through `Interact()`; call the state methods, behind a re-entrancy guard so your postfixes do not rebroadcast.
2. Public `Open*` on `Door` consults the local player's keys. Remote opens must use `ForceOpen*`/`Close` and sync `Locked` separately.
3. Door direction is encoded in a private `ReversedDir` plus a sign flip of `OpenAngle`; a mirrored apply must pass the same side.
4. Many gameplay events are private `Relay` fields on prefabs. They exist only in scene/prefab data, so they are not discoverable without reflection, and `Relay.fireOnceOnly/firedOnce` can silently suppress a re-fire.
5. Placed-furniture GUIDs are regenerated on every save (`manager.GUID + n`, reverse order) and are empty until the first save after placement. Assign your own ids.
6. `BuildingSystem.Place()` does not raise `FurnitureManager.FurniturePlaced`. Only `PlaceMovingItem` does. Taking raises `FurnitureTaken` (from `FurniturePlaceable.Take`).
7. `Move()` for furniture = Take + Place. A naive hook on both will see a remove then an add with the same type; correlate them if you want a "move" message.
8. Taking furniture pushes it into the local `availableFurnitures` and `TaskItemsManager`. Remote removals must avoid that path or the remote player's items duplicate into yours.
9. Apartment content is torn down and re-instantiated on upgrade, resident change and `UpdateApartmentPrefabs`. References to child objects go stale; look them up by GUID (with the `APT_n_` prefix) each time.
10. `TimeOfDayAzure.SecondsPassed` handlers receive game seconds and may be called with large values when time is skipped (sleep). Subscribers like `BatteryPowerSource.LoadDelay`, `Fuel`, `TenementController` handle big jumps but can fire many times in one frame.
11. `TenementController.UpdateRentPayment` pays out on whichever machine runs it. Disable it on clients.
12. Blackout in the codebase means the player passing out, not a power cut. Power failure is just `PowerGrid.GridEnabled=false` or over-capacity.
13. Train and elevator motion use `Time.deltaTime`/`Time.time`, so independent simulation drifts. Pick an authority.
14. `Button.Interact()` starts a 10 ms real-time polling coroutine while waiting on lock or unlock delay; if you suppress `Trigger` the coroutine keeps polling until `StopInteract`.
15. `SaveController.GetObjectByGUID(string, Type)` scans the scene with reflection. The one-argument overload only works for objects registered during load.
16. Level change = save + scene unload + load. Any mod-side id table keyed by Unity instance must be cleared on `SaveController.PlayerWillChangeLevel` / `OnMapChanging`, and co-op peers must handle the other player being in a different scene.
17. `Interactable` is the base class for 56 types but `WorkableResourceSource` implements `IInteractable` alone, so patches or lookups on `Interactable` miss it.
