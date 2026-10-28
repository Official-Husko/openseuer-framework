# NPCs

`ObenFramework.Npcs` reads the game's NPCs, tells you when one appears, leaves, dies or changes what it does, lets one mod at a time take an NPC over for a while (move it, freeze it, animate it, dress it), does one-off things to NPCs, reads their schedules, and covers relationships, the police, robbers, janitor actions and surveillance. Background on how the game runs NPCs (the schedule database, `NPCController`, activities, the director that simulates NPCs in other scenes) is in [../../game/npcs-dialogue-quests.md](../../game/npcs-dialogue-quests.md) section 2.

Related guides: NPCs of your own mod (spawning, looks, orders, weekly routines) are in [mod-npcs.md](mod-npcs.md), animals in [animals.md](animals.md), phone numbers and the phones in the scene in [phone.md](phone.md).

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

## Died and activity changes

```csharp
Npcs.Died.Subscribe(this, npc => Log.Info($"{npc.FullName} died in {npc.Scene}"));
Npcs.ActivityChanged.Subscribe(this, change => Log.Info($"{change.NpcId}: {change.From} -> {change.To}"));
```

`Died` comes from `NPCInfo.ChangeState` and only counts the step from alive to dead, so an NPC that was already dead in the save does not report again when the level loads. Any claim on the NPC ends with it. `ActivityChanged` comes from the setter of the NPC's current activity, in your scene and in the off-scene simulation alike, so it fires often; `From` and `To` are activity names (null for none). Both hooks do nothing while nobody listens.

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
| `Hold(NpcHolds)` | switches parts of the NPC's own behaviour off, see below |
| `PlayAnimation(clip, loop, fade)` / `StopAnimation(fade)` | plays an `AnimationClip` over the NPC's own animation, see below |
| `ApplyLook`, `SetBlendShape`, `SetMeshVisible`, `SetMaterials`, `RestoreAppearance` | changes how the NPC looks until the claim ends, see below |
| `Dispose()` | undoes everything: stops the animation, puts the appearance back, lifts the holds, stops looking, cancels a walk, resumes the schedule |

The claim is released by `Dispose`, when the owning mod unloads, when the NPC leaves the scene or dies, and when the level changes. `claim.IsValid` is false after that. If someone else (not through the framework) already paused the NPC, the claim notes it and does not resume it for them.

### Holds

```csharp
claim.Hold(NpcHolds.Sight | NpcHolds.Talk | NpcHolds.Freeze);   // replaces the previous holds
claim.Hold(NpcHolds.None);                                      // gives them all back
```

| Hold | While held |
|---|---|
| `Sight` | the NPC's field of view sees nothing: no crimes noticed, no police reaction to the player |
| `Talk` | the player cannot start a conversation with it |
| `Barks` | no barks or comments of its own; `NpcActions.Say` and `Comment` are refused too |
| `Visible` | it is not hidden at a distance or culled out of sight, so a scene you stage stays put |
| `Freeze` | it cannot move at all (its path finding agent is stopped) and its activity is paused; `MoveTo` is refused |

The game hooks behind the holds (`FadeGameObject.Cull`, `NPC_FOV.CheckIfTargetIsDetected`, `InteractableTalk.CanTalk`, `BarkStarter.TryBark`, `DialogueSystemTrigger.Fire`) run every frame for every NPC; each returns at once while no claim holds anything. Freeze is asserted once per frame for frozen NPCs, because the game turns movement back on in a few places (a talk ending, an activity starting). A sleeping or seated NPC is left still when Freeze is lifted.

### Animation

```csharp
var dance = claim.PlayAnimation(myClip, loop: true, fadeSeconds: 0.3f);
claim.Pause();                        // keep it from walking off mid-dance
...
claim.StopAnimation(0.3f);            // fades back to its own animation
```

