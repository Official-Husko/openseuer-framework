# Mod NPCs

`ModNpcs` (in `ObenFramework.Npcs`) spawns NPCs of your mod's own: copies of a game character with their own name and character id, standing in the scene the player is in. To the game they are real NPCs: they show up in `Npcs`, raise `Npcs.Spawned` and `Npcs.Died`, can be claimed, animated and talked to (if you allow it), and die like any other. On top of that a mod NPC can be dressed, ordered about and given a weekly routine. Reading and controlling the game's own NPCs is in [npcs.md](npcs.md).

Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Npcs;`.

## Spawning

```csharp
var barkeep = ModNpcs.Spawn(this, "barkeep", new ModNpcSpec
{
    Template = 253,                // a game character to copy; 253 has an AI body, so it can walk
    FirstName = "Raimo",
    LastName = "Baarimikko",
    Position = counterSpot,
    Yaw = 180f,
});
if (barkeep == null) return;       // the reason is in the log
```

| Spec field | Meaning |
|---|---|
| `Template` | character id of the game NPC to copy: its body, voice, animations and default look. The player (character 0) and other mod NPCs cannot be templates |
| `Body` | character id whose body to use instead, or 0. A template without a body of its own gets the first body with character editor sliders |
| `FirstName`, `LastName` | what the player sees. `FirstName` is required |
| `Position`, `Yaw` | where it stands (put on the ground below, within a few metres, and moved aside if something is in the way) and which way it faces |
| `KeepDialogue` | keep the template's conversation; off by default, so the copy cannot be talked to until you start conversations yourself |
| `Saved` | keep it in the save, see below |
| `Look` | a `LookData` for slider bodies, see below |

The name (`"barkeep"`) is per mod: two mods can both have a barkeep. `Spawn` returns the NPC if it is already here, so calling it from `GameEvents.WhenWorldReady` every time is fine. It returns null outside a level, while a save or load runs, when the template has no body to spawn or there is no ground below the position; the reason is logged.

What the copy gets: a character id from 1,000,000 up, hashed from your mod id and the name so it is the same every session (Lua, mail and the console can use it), moved on by one if another character already has it. Everything that would tie the body to the template is stripped: its save ids (the game does not save the copy as the template), its persistence components, its barks.

`ModNpc` is the handle:

| Member | Meaning |
|---|---|
| `Owner`, `Name`, `CharacterId`, `Template`, `Body`, `FullName`, `Scene`, `Saved` | who it is |
| `Loaded`, `Alive`, `Npc`, `Position`, `Record` | whether it still exists, the game's `NPCInfo`, where it stands, an `NpcRecord` like `Npcs.Get` gives |
| `Teleport(position)` | puts it elsewhere in the scene |
| `Despawn()` | removes it and forgets it in the save |

`ModNpcs.Get(this, "barkeep")`, `ModNpcs.Get(characterId)`, `ModNpcs.List(this)` and `ModNpcs.All()` find them again; `ModNpcs.Despawn(this, name)` and `ModNpcs.Clear(this)` remove them. Every NPC your mod spawned is removed when your mod unloads (saved ones come back when it loads again) and when the level changes.

### Which templates can walk

Of the game's 294 characters (0.5.13) only 79 have a body of their own, and only 11 of those bodies have the game's NPC AI (an `NPCController` with a path finding agent). Only a copy of one of these can walk, take orders or follow a routine; every other body stands where it is spawned (it can still be talked to, claimed for looks and animations, and saved). `ModNpc.CanMove` says which kind you got. Orders on a body without AI return false and log once why; `NpcRoutines.Set` refuses such a body.

| Id | Character | Body |
|---|---|---|
| 144 | Empty Test Map NPC | a plain townsperson; the safest choice for testing |
| 145 | Empty Test Map NPC Follower | plain |
| 146 | Empty Test Map Police | the only AI body with character editor sliders (wears a `LookData`) |
| 253 | Bror Stjäla | |
| 289 | Jaana Ahovaara | the only female AI body |
| 107 | Crazy Neighbor | story NPC |
| 84, 85, 172, 173, 188 | the police of the market square, backyard, gold store, bazaar and slums | police bodies |

To give another character's look an AI body, set `Body`: for example `Template = 59, Body = 144` walks on the plain body, and a character without a body of its own (`HasBody` false) with `Body = 146` gets its own slider values on the one slider body that walks. Bodies without AI are not made to walk: the framework does not add AI components to a prefab that was not built with them.

Of the 79 bodies only 2 have character editor sliders (Lizzy Mopper, 126, without AI, is the one used for templates that have no body; Empty Test Map Police, 146, has AI). The other 215 characters are spawned on Lizzy Mopper's body with their own slider values; 40 of them carry worn items, the rest get a head of their gender. `Characters.All()` and `oben call npcs.characters` show `HasBody`, `HasAi` and `HasSliders` for each (`oben call npcs.characters ai=true` lists the walking ones).

## Saved NPCs

```csharp
GameEvents.WhenWorldReady(this, scene =>
{
    if (scene == "Open Sewer Bazaar")
        ModNpcs.Spawn(this, "trader", new ModNpcSpec { Template = 100, FirstName = "Ville", Position = stall, Saved = true });
});
```

A saved NPC is written to the framework's per-save file (`mods/ObenFramework.npcs.json` next to the save) with the scene it was spawned in, its position and facing at save time, and its look. Every time that scene is ready it is spawned again, as long as your mod is loaded; a mod that loads mid-game (a hot reload) gets its NPCs back at once. For a saved NPC the save wins: if the save already has it in this scene, `Spawn` brings it back the way it was saved and ignores the spec's position and look. `Despawn` (or `ModNpcs.Clear`) forgets it. A saved NPC that dies leaves the save at the next save. Entries of mods that are not installed stay in the file, so the NPCs return with the mod. Up to 256 saved NPCs per mod.

## Looks

```csharp
var look = PlayerLook.Capture();                 // or LookData.FromJson(...)
look.hat = "Hat small - Black Beanie";
npc.SetLook(look);                               // slider bodies only (npc.HasSliders)

