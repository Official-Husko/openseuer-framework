# NPCs

`ObenFramework.Npcs` reads the game's NPCs, tells you when one appears in or leaves the scene, lets one mod at a time take an NPC over for a while, and reads their schedules. Background on how the game runs NPCs (the schedule database, `NPCController`, activities, the director that simulates NPCs in other scenes) is in [../../game/npcs-dialogue-quests.md](../../game/npcs-dialogue-quests.md) section 2. Phone numbers are a separate part of the same module, see [phone.md](phone.md).

Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Npcs;`.

## Who counts as an NPC

The game knows an NPC in two ways, and the framework joins them:

- The **schedule database** (`NPCDirector.globalDatabase`, read from `StreamingAssets/NPCBehavior.json`) has one record per NPC, wherever it is. About 240 of them. Most have no timetable. The game simulates the ones that are not in your scene once per game minute, so for them only a scene name and a last stored position exist, not a real place.
- The **scene objects** (`NPCInfo` components listed in `NPCManager`) exist for the NPCs in the scene you are in. Some have no database record at all, and many tenement residents have no `NPCController`: they cannot be steered, only read.

`Npcs.All()` returns the database followed by scene NPCs the database lacks. An `NpcRecord` is a copy taken when you ask:

| Member | Meaning |
|---|---|
| `Id`, `Name`, `FullName`, `Job` | the character's identity; `Id` is the number Lua, mail and the console use |
| `Scene` | the scene the NPC is in; for off-scene NPCs where the simulation last put them, empty before it has |
| `Position` | real position when loaded, else the last stored one (only meaningful with `Scene`) |
| `Loaded` / `Active` | an object exists in this scene / and it is switched on |
| `State`, `Dead` | `Any`, `Default`, `Sleeping`, `Dead` (empty when not loaded) |
| `Activity`, `ScheduleEntry` | what the NPC is doing now (`Act_Stand`, `Act_Sleep`...) and the schedule entry that started it |
| `HasSchedule` | the database gives it a timetable or random activities |
| `Data`, `Live` | the game's own `NPCData` and `NPCInfo`, for when you need more (check `Live` for Unity null) |

```csharp
foreach (var npc in Npcs.InScene())
    Log.Info($"{npc.FullName} at {npc.Position}: {npc.Activity}");

var pentti = Npcs.Find("Pentti");            // first or full name, ignoring case
var same = Npcs.Get(11);                     // by id
var closest = Npcs.Nearest(maxDistance: 8);  // from the player; or Nearest(somePoint)
```

`Nearest` only looks at living, active NPCs in the scene. Reads build copies; call them when something happened, not every frame.

## Spawned and despawned

```csharp
Npcs.Spawned.Subscribe(this, npc => Log.Info($"{npc.FullName} is here"));
Npcs.Despawned.Subscribe(this, npc => Log.Info($"{npc.FullName} left"));
```

Both come from the game's scene NPC list, the one place every spawn and removal goes through. They fire when a level loads (once per NPC), when an NPC arrives from another scene through a door, and when one is removed or the level is torn down. Despawned is raised just before the object goes, so `npc.Live` is still usable inside the handler but not after it. Subscribe with an owner and a hot reload removes the handler; every handler runs in its own try/catch. The hook returns at once while nobody is subscribed.

## Taking control

```csharp
var claim = NpcControl.Claim(this, 11);          // null if not loaded, not steerable, or another mod has it
if (claim == null) return;

claim.LookAt(PlayerRef.Body);                    // eyes, head and body turn to the player
claim.MoveTo(counterPoint, arrived =>
{
    Log.Info(arrived ? "there" : "did not make it");
    claim.Dispose();                              // give the NPC back
});
```

A claim is one mod's hold on one NPC. It changes nothing by itself. Only one mod can hold an NPC at a time: a second `Claim` returns null until the first lets go, and `NpcControl.ClaimedBy(id)` tells you who holds it, so co-op code can check before it moves an NPC to match another player. Claiming an NPC your mod already holds returns the existing claim.

What a claim can do:

| Call | Effect |
|---|---|
| `Pause()` / `Resume()` | stops the NPC's activity and keeps its schedule from starting another / lets the schedule run again within a game minute. Pause is refused (false) while the activity cannot be interrupted, such as a police chase |
| `MoveTo(point, done, stopDistance, timeoutSeconds)` | pauses, then walks with the game's path finding. `done(true)` on arrival, `done(false)` when cancelled, timed out or the point is out of reach. A new walk cancels the last |
| `CancelMove()` | stops where the NPC stands |
| `LookAt(transform, turnBody)` / `LookAt(point)` / `StopLooking()` | eyes and head follow a transform; with `turnBody` the body turns too (which pauses the activity) |
| `Dispose()` | undoes everything: stops looking, cancels a walk, resumes the schedule |

The claim is released by `Dispose`, when the owning mod unloads, when the NPC leaves the scene, and when the level changes. `claim.IsValid` is false after that. If someone else (not through the framework) already paused the NPC, the claim notes it and does not resume it for them.

Things to know:

- Only NPCs with an `NPCController` can be claimed. Tenement residents and some story NPCs are plain scene objects without one; `Claim` returns null for them.
- Walking uses the NPC's pathfinding agent directly. The activity is paused meanwhile and the NPC walks the navigation mesh only, with none of the game's waypoint or door logic. Keep to short walks inside one scene. Claims have been written from the game's code and not yet run against an AI-driven NPC, so treat their edges as untested.
- A talk starts on its own terms: if the player talks to a claimed NPC the game stops it for the conversation, and a walk then ends on its timeout.
- The framework ticks claims once per frame in `OnUpdate`. With no claims it does nothing.

## Schedules

```csharp
foreach (var e in NpcSchedules.Timetable(npcId))
    Log.Info($"{e.Name}: day {e.WeekDay} at {e.StartHour:0.0}h for {e.DurationMinutes} min, {e.Activity} in {e.Place}");

var random = NpcSchedules.RandomActivities(npcId);
NpcSchedules.SetDisabled(this, npcId, index: 0, disabled: true);   // put back when your mod unloads
```

The game builds schedules from the JSON on every start and never saves them, so the data is the same for every save. Entries are copies (`NpcScheduleEntry`: `Name`, `Index`, `WeekDay`, `StartHour`, `DurationMinutes`, `Chance`, `InterruptsPrevious`, `Activity`, `Place`, `Disabled`). The week has 7 days numbered 0 to 6; an entry that runs past the end of the week wraps. Most timetable activities in the shipped data are placeholders ("Test", an empty `Activity`).

Switching an entry off is the one change offered. The game skips disabled entries both for NPCs in the scene and for the off-scene simulation. Adding entries is not offered: an entry needs the game's own activity objects (nodes and groups that exist only in scene data), and a custom `Activity` subclass would be written into saves by type name and break them when the mod is missing.

## Dev bridge

```sh
oben call npcs.list loaded=true
oben call npcs.list scene="Interior Player Tenement" name=Malone
oben call npcs.get name=Mirjam
oben call npcs.nearest maxDistance=10
```

`npcs.list` prints id, name, scene, position, activity and whether the NPC is loaded; `npcs.get` adds the job, who claims it and its schedule; `npcs.nearest` gives the closest living NPC with its distance. All are read only. See [../dev-tools.md](../dev-tools.md).
