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

## Trains

The level's train controller knows every rail vehicle (locomotives, wagons, the minitrain) by GUID. Going by the scene files, most train objects of 0.5.13 are in `Open Sewer Mines`; `Open Sewer Bazaar` has one track switch and no vehicles.

```csharp
foreach (TrackTrain train in Trains.All()) { ... }
TrackTrain train = Trains.Find(guid);
TrainInfo info = Trains.Describe(train);    // Speed, TargetSpeed, EngineOn, Braking, FrontCoupledTo, BackCoupledTo, WagonType, CargoId, IsMinitrain
MinitrainInfo? cab = Trains.Minitrain;      // Fuel, Capacity, Running, PlayerDriving, Speed
List<TrackSegmentInfo> track = Trains.Segments();
Trains.SetBranch(trackSwitch, 1);           // refuses while a train is on the switch's segment, unless force: true
Trains.SetLoader(loader, on: true, freeToTake: false);
```

`TrainActions` drives them: `SetEngine` (a minitrain is started and stopped through its cab, so lights, sounds and fuel behave), `SetTargetSpeed`, `Brake`, `Couple(train, TrainEnd.Back, other, TrainEnd.Front)` (only when the ends touch, both couplers are free and ready and neither vehicle is too fast) and `Release`. While the player drives a minitrain their levers set the speed again every physics step.

`Trains.Moved` reports `Departed`, `Stopped` and `Crashed` (with the other vehicle). Crashes come from the game's own crash step; departures and stops are checked four times a second, and only while someone is subscribed, so an unused event costs nothing.

## Ticket machines

```csharp
foreach (var machine in TrainTickets.All()) { TicketMachineInfo info = TrainTickets.Describe(machine); }   // Price, BasePrice, Currency, TicketItemId
TrainTickets.SetPrice(this, 10);                                         // every machine
TrainTickets.AddRule(this, new TicketPriceRule { Machine = id, Factor = 0.5f });
```

Rules from several mods combine: a fixed price for one machine beats one for every machine, the newest wins among equals, then every factor multiplies; the result is rounded and never below zero (`TicketPriceRules`, unit tested). The machines' own prices come back when the rules go. The game doesn't save ticket prices, so nothing sticks in a save. `TrainTickets.Changed` reports tickets bought and taken out of the tray. Going by the scene files, the ticket machines are in `Open Sewer Metro` (none in the Bazaar).

## Drones

```csharp
foreach (Drone drone in Drones.All()) { DroneInfo info = Drones.Describe(drone); }   // Active, MaxSpeed, TargetNode, NextNode
Drones.SetActive(drone, true);
Drones.SetTarget(drone, Drones.FindNode(nodeId), start: true);
Drones.SetMaxSpeed(drone, 4f);
```

`Drones.Changed` reports switching on or off and each air node a drone passes (`Node`, `RouteEnd`).

## Wait stations

Wait stations are the benches, desks, TVs, games and jobs where the player passes time (`InteractableWait`):

```csharp
WaitStationInfo info = WaitStations.Describe(station);   // Kind (waiting, working, having fun, ...), Minutes, IgnoresNeeds, CanWait, Satisfies*
bool ok = WaitStations.CanUse(station, out string reason);
WaitStations.Use(station);                               // as if the player used it; time passes
WaitStations.SetMinutes(station, 30);                    // not saved
```

`WaitStations.Finished` reports the end of each wait with the game seconds waited. Sleep and waits in general are in `PlayerSleep` ([player-actions.md](player-actions.md)).

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
| `trains.list` | rail vehicles, the minitrain; `segments=true` adds segments and switches |
| `trains.drive` | `id` plus `engine`, `speed`, `brake` |
| `trains.switch` | throws switch `id` to `branch` (`force`) |
| `trains.events` | train, ticket, drone and wait station events since recording started |
| `tickets.list` | ticket machines with prices; `price`/`factor` (for `id` or all) adds a bridge rule, `reset` drops them |
| `drones.list`, `drones.set` | drones (`nodes=true` adds air nodes); `id` plus `active`, `target`, `speed` |
| `waits.list`, `waits.use` | wait stations and whether they can be used now; use `id` |

```sh
oben call levels.list max=5
oben call travel.entrypoints
oben call ladders.list
oben call travel.events
oben call trains.list segments=true
oben call tickets.list
oben call waits.list
```