npc.SetBlendShape("male_head", "_02UpperLidRaise", 60f);   // a mesh of the body and one of its shapes
foreach (var mesh in npc.Meshes()) Log.Info(mesh.ToString());
npc.ResetLook();                                 // back to the template
```

A mod NPC owns its copy of the character, so a look is written straight into it and the body rebuilt; the game's shared character assets are never touched. `Look` reads the look the mod set (or, for a slider body, what it shows now). `SetLook` only works on slider bodies; baked bodies (most templates) take blend shapes, which go into the copy's mesh list so a rebuild keeps them. `AvailableShapes()` lists every mesh and shape with its current weight. A saved NPC keeps its look and shapes in the save. To change a game NPC's look for a while, claim it instead (see [npcs.md](npcs.md)).

## Orders

```csharp
barkeep.WalkTo(door, yaw: 90f, run: false, done: arrived => Log.Info(arrived ? "at the door" : "did not make it"));
barkeep.Wander(square, radius: 6f);
barkeep.Patrol(new[] { a, b, c });
barkeep.Sit(chairSpot);             // nearest free chair within 1.5 m
barkeep.Sleep(bedSpot);             // nearest free bed within 2 m
barkeep.StopOrder();
```

Orders use the game's own NPC path finding (`NPCController.SetTargetPosition`, so doors and waypoints work) and, for sitting and sleeping, the game's own `Act_sit` and `Act_Sleep` activities. One order at a time: a new one replaces the last, whose `done` gets false. `WalkTo` ends on arrival (`done(true)`, then the NPC turns to `yaw`) or after a timeout of three times the walking time plus ten seconds; wandering (between random reachable points, waiting 4 to 15 seconds at each), patrolling (round and round) and sitting or sleeping go on until stopped. `Order` says what it is doing.

Orders wait while the player talks to the NPC, while a claim pauses or freezes it, and end when it dies or leaves. An order given right after `Spawn` waits the few frames until the NPC's controller has started. Orders are not saved. On a body without AI (`CanMove` false) every order returns false and the log says why once.

## Routines

```csharp
var routine = new NpcRoutine()
    .Stand(NpcDays.Weekdays, TimeSpan.FromHours(8), TimeSpan.FromHours(16), "Interior Bar", counter, yaw: 90f)
    .Sit(NpcDays.Weekdays, TimeSpan.FromHours(16), TimeSpan.FromHours(17), "Interior Bar", stool)
    .Wander(NpcDays.Weekend, TimeSpan.FromHours(12), TimeSpan.FromHours(18), "Open Sewer Bazaar", square, 6f)
    .Sleep(NpcDays.Every, TimeSpan.FromHours(23), TimeSpan.FromHours(7), "Interior Bar", bed)   // past midnight
    .Door("Interior Bar", barDoor);