`NpcAnimation` plays a clip through a playable graph of its own, blended over the Animator's state machine, so fading out returns the NPC to exactly what it was doing. Calling `PlayAnimation` again crossfades to the new clip. A clip that does not loop fades out by itself at its end. Clips come from your mod's asset bundles or from the game; legacy clips are refused. The same works on any Animator (a puppet, a mod NPC's body): `NpcAnimation.Play(this, animator, clip)` returns an `AnimationPlayback` you can `Stop(fade)` or dispose. One mod per Animator; a game NPC another mod has claimed is that mod's to animate. Everything stops when the mod unloads and when the level changes.

### Appearance

```csharp
claim.ApplyLook(LookData.FromJson(json));          // slider bodies only, through PlayerLook
claim.SetBlendShape("male_head", "_02UpperLidRaise", 80f);   // every mesh of that name
claim.SetMeshVisible("karl_prostetic_arm", false);
claim.SetMaterials("average_body", myMaterial);   // as many as the mesh has slots
foreach (var mesh in claim.Meshes())               // names, materials and blend shapes to pick from
    Log.Info(mesh.ToString());
```

All of it is put back when the claim ends (or with `RestoreAppearance()`), and none of it touches the game's shared character assets. `ApplyLook` only works on NPCs whose body has character editor sliders; most named NPCs have a baked body, which blend shapes, meshes and materials still reach.

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

Switching an entry off is the one change offered. The game skips disabled entries both for NPCs in the scene and for the off-scene simulation. Adding entries is not offered: an entry needs the game's own activity objects (nodes and groups that exist only in scene data), and a custom `Activity` subclass would be written into saves by type name and break them when the mod is missing. For NPCs of your own that keep a timetable, use a routine on a mod NPC ([mod-npcs.md](mod-npcs.md)).

## One-off actions

`NpcActions` does one thing to an NPC in your scene and leaves it to the game afterwards. No claim is needed; to hold an NPC for a while, claim it. Every call returns false when the NPC is not loaded, is dead, has no `NPCController`, or a save or load is running.

```csharp
NpcActions.Teleport(id, spot);                      // on the navigation mesh
NpcActions.FollowPlayer(id, run: false);            // until StopFollowing (or a level change)
NpcActions.Say(id, "Nice weather for it.");         // a bark line over its head
NpcActions.Comment(id, "Generic/Hit");              // a bark picked from a conversation
NpcActions.StartConversation(id, "My/Conversation");
NpcActions.LookAtPlayer(id, seconds: 5);
```

| Call | What it does |
|---|---|
| `Teleport(id, position)` | puts the NPC there at once through its path finding agent |
| `Kill(id)` | the game's own death (state Dead, kept in the save); raises `Npcs.Died` |
| `Hit(id)` | what a punch does without the damage: a reaction line or talk, a violent crime if the NPC reports those, an officer starts a chase, a robber gives up |
| `FollowPlayer(id, run, untilStopped)` / `StopFollowing(id)` | the game's follow activity; with `untilStopped` (the default) the schedule cannot interrupt it |
| `Say(id, text)` | a bark line, shown as given; refused during a conversation and while a claim holds `Barks` |
| `Comment(id, conversation)` | a bark from a conversation, the game's comments |
| `StartConversation(id, conversation)` | talks to the NPC with that conversation instead of its usual one |
| `LookAtPlayer(id, seconds)`, `LookAt(id, transform)`, `StopLooking(id)` | head looks; 0 seconds looks until stopped |
| `ChangeLevel(id, scene)` | sends an NPC that is not in your scene to another scene, the way the off-scene simulation does; refused for NPCs on the way somewhere, following someone or whose timetable wants them elsewhere |

These change the game the way playing would, and nothing is undone when your mod unloads.

## Relationships

The player's relationships (family, an ex, a pen pal...) are assets the game adds from the start choices and story events. Each has a disposition from -100 to 100 that drifts towards a target once per game day and, depending on it, sends gifts and mail.

```csharp
foreach (var r in Relationships.All())
    Log.Info($"{r.Name} ({r.Id}): {r.Disposition} drifting to {r.Target}, {r.ChangePerDay}/day, active {r.Active}");

Relationships.SetDisposition("Mother", 60f);              // and the target, so it stays
Relationships.SetDisposition("Mother", 60f, target: 20f); // starts at 60, drifts down to 20
Relationships.SetActive("Mother", false);                  // pauses drift and gifts
Relationships.Changed.Subscribe(this, c => Log.Info(c.ToString()));
```

Ids are what the game's Lua `RelationshipIdActive` checks; a name works too. Values are clamped to -100..100. They are saved with the game and not undone when your mod unloads. `Changed` reports `Started`, `Ended` and `Disposition` changes (the daily drift and your own sets). Outside a level the list is empty.

## Characters

`Characters.All()` lists the game's character database: every character it has an asset for (294 in 0.5.13), the player first, with `HasBody` (the game can spawn it: 79 of them), `HasAi` (that body has NPC AI and can walk: 11) and `HasSliders` (its body has character editor sliders: only 2). Use it to pick mod NPC templates. At the main menu, before the database is filled, the assets are read from the game's resources.

## Police, robbers, janitors and surveillance

```csharp
Pursuit.Changed.Subscribe(this, chased => Log.Info(chased ? "the police are after the player" : "the chase is over"));
bool hot = Pursuit.IsPursued;
Pursuit.CallPolice(severe: true);          // officers within 15 m chase (anyDistance: true for all)
Pursuit.StopAll();                         // calls every chase off, like after an arrest

foreach (var robber in Robbers.All())      // NPCs out robbing, in the scene and off it
    Log.Info($"{robber.NpcId}: {robber.State}");
Robbers.Robbed.Subscribe(this, npcId => Log.Info($"robbed by {npcId}"));

Surveillance.PlayerSpotted.Subscribe(this, s => Log.Info(s.ByCamera ? "camera" : $"seen by {s.NpcId}"));
Surveillance.TrespassStarted.Subscribe(this, area => Log.Info($"trespassing in {area}"));
Surveillance.SetCameraOn(cameraAddress, false);

Janitors.Acted.Subscribe(this, a => Log.Info($"{a.Tag}: {a.Kind} at {a.Address}{(a.Replay ? " (replayed)" : "")}"));
```

| Part | What there is |
|---|---|
| `Pursuit` | `IsPursued`, `ActivePolice()` (officer ids in the scene), `CallPolice(severe, anyDistance)`, `StopAll()`, `Changed` (true when a chase starts, false when the last one ends) |
| `Robbers` | `All()`, `State(id)`, `Aggro(id)`, `Rob(id)` (refused when the player carries less than the robber wants), `Negotiate(id)`, `SetAloof(id, aloof)`, `Robbed` |
| `Surveillance` | `Cameras()` (address, on, sees the player, range, field of view), `SetCameraOn(address, on)`, `IsTrespassing`, `IsPlayerSeen(crime)`, `PlayerSpotted` (a witness reported a crime), `TrespassStarted` / `TrespassEnded` |
| `Janitors` | the story's janitor actions: tagged changes (a storage filled, relays fired) that run in the scene when dialogue triggers the tag and are replayed in every other level the first time it loads. `Tags()`, `Has(tag)`, `Trigger(tag)` (saved by the game, not undone), `Acted` with `Replay` telling the two apart |

Cameras and the other objects are named by their `ObjectAddress` string (see [world.md](world.md)). The trespass hooks run every physics step while the player stands in an area and return at once while nobody listens; when the level changes while the player is inside, `TrespassEnded` is raised once with an empty address.

## Dev bridge

```sh
oben call npcs.list loaded=true
oben call npcs.list scene="Interior Player Tenement" name=Malone
oben call npcs.get name=Mirjam
oben call npcs.nearest maxDistance=10
oben call npcs.act name=Mirjam action=say text="Hello there"
oben call npcs.act id=11 action=follow
oben call npcs.relationship
oben call npcs.relationship id=Mother value=50
oben call npcs.crime
oben call npcs.crime action=stopPursuits
oben call npcs.characters name=Pentti
```

`npcs.list` prints id, name, scene, position, activity and whether the NPC is loaded; `npcs.get` adds the job, who claims it and its schedule; `npcs.nearest` gives the closest living NPC with its distance. Those three are read only. `npcs.act` runs one of the one-off actions above (`teleport`, `kill`, `hit`, `follow`, `unfollow`, `say`, `comment`, `talk`, `lookAtPlayer`, `stopLooking`, `changeLevel`); `npcs.relationship` lists relationships and sets one; `npcs.crime` shows the police, robbers, cameras, trespassing and janitor tags and can call (`callPolice`, `chase`) or stop the police; `npcs.characters` lists templates for mod NPCs. The mod NPC commands are in [mod-npcs.md](mod-npcs.md). See [../dev-tools.md](../dev-tools.md).
