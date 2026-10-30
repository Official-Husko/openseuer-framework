# World: doors, interactables, furniture, containers and power

`ObenFramework.World` wraps the parts of the game's world that mods and the co-op mod need most: doors, the things the player can use, lights and switches, placed furniture (and placing, moving and removing it from code), containers, items lying on the floor and the power grid. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.World;`.

Background on how the game does these things, and why the hooks are where they are, is in [../../game/world-building.md](../../game/world-building.md). The co-op notes in [../../research/coop-reference.md](../../research/coop-reference.md) (section 5) list the same game methods; the events below are the ones a mod that mirrors the world needs.

What is covered: doors and their opening hours, key locks and keypads, interactables, light fixtures, wall switches, placed furniture, containers (storage), floor items, power grids. Elevators, ladders, tarp doors, trains, drones and level changes are in [transport.md](transport.md); custom furniture in [furniture.md](furniture.md).

Props that mods place and keep in the save are in [world-props.md](world-props.md); a level's logic objects (relays, branches, counters, state machines), map landmarks, editable signs and per-object mod data in [scene-logic.md](scene-logic.md); character looks read from scene files in [scenelook.md](scenelook.md).

## Reading the rules once

All events share the same behaviour.

- **Subscribe with an owner.** `Doors.DoorChanged.Subscribe(this, change => ...)` ends with your mod, so a hot reload removes the handler. Without an owner you get an `IDisposable` back. Each handler runs in its own try/catch, so a throwing handler cannot stop the others or the game.
- **Nothing runs per frame.** Each event is a Harmony postfix on a game method that returns at once while nobody is subscribed, and raising an event does not allocate.
- **Only in a settled world.** Events are not raised while a save loads or between scenes (when the game applies saved state to thousands of switches). Use `GameEvents.WhenWorldReady` for the initial state and events after that.
- **Every change says who caused it** (below).

### Change sources and echo

Each event payload has a `Source` of type `ChangeSource`:

| Source | Meaning |
|---|---|
| `Game` | a character, a timer, a script or a relay |
| `LocalPlayer` | the local player, by using something or in the build menu |
| `Mod` | the framework's setters, or any code inside a `WorldChanges.Enter(ChangeSource.Mod)` scope |

A mod that sends changes to other machines forwards `Game` and `LocalPlayer` events and ignores `Mod` ones; when it applies a remote change it uses the setters below (or its own scope), so its own change comes back as `Mod` and is not sent again:

```csharp
Doors.DoorChanged.Subscribe(this, c =>
{
    if (c.Source == ChangeSource.Mod) return;       // our own doing, already in sync
    Send(c.Id, c.Kind, c.State);
});

// on the receiving side
Doors.Apply(door, receivedState);                  // runs as ChangeSource.Mod

// calling the game directly? mark it yourself
using (WorldChanges.Enter(ChangeSource.Mod))
    door.ForceOpen();
```

`ByLocalPlayer` on the payloads is `Source == LocalPlayer`. The player scope covers everything the interact key triggers (`InteractObjects.UseInteractable`) and the build menu's place, take and move. A wall switch's press runs a moment after the key press; the framework carries the player's attribution through to what the switch drives (lights, doors) for presses that start within 5 seconds of the player using it.

## Addresses: pointing at a scene object

`ObjectAddress` names one component across sessions and machines.

```csharp
var address = ObjectAddress.Of(door);     // or Interactables.Describe(c).Address
string text = address.ToString();          // "Level1|Door|3f2a...|Apartment/Hall/Door#1"
...
Component back = ObjectAddress.TryParse(text, out var a) ? a.Resolve() : null;
```

It holds the scene name, the component's class name, the game's `GUID` where the object has one, and the hierarchy path. Resolving tries the GUID first (the game sets GUIDs in the editor for doors, buttons, lights, grids and most other scene objects, and they are stable), then the path. In a path, `#n` after a segment picks between siblings that share a name (`Door#1` is the second "Door" under that parent); characters that would break the text form inside a name are percent-escaped.

Things spawned at runtime (placed furniture, dropped items) have neither a stable GUID nor a stable path. Placed furniture has its own ids (below), floor items and containers theirs (`FloorItems`, `Containers`).

`ObjectResolver.Find<T>(text)` is the loose version used by the facades' `Find(id)`: it accepts the full text, a bare GUID, a hierarchy path or `#instanceId`. GUID lookups use an index built once per scene (a scan of all `SavableScript`s), so the first call in a scene costs one scan; later calls are dictionary reads. None of this is for per-frame use.