NpcRoutines.Set(this, "barkeep", new ModNpcSpec { Template = 253, FirstName = "Raimo" }, routine);
NpcRoutines.StepChanged.Subscribe(this, change => Log.Info(change.ToString()));
```

A routine is a weekly timetable: steps on some days (`NpcDays` flags) between two times, each in a scene with a kind (`Stand`, `Wander`, `Patrol`, `Sit`, `Sleep`). A step that ends before it starts runs past midnight. Where steps overlap the one added first wins; when no step covers the time the NPC is away, in no scene. The routine checks once per game minute and when the scene changes:

- When the current step is in the scene the player is in and the NPC is not there, it is spawned at the step's point and starts the step. If the step starts while the player is in the scene and the routine has a `Door` there, it appears at the door and walks in.
- When the step moves to another scene (or the NPC is away), it walks to the scene's door and is removed there, or is removed at once when the scene has no door. An NPC the routine did not spawn is never taken away.
- Your own orders on the NPC win over its step until they end; then the step starts again within a minute.

`NpcRoutines.Current(this, name)` gives the step the clock says now (wherever the NPC is), `Names(this)` the NPCs with routines, `Remove` and `Clear` end them (removing the NPCs they spawned). The spec gives who the NPC is; its position and `Saved` are not used: routine NPCs are not saved, the routine puts them back. `Set` refuses a name your mod already keeps in the save or spawned by hand. Up to 64 routines and 64 steps each per mod. Routines end when your mod unloads.

## Things to know

- The NPC's body is the template's prefab (or `Body`'s); it does what that body does. Only AI bodies move at all (see above), and even they stand still unless given orders or a routine: a mod NPC has no game schedule.
- Spawning, saving and restoring were written from the game's code (0.5.13) and checked against it, but treat them as new: test a template in game before relying on it.
- Character ids from 1,000,000 up are reserved for mod NPCs; `ModNpcs.IsModNpc(id)` tells them apart.

## Dev bridge

```sh
oben call npcs.characters name=Agnes
oben call npcs.characters ai=true
oben call npcs.spawn name=test template=144 first=Testi
oben call npcs.spawn name=test2 template=59 x=1 y=0 z=4 yaw=90 saved=true   # a baked body without AI: stands
oben call npcs.mods
oben call npcs.order name=test order=walk          # to the player
oben call npcs.order name=test order=wander radius=5
oben call npcs.look name=test meshes=true
oben call npcs.look name=test mesh=male_head shape=_02UpperLidRaise weight=50
oben call npcs.despawn name=test
```

NPCs spawned from the bridge belong to DevBridge, so a DevBridge reload removes them (saved ones come back). `npcs.spawn` places the NPC two metres in front of the player, facing them, unless given a position. See [../dev-tools.md](../dev-tools.md).
