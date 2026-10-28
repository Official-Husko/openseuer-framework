# Transport: levels, travel, elevators, ladders, trains and drones

`ObenFramework.Transport` covers how the player and things move around: the exits between levels, sending the player to a scene, entrypoints, elevators, ladders, tarp doors, trains and the minitrain, ticket machines, drones and wait stations. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Transport;`.

The events follow the rules in [world.md](world.md#reading-the-rules-once): subscribe with an owner, nothing runs per frame, nothing is raised while a save loads, and each event carries the `ChangeSource` where it can tell. Ids are the object's GUID where it has one, else its address (see [world.md](world.md#addresses-pointing-at-a-scene-object)); pieces of furniture get `furniture:<id>:<n>`.

Background on the game's side is in [../../game/world-building.md](../../game/world-building.md) (section 7).

## Levels

Every level has exits: doors to other scenes (`DoorChangelevel`, with a lock) and triggers or signs (`Changelevel`). `Levels` lists and uses them and sends the player anywhere:

```csharp
foreach (LevelExit exit in Levels.List()) { ... }   // Id, Title, Scene, Entrypoint, Name, IsDoor, Locked, Position, Spawn, Stand
Changelevel exit = Levels.Find(id);
Levels.SetLocked(exit, true);                        // doors only
Levels.Use(exit);                                    // as if the player used it: a door animates, then the level changes
Levels.ChangeTo("Open Sewer Bazaar", "SomeEntrypoint");  // straight there; null entrypoint = the level's spawn
bool can = Levels.CanTravel(out string reason);
IReadOnlyList<string> scenes = Levels.Scenes;        // all 124 scenes in build order
```

A level change is a full save and reload, like the game's own. `Use` and `ChangeTo` return false when the trip can't start: while a save loads or saves, during another level change, while the player waits or sleeps, outside a level, or when the scene doesn't exist.

`Levels.Leaving` (`LevelTrip`: `From`, `To`, `Entrypoint`, `ExitId`, `Source`) is raised when any trip starts (an exit, fast travel, the console, a mod) and `Levels.Arrived` when that trip's level is ready. `ExitId` is the exit used, or null. A door plays its animation before the level changes, so the player's use is remembered for a short while and the trip still reports `LocalPlayer`.

## Entrypoints and fast travel

Entrypoints are the named spots players arrive at. A mod can add its own to any scene:

```csharp
Travel.AddEntrypoint(this, "Open Sewer Bazaar", "MyMod.Dock", new Vector3(10f, -99f, 4f), yaw: 90f);
Levels.ChangeTo("Open Sewer Bazaar", "MyMod.Dock");
Travel.TeleportTo("MyMod.Dock");          // within the current level
List<string> names = Travel.Entrypoints(); // the current level's, game and mod ones
Travel.OpenFastTravelMenu();
bool driving = Travel.UsingMinitrain;
```

The spot is put in place whenever the scene loads, before the game moves an arriving player, and taken away when your mod unloads. Mod entrypoints are never used as the level's default spawn. A name the scene already has is refused (logged once).

## Elevators

```csharp
foreach (Elevator elevator in Elevators.All()) { ... }
ElevatorInfo info = Elevators.Describe(elevator);   // Floors, Position (floor index, fractional while moving), Floor, Moving (-1/0/1), Powered, Queue, FloorsEnabled
Elevators.Call(elevator, 2);                        // like a call button; false without power or for a disabled floor
Elevators.SetPowered(elevator, false);
Elevators.SetFloorEnabled(elevator, 1, false);
```

`Elevators.Called` (a new floor joined the queue) and `Elevators.Arrived` (the car stopped at a called floor) come from the game's `Call` and its floor-reached step, not from a per-frame hook. The car and floor doors are ordinary doors.

## Ladders and tarp doors

```csharp
Ladders.SetBlocked(ladder, true);      // false if the player is on it; the game saves the flag
bool on = Ladders.PlayerClimbing;
Ladders.GetOff();                      // takes the player off at the nearest exit
TarpDoors.SetOpen(tarp, true);         // the cloth curtains in doorways; the game saves their state
```

`Ladders.Climbed` reports getting on and off (`Climbing` true or false), `TarpDoors.Changed` each open or close.

## Dev bridge

| Command | Does |
|---|---|
| `levels.list` | exits of the current level nearest first, with scene, entrypoint, lock and stand spot; `scene`, `max`; also whether travel is possible now |
| `levels.use` | uses exit `id` (`ignoreLock`) |
| `levels.lock` | locks or unlocks level door `id` |
| `travel.go` | sends the player to `scene` (`entrypoint`) |
| `travel.scenes` | all scenes in build order |
| `travel.entrypoints` | entrypoints of the current level |
| `travel.teleport` | moves the player to entrypoint `name` |
| `travel.events` | `Leaving`, `Arrived`, elevator, ladder and tarp door events since recording started |
| `elevators.list`, `elevators.call` | elevators with floors and queue; call `id` to `floor` |
| `ladders.list`, `ladders.block` | ladders; block or free `id` |
| `tarps.list`, `tarps.set` | tarp doors; open or close `id` |

```sh
oben call levels.list max=5
oben call travel.entrypoints
oben call ladders.list
oben call travel.events
```
