# World: doors, interactables, furniture and power

`ObenFramework.World` wraps the parts of the game's world that mods and the co-op mod need most: doors, the things the player can use, lights and switches, placed furniture and the power grid. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.World;`.

Background on how the game does these things, and why the hooks are where they are, is in [../../game/world-building.md](../../game/world-building.md). The co-op notes in [../../research/coop-reference.md](../../research/coop-reference.md) (section 5) list the same game methods; the events below are the ones a mod that mirrors the world needs.

What is covered: doors, interactables, light fixtures, wall switches, placed furniture, power grids (read only). What is not: elevators, trains, keypads, crafting machines, storage. Those are in the game notes and can get the same treatment later.

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

Things spawned at runtime (placed furniture, dropped items) have neither a stable GUID nor a stable path. Placed furniture has its own ids (below).

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

The game gives placed furniture a GUID only at save time (`manager GUID + n`, counted over the list in reverse), so a freshly placed piece has none and removing one piece renames others. The ids here are our own: 12 random hex characters handed out the first time a piece is asked about. They are kept with the save: before each game save the framework records, per scene, each piece's id with its kind, zone and position (`mods/ObenFramework.furniture.json` in the save folder, through `ModSaves`), and after a load a piece gets its old id back when the same kind of furniture stands in the same zone within 2 cm of where it was. Furniture that came with the scene uses `pre:` plus its prefab GUID, which is stable already. A moved piece keeps its id. `PlacedFurniture.Adopt(placeable, id)` sets an id on purpose, for a guest that must use the host's ids.

### Events

```csharp
PlacedFurniture.FurniturePlaced.Subscribe(this, c => ...);   // c.Furniture (FurnitureInfo), c.Source
PlacedFurniture.FurnitureRemoved.Subscribe(this, c => ...);
PlacedFurniture.FurnitureMoved.Subscribe(this, m => ...);    // m.Before, m.After (same Id), m.Source
```

The hooks are the ones the game notes recommend. `FurniturePlaceable.SetFurnitureInfo` is the one call every placement makes: inside the build menu's `Place()` the new pieces are collected and reported when it returns (the piece's rotation and its parent and attachment links are set by then; the game's own `FurnitureManager.FurniturePlaced` is not raised for normal placements, so it is not used). Outside it (a tenant moving in, scripted furniture, apartment rebuilds) the event is raised at once with source `Game`. `FurniturePlaceable.Take` is every removal; it recurses into children and attached pieces, which each get their own `FurnitureRemoved`, children first.

The build menu's move is a take followed by a place of the same piece, possibly seconds apart. The take is held back; if the next place is the same kind of furniture it becomes one `FurnitureMoved` (and the id is kept), otherwise, or when the player closes the menu or leaves the zone, the held take is reported as a `FurnitureRemoved`. Children and attached pieces of a moved piece go to the inventory (that is what the game does) and are reported as removals.

Not covered: loose items on surfaces (not furniture, no stable id), wallpaper, and the player's furniture inventory.

Applying a remote placement or removal is not wrapped yet: the game's `BuildingSystem.Take` puts a removed piece into the local inventory, which is wrong for a remote removal. The notes in [../../game/world-building.md](../../game/world-building.md) section 4.5 describe what an apply has to do.

## Power grids (read only)

```csharp
PowerGrid grid = PowerGrids.TenementMain;                 // or PowerGrids.All(), PowerGrids.Find(id)
PowerGridInfo info = PowerGrids.Describe(grid);           // Enabled, BaseCapacity, LocalBalance, NetBalance, TotalCapacity,
                                                          // FreeCapacity, Flickering, Unlimited, Unstable, ParentId, Name, Id
PowerGrids.GridChanged.Subscribe(this, c => ...);         // c.Grid, c.Kind (Toggled, CapacityChanged), c.Source, c.Info
```

All numbers are watts as in the game's power meters. Which listeners lose power when a grid is overloaded depends on the order they were switched on, so two machines only agree if they see the same sequence of events. `GridChanged` covers `PowerGrid.Toggle` and `SetBaseBalance`; changes caused by devices being switched are not events, read `Describe` when you need the numbers. There are no grid setters yet.

## Dev bridge

| Command | Does |
|---|---|
| `doors.list` | doors with id, phase, locked, position, distance; `radius`, `max`, `inactive` |
| `doors.set` | `id` plus `open` and/or `locked`; `swing` (`default`, `opposite`), `respectLock` |
| `interact.target` | the crosshair target: id, address, title, type, and state for doors and switches |
| `furniture.list` | placed furniture with ids; filters `type`, `manager`, `preplaced`, `max` |

```sh
oben call doors.list radius=5
oben call doors.set id=<id> open=true
oben call interact.target
oben call furniture.list type=shelf
```