## Doors

```csharp
foreach (Door door in Doors.All()) { ... }
Door door = Doors.Find(id);
string id = Doors.IdOf(door);               // the GUID if it has one, else the full address text
DoorState state = Doors.StateOf(door);      // Phase, Position (0-1), Locked, Opposite, IsOpen
```

`DoorPhase` is `Closed`, `Opening`, `Open`, `Closing` or `Ajar` (stopped part way, only on doors that allow it). `Opposite` is the game's private `ReversedDir`: the door is swinging the other way from its default, which a remote copy has to repeat or the door swings through the wall.

Changing doors:

| Call | Does |
|---|---|
| `Doors.Open(door, swing = Default, respectLock = false)` | `ForceOpen()` or `ForceOpenOpposite()`: opens past the lock like a character. With `respectLock` a locked door stays shut. An open or opening door is left alone. Returns true if open or opening afterwards. |
| `Doors.Close(door)` | `Close()`. |
| `Doors.SetLocked(door, locked)` | `Lock()` / `Unlock()`, which fire the door's lock relays. |
| `Doors.Apply(door, state)` | Lock first, then open (same swing side) or close, from a received `DoorState`. A door stopped part way is left to finish by itself. |

These never use `Open()`/`UseDoor`, which check the local player's keys and need the player's position.

### DoorChanged

`Doors.DoorChanged` carries a `DoorChange`: `Door`, `Kind`, `Source`, `State` (after the change) and `Id` (built on demand). Kinds:

- `Opening`, `Closing`: a swing started, or turned around mid-swing.
- `Opened`, `Closed`: the swing ended. They carry the source of the swing that led to them.
- `Locked`, `Unlocked`.

How it works: patches on `OnOpen`, `Close`, `ToggleInterrupt`, `OnFullyOpen`, `OnFullyClosed`, `Lock`, `Unlock` and `ToggleLock`. The game's door methods call each other, so instead of counting calls the hook compares the door's state before the outermost call with its state after each call, which reports each change exactly once. Limits:

- Unlocking with a key or a lockpick sets the lock's flag inside the game's `Lock` class; it is reported when the door next acts (opening after a key goes through `Unlock()` and is reported at once).
- A door that starts an auto close from its own Update (a timer) is reported when it settles (`Closed`) but not as `Closing`.

### Opening hours

Shops and other places lock and unlock their doors on the clock through the game's `DoorSchedule` (a table of day, opening hour, closing hour). `DoorSchedules` reads and changes it:

```csharp
DoorSchedule schedule = DoorSchedules.Of(door);            // null when the door has no schedule (a scan)
List<OpeningHours> hours = DoorSchedules.HoursOf(schedule); // Day (DayOfWeek), Opens, Closes; empty = always locked
bool open = DoorSchedules.IsOpenNow(schedule);
IDisposable change = DoorSchedules.SetHours(this, schedule, DoorScheduleRules.EveryDay(6, 22));
Vector3? stand = DoorSchedules.StandPositionOf(door);       // where characters stand to use the door
```

`SetHours` lasts until your mod unloads or you dispose the result; then the level's own hours come back (unless another mod changed them since). The doors follow at the next game minute. The game does not save schedules, so set them again from a world-ready handler if they should last. `DoorScheduleRules` (plain C#, unit tested) holds the conversion: the game's day 0 is Monday, and a door is open from the opening hour until before the closing hour.

## Locks and keys

Ten game classes carry a key lock (the game's `Lock` object): doors, doors to other levels, storages, liquid storages, stations (`CraftingBase`), the game's world `Button`, selection buttons, item put spots, breakable padlocks and market stalls. `Locks` works on the component that holds the lock:

```csharp
foreach (Component owner in Locks.All()) { ... }           // a scan of the ten types
Component owner = Locks.Find(id);                           // id from Locks.IdOf(owner)
LockInfo info = Locks.Describe(owner);                      // Kind, Locked, Lockpickable, Difficulty (0-2), KeyName, PlayerHasKey, RelockMinutes
Locks.SetLocked(owner, false);                              // doors through Lock()/Unlock() so their relays fire
Locks.SetLockpickable(owner, true);
Locks.SetDifficulty(owner, 1.5f);
TaskItem key = Locks.KeyOf(owner);                          // null when no key opens it
Locks.SetKey(owner, someKey);                               // any key task item; part of the level design, not saved
Locks.GiveKey(owner);                                       // hands the player the key with the game's notification
Locks.StartLockpick(owner, ok => Log.Info("picked: " + ok));
```

`StartLockpick` starts the game's lockpicking on a locked, pickable lock. The game cancels lockpicking when the player looks away from the lock, so start it while they face it (from an interaction). The callback gets false on failure or cancel.

Events: `Locks.Changed` (`LockChange`: `Owner`, `Locked`, `Source`) for every lock and unlock, whether by key, lockpick, timer, schedule, script or mod, and `Locks.Lockpicked` (`LockpickResult`: `Owner`, `Success`). A lock is a plain object inside its component, so the framework finds the component through an index built on first use per scene (rebuilt at most every two seconds when a new piece of furniture shows up).

### Keypads

Keypads and rotary combination locks have a code instead of a key:

```csharp
foreach (Keypad keypad in Keypads.All()) { ... }
int[] code = Keypads.CodeOf(keypad);
Keypads.SetCode(keypad, new[] { 4, 7, 1, 1 });   // 1 to 4 digits; lasts until the level loads again
Keypads.SetOpen(keypad, true);                   // success sound and relay, as if the right code was typed
bool powered = Keypads.HasPower(keypad);
```

`Keypads.Changed` reports `Solved`, `WrongCode` and `Locked`.

## Interactables

The game's player raycast picks one `IInteractable` per frame; the interact key calls it.

```csharp
Component target = Interactables.Target;                 // under the crosshair, null if none (a field read)
InteractableInfo? info = Interactables.TargetInfo;       // Component, Address, Title, Disabled, TypeName

Interactables.Interacted.Subscribe(this, use =>
{
    // use.Target (Unity-null if the use destroyed it, e.g. a pickup), use.Address, use.Source
});

Component c = Interactables.Find(savedAddressOrId);
List<Component> all = Interactables.All();               // a scan
```

`Interacted` is raised after the game's own call returned (a call that throws raises nothing), for the local player's uses only. It is not raised for things the game or a mod interacts with by code. The address is taken before the use, so it is available even when the target is gone afterwards. `Interactables.All` covers the `Interactable` base class and `WorkableResourceSource`, which implements `IInteractable` alone; anything that asks for "all interactables" with `Interactable` alone misses it.

## Radios, televisions and the guitar

```csharp
RadioInfo radio = Radios.Describe(r);       // Enabled (power), Station (0 = off), Stations, Playing
Radios.SetStation(r, 3);
Radios.SetEnabled(r, false);
Televisions.SetOn(tv, true);                // false without power or when the level locked it
```

`Radios.Changed` and `Televisions.Changed` report every change. The guitar must be in the player's hands (`Instruments.Held`):

```csharp
Instruments.Strum(guitar);
Instruments.PlaySong(this, guitar, new[] { 0f, 0.5f, 0.5f, 0.25f, 0.25f, 1f });   // seconds before each strum
Instruments.Hold(guitar, 0);                // one of its long notes, until StopHold
```

Songs and held notes stop when the guitar is put away, a menu or conversation opens, the level changes or the mod unloads; at most four strums fall on one frame. `Instruments.Strummed` and `Instruments.Commented` (an NPC's comment, with `Positive` from the game's music skill roll) report what happened.

## Lights and switches

```csharp
foreach (var light in Lights.All()) ...
Lights.IsOn(light); Lights.IsPowered(light); Lights.IsLit(light);   // lit = on and powered
Lights.SetOn(light, true);          // turnLightOn / turnLightOff
Lights.SetPowered(light, false);    // TogglePower, what the power grid does

Lights.LightChanged.Subscribe(this, c => ...);   // c.Light, c.Kind (TurnedOn, TurnedOff, PowerLost, PowerRestored), c.Source, c.IsLit
```

`Lights` is for `LightTurnOffOn` fixtures. Multi-state fixtures (`LightTurnOffOnMultiState`) and the old `LightsON` switches are not covered. Lights that follow the time of day raise a burst of events at dusk and dawn.

```csharp
Switches.IsPressed(button); Switches.IsLocked(button);
Switches.Set(button, true);     // Press() / Unpress(): fires its outputs, skips the player's key and item checks

Switches.SwitchChanged.Subscribe(this, c => ...);   // c.Button, c.Pressed, c.Source
```

`Switches` wraps the game's `Button` (wall switches, levers, buttons). The event comes from `PressEffects` and `UnpressEffects`, which every path ends in, including timed releases. A switch only holds its own state; what it switches is wired in the scene, so a mirror should send the switch and let each side's scene react, or send the light and door events and apply those, not both.

## Placed furniture

```csharp
List<FurniturePlaceable> placed = PlacedFurniture.All();             // includePreplaced: true adds scene furniture
List<FurnitureInfo> infos = PlacedFurniture.List();
string id = PlacedFurniture.IdOf(placeable);
FurniturePlaceable p = PlacedFurniture.Find(id);
FurnitureInfo info = PlacedFurniture.Describe(p);
```

`FurnitureInfo`: `Id`, `TypeId` (the game's `Furniture.Id`), `Title`, `Manager` (the zone's GUID), `Scene`, `Position`, `Rotation` (root), `ModelRotation` (child 0, the part the rotate key turns), `Skin`, `Alternative`, `Preplaced`, `ParentId`, `AttachedToId` and the live `Placeable`.

### Ids

The game gives placed furniture a GUID only at save time (`manager GUID + n`, counted over the list in reverse), so a freshly placed piece has none and removing one piece renames others. The ids here are our own: 12 random hex characters handed out the first time a piece is asked about. They are kept with the save: before each game save the framework records, per scene, each piece's id with its kind, zone and position (`mods/ObenFramework.furniture.json` in the save folder, through `ModSaves`), and after a load a piece gets its old id back when the same kind of furniture stands in the same zone within 2 cm of where it was. Furniture that came with the scene uses `pre:` plus its prefab GUID, which is stable already. A moved piece keeps its id. Ids also hold when the player changes level (the level change's own save records them before the next level loads) and over a hot reload of the framework (the ids are kept by the pieces' objects, which outlive the reload), so a piece placed or moved since the last load keeps its id too. `PlacedFurniture.Adopt(placeable, id)` sets an id on purpose, for a guest that must use the host's ids.

`PlacedFurniture.IsAvailable` (and `FurnitureBuilder.IsAvailable`, the same check) is false when the framework's furniture hooks failed to apply after a game update; the events then stay quiet and ids may not be kept over a save.

### Events

```csharp
PlacedFurniture.FurniturePlaced.Subscribe(this, c => ...);   // c.Furniture (FurnitureInfo), c.Source
PlacedFurniture.FurnitureRemoved.Subscribe(this, c => ...);
PlacedFurniture.FurnitureMoved.Subscribe(this, m => ...);    // m.Before, m.After (same Id), m.Source
```

The hooks are the ones the game notes recommend. `FurniturePlaceable.SetFurnitureInfo` is the one call every placement makes: inside the build menu's `Place()` the new pieces are collected and reported when it returns (the piece's rotation and its parent and attachment links are set by then; the game's own `FurnitureManager.FurniturePlaced` is not raised for normal placements, so it is not used). Outside it (a tenant moving in, scripted furniture, apartment rebuilds) the event is raised at once with source `Game`. `FurniturePlaceable.Take` is every removal; it recurses into children and attached pieces, which each get their own `FurnitureRemoved`, children first.

The build menu's move is a take followed by a place of the same piece, possibly seconds apart. The take is held back; if the next place is the same kind of furniture it becomes one `FurnitureMoved` (and the id is kept), otherwise, or when the player closes the menu or leaves the zone, the held take is reported as a `FurnitureRemoved`. Children and attached pieces of a moved piece go to the inventory (that is what the game does) and are reported as removals.

Not covered: loose items on surfaces (not furniture; see `FloorItems`), wallpaper, and the player's furniture inventory.

### Placing, moving and removing from code

`FurnitureBuilder` makes the changes without the build menu, so another player's change can be applied on this machine. (The class is not called `Furniture` because that is the game's own type for a kind of furniture.)

```csharp
var edit = FurnitureBuilder.Place("Red Metal Chair", new Vector3(103.1f, -99f, 12.4f), Quaternion.Euler(0, 90, 0), zone: zoneGuid);
if (!edit.Success) Log.Warn(edit.Error);
string id = edit.Furniture.Id;

FurnitureBuilder.Move(id, newPosition, Quaternion.Euler(0, 180, 0));
FurnitureBuilder.Remove(id);
```

| Call | Does |
|---|---|
| `Place(kind, position, rotation, zone = null, options = default, source = Mod)` | Instantiates the prefab under the zone and registers it the way the game's `Place()` does (`SetFurnitureInfo`, skin and alternative, parent and attachment links). `zone` is a manager GUID (`Zones()`), null means the zone the local player is building in. `rotation` is the world rotation of the root. |
| `Move(id, position, rotation = null, modelRotation = null, source = Mod)` | Sets the transform, keeps the id, refreshes window light and the navmesh cut. Refused for a piece that stands on or hangs on another, or carries others (they would stay behind). |
| `Remove(id, source = Mod, withPieces = false, force = false)` | Does what `FurniturePlaceable.Take` does (stand up a sitter, unlink, the piece's taken relay, janitor actions, destroy) but never calls `BuildingSystem.Take`, so nothing goes into the local player's furniture inventory. Refused for a piece that carries others unless `withPieces` (those are removed first, each with its own event), for one that holds items, liquid or crafting progress unless `force` (the contents are lost), and for pieces the game marks `cantTake`. |
| `Zones(scene = null)`, `FindZone(guid)` | The building zones: GUID, scene, position, how many placed pieces, which one is active. |

`kind` is what `FurnitureInfo.TypeId` reports, the asset path (`Assets/Content/Prefabs/Building System/Furniture/Chairs/Red Metal Chair.asset`); `FurnitureKinds.Find` also takes the short name (`Red Metal Chair`) and, last, the title, ignoring case. `FurnitureKinds.All` lists the 655 pieces the build menu knows plus the pieces mods defined with `CustomFurniture` (asking builds the ones not built yet), and `FurnitureKinds.Search(text)` filters them. An asset path the list lacks is loaded from the game's assets. `FurnitureKinds.IsKnown(kind)` tells the two apart: true only for pieces in the list. For a machine that places what another one asks for, `FurniturePlacement.KnownOnly` makes `Place` refuse anything else. Furniture added by other mods' own systems is not covered.

Every call returns a `FurnitureEdit`: `Success`, `Error` (a plain sentence) and `Furniture` (the `FurnitureInfo` after a place or move, as it stood for a remove). `FurniturePlacement` carries the optional parts: `Skin`, `Alternative`, `ModelRotation`, `ParentId`, `AttachedToId`, `KnownOnly` and `Id`, an id to give the piece so a guest can use the host's ids (the id is set before the piece is registered, so the placed event already carries it).

The events come out as with the build menu: `FurniturePlaced`, `FurnitureMoved` and `FurnitureRemoved` with `Source` set to what you passed (default `Mod`). Place runs inside a `WorldChanges.Enter(source)` scope; move and remove raise their events directly with the given source. Nothing checks that the spot is free: that is the build menu's preview and not a game rule. Every call returns an error while a save is loading or when no world is loaded.

Loose items that lie on surfaces inside a build zone get a furniture component from the game (their "furniture" is a made-up asset with the item's title). `FurnitureBuilder` refuses them ("a loose item, not furniture"); use `FloorItems` for those.

## Containers

`Containers` covers the game's `Storage`: drawers, chests, crates, fridges, a machine's item slots, the hidden storage behind the backpacks.

```csharp
foreach (ContainerInfo c in Containers.List()) ...           // Id, Title, Scene, Position, SlotCount, IsEmpty, GameLocked, NpcOwner, EditOwner
Storage storage = Containers.Find(id);                       // null when it is not in a loaded scene
List<ContainerItem> items = Containers.GetItems(storage);    // Slot, ItemId, Title, Amount, Owner
int count = Containers.Count(storage, itemId);

int notFitted = Containers.Add(storage, item, 5);            // how many did NOT fit, 0 = all in
int taken = Containers.Remove(storage, itemId, 3);           // how many were taken, nothing goes to the player
```

**Ids.** `Containers.IdOf(storage)` is the game's GUID (the editor sets one for every scene container; the 126 in the player's tenement had no duplicates), or the `ObjectAddress` text if it has none. Containers inside furniture the player placed are `furniture:{furnitureId}:{n}`, because the GUID in the prefab is the same for every copy of that piece; the n-th container in the piece is stable. `Containers.Find` understands both, a bare GUID and any address text.

**Add and Remove** use the game's own methods (`Storage.AddItemAndReturnRemaining`, `Storage.RemoveItem`), so stacking, the container's category filters and ownership work as in the game and nothing is dropped on the floor. Items 0 and 1 are not money inside a storage, they are ordinary stacks. Removing ignores who the stack belongs to and does not commit a theft. The open window is refreshed (`UpdateSlots`).

**Events** (all `WorldEvent<ContainerEvent>`, with `Container` and `Source`):

- `Containers.Opened` and `Containers.Closed`: patches on `Storage.EnterExternally` and `ExitExternally`, and on `OnDisable` for a container that is switched off or destroyed while open. The local player's interact key runs inside the player scope, so `Source` is `LocalPlayer`.
- `Containers.Changed`: raised after the public mutators (`AddItem`, `AddItemAndReturnRemaining`, `AddItemToStorage`, `RemoveItem`, `TakeAll`, `MoveToOtherStorage`, `Empty`) when the slots differ afterwards (a fingerprint of the slots is compared, so a refused add raises nothing), and for drags, drops and quick moves in the open inventory window (reported as `LocalPlayer`). Nested calls report once. `Add` and `Remove` above report as the source you pass (default `Mod`). Loot that a container spawns for itself is not reported.

**Locks.** `ContainerLocks` is a small advisory lock so one user edits a container at a time:

```csharp
if (!ContainerLocks.TryLock(id, "player-2")) return;        // someone else has it
try { Containers.Add(storage, item, 1, asOwner: "player-2"); }
finally { ContainerLocks.Unlock(id, "player-2"); }
```

There are also `OwnerOf`, `IsHeldByOther`, `ReleaseAll(owner)`, `All()` and the `LockChanged` event for when one is taken or released. `Add` and `Remove` take `asOwner` (null means `ContainerLocks.LocalOwner`, default `"local"`) and do nothing when another owner holds the container. The local player cannot open a container that another owner holds (they get the game's "locked" feedback). With `ContainerLocks.LockWhileLocalPlayerHasOpen = true` the local player takes the lock for the container they open and gives it back on closing, so a co-op mod can broadcast `LockChanged` and keep others out. Locks are not saved and are all dropped when the scene changes. They are separate from the game's own key lock (`ContainerInfo.GameLocked`).

## Floor items

`FloorItems` is for pickups lying in the world: `CollectibleItem` and coins (not blueprints or notes).

```csharp
foreach (FloorItemInfo i in FloorItems.All()) ...            // Id, ItemId, Title, Amount, Position, Scene, GameObject
Collectible pickup = FloorItems.Find(id);
FloorItemInfo? spawned = FloorItems.Spawn(item, 3, position, id: hostId);   // a normal pickup the game saves with the level
bool gone = FloorItems.Remove(id);                           // destroyed and remembered as gone, nobody gets the item
```

`All()` walks the game's own `Collectible.allCollectibles`, which holds pickups that have started (active objects of loaded scenes); the game re-creates that set on a load, so do not cache it.

**Ids.** A pickup that came with the scene is identified by its game GUID. A spawned one has none, so it gets a random 8-character id the first time anyone asks (`FloorItems.IdOf`, which also takes the GameObject `Items.WorldItems.Spawn` returns) or, while somebody is subscribed, when it appears. They last for the session: the game saves spawned pickups with the level but not our ids, so after a load they are new. `FloorItems.Adopt(pickup, id)` and `Spawn(..., id)` set an id on purpose, for a guest that must use the host's.

**Events** (`WorldEvent<FloorItemEvent>`, with `Item` and `Source`):

- `Spawned`: any pickup that appears through `ItemOperations.SpawnItemToPosition`, which every drop, `FloorItems.Spawn` and `Items.WorldItems.Spawn` end in. Source is `Mod` for spawns made inside a mod scope, `Game` otherwise.
- `Dropped`: right after `Spawned` for a pickup that came out of `ItemOperations.DropItem(ItemStack, ...)`: the inventory window's drop, a full inventory spilling over, a storage overflowing, an NPC. `LocalPlayer` when the dropper was the player.
- `PickedUp`: `CollectibleItem.PerformPickup` and `CollectibleCoin.Interact`. `Item.Amount` is how much was taken (a partial pickup leaves the rest on the floor).

`Remove` and `Spawn` do not go through the pickup path, so applying a remote player's pickup with `Remove(id)` raises no `PickedUp` and nothing is echoed. Loose items that sit in a build zone are pickups too (the game gives them a furniture component), so they appear in both lists.

**Expiry.** Dropped items disappear after a random 0.5 to 1.5 times the game's expiry time (`FloorItemExpiry.GameMinutes`, 600 game minutes by default). `FloorItemExpiry.Set(this, minutes)` changes it for items whose timer starts from then on, until your mod unloads; the newest setter wins. Items the game spawns with their own time (never expiring, expiration zones) keep it.

## Power grids

```csharp
PowerGrid grid = PowerGrids.TenementMain;                 // or PowerGrids.All(), PowerGrids.Find(id)
PowerGridInfo info = PowerGrids.Describe(grid);           // Enabled, BaseCapacity, LocalBalance, NetBalance, TotalCapacity,
                                                          // FreeCapacity, Flickering, Unlimited, Unstable, ParentId, Name, Id
PowerGrids.GridChanged.Subscribe(this, c => ...);         // c.Grid, c.Kind (Toggled, CapacityChanged), c.Source, c.Info
```

All numbers are watts as in the game's power meters. Which listeners lose power when a grid is overloaded depends on the order they were switched on, so two machines only agree if they see the same sequence of events. `GridChanged` covers `PowerGrid.Toggle` and `SetBaseBalance`; changes caused by devices being switched are not events, read `Describe` when you need the numbers.

`PowerGrids.SetEnabled(grid, on)` switches a grid's breaker and `PowerGrids.SetBaseCapacity(grid, watts)` sets its own capacity (the tenement sets its main grid's capacity from the electricity upgrade again whenever its utilities update). The appliances, generators, consumers and batteries on a grid are in [environment.md](environment.md) (`PowerDevices`).

## Dev bridge

| Command | Does |
|---|---|
| `doors.list` | doors with id, phase, locked, position, distance; `radius`, `max`, `inactive` |
| `doors.set` | `id` plus `open` and/or `locked`; `swing` (`default`, `opposite`), `respectLock` |
| `locks.list` | key locks nearest first with kind, locked, lockpickable, difficulty, key; `radius`, `lockedOnly`, `max` |
| `locks.set` | `id` plus `locked`, `lockpickable`, `difficulty` |
| `locks.givekey` | gives the player the key of lock `id` |
| `locks.events` | `Locked`, `Unlocked`, `Lockpicked` and keypad events since recording started |
| `keypads.list`, `keypads.set` | keypads with codes; `id` plus `open` and/or `code` |
| `doors.schedule` | opening hours of every schedule, or of `id` (a schedule or a scheduled door); `opens`+`closes` sets every day, `reset` restores |
| `radios.list`, `radios.set` | radios; `id` plus `station`, `enabled` |
| `tvs.list`, `tvs.set` | televisions; `id` plus `on` |
| `guitar.play` | the guitar in hand: `strum`, `song` (waits), `hold`, `stop` |
| `media.events` | radio, television, strum and comment events since recording started |
| `computers.list` | computers with e-mail and file counts; `id` for one with its documents |
| `interact.target` | the crosshair target: id, address, title, type, and state for doors and switches |
| `furniture.list` | placed furniture with ids; filters `type`, `manager`, `preplaced`, `max` |
| `furniture.place` | `kind`, `x`/`y`/`z` or `pos`, `yaw`, `zone`, `skin`, `alternative`, `modelYaw`, `id`, `parent`, `attachedTo`, `known` (refuse pieces the build menu lacks), `source` |
| `furniture.move` | `id`, `x`/`y`/`z` or `pos`, `yaw`, `modelYaw`, `source` |
| `furniture.remove` | `id`, `withPieces`, `force`, `source` (nothing goes into the furniture inventory) |
| `furniture.kinds` | the kinds the game knows, mod pieces marked `custom`; `text`, `max`, `known=<kind>` (is it one the build menu offers) |
| `furniture.zones` | building zones with GUID, placed count and which is active |
| `containers.list` | containers nearest first; `radius`, `text`, `empty`, `inactive`, `max`; also the edit locks held |
| `containers.get` | one container by `id` with its stacks |
| `floor.list` | items lying in the world nearest first; `radius` (default 10), `item`, `text`, `max` |

```sh
oben call doors.list radius=5
oben call doors.set id=<id> open=true
oben call interact.target
oben call locks.list radius=6 lockedOnly=true
oben call doors.schedule
oben call furniture.list type=shelf
oben call furniture.kinds text=chair
oben call furniture.place kind="Red Metal Chair" pos=[103.1,-99,12.4] yaw=90
oben call containers.list radius=4
oben call containers.get id=<id>
oben call floor.list radius=5
```
