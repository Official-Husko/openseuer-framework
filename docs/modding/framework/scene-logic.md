# Scene logic, landmarks, signs and saved objects

The level designers script Obenseuer's levels with small logic objects: relays that fire lists of outputs, true/false branches, counters, named-state machines, timed sequences and timers. `ObenFramework.World.Logic` reads them and changes them through the game's own methods, so whatever a level hangs off them reacts as if the game had done it, and the game saves the result as usual. The same page covers map landmarks, editable signs, the game's saved objects and the scenes the player has visited (`ObenFramework.World`).

## Ids

Every list hands out an id per object, from `ObenFramework.Scenes.SceneIds`: the object's save GUID when it has one (most relays, branches, counters, state machines, sequences and signs do), otherwise its [`ObjectAddress`](world.md) text. The setters take that id, an address, a bare GUID, a hierarchy path or `#instanceId`. Lookups scan the active scene, so call them on events or commands, not every frame. Only the active scene is searched.

```csharp
string id = SceneIds.Of(component);            // GUID or address
var relay = SceneIds.Find<Relay>(id);          // or null
List<Door> doors = SceneIds.Active<Door>();    // every one in the active scene
```

`ObjectPath.Split` and `ObjectPath.Descend` walk plain `Root/Child` paths over any tree, trying every sibling that shares a name; `WorldObjects.FindByPath` uses them on a loaded scene.

## State: branches, counters, state machines, sequences, timers

`SceneState` lists each kind for the active scene and has a setter per kind. Every setter returns false when the id matches nothing, the value is refused, or the game is saving or loading.

| Kind (game type) | List | Change |
| --- | --- | --- |
| Branch (`logic_branch`) | `Branches()`: id, value | `SetBranch(id, value)`: the game tests the branch right away (firing its true/false relay) unless the branch is set to manual testing |
| Counter (`CounterRelay`) | `Counters()`: id, value, max | `SetCounter(id, value)` (0 or more), `AddCounter(id, amount)` (1 or more); reaching the maximum fires the counter's relay and may reset it, as in the game |
| State machine (`logic_state`) | `States()`: id, current, names | `SetState(id, name, trigger: false)`: only one of its own states; with `trigger` the state's relay fires too |
| Sequence (`Logic_Sequence`) | `Sequences()`: id, entries, loop | `StartSequence(id)`, `StopSequence(id)` |
| Timer (`logic_timer`) | `Timers()`: id, interval, running, disabled | `StartTimer(id)`, `StopTimer(id)`, `SetInterval(id, seconds)` (above 0, at most a day) |

The rules behind the refusals are in `SceneStateRules`, which has no Unity types and is unit tested.

## Relays

`SceneLogic.Relays()` lists the relays with whether they fire only once, have fired and have every output switched on. `SceneLogic.Fire(id)` fires a relay the way the level would, and only when it would really fire: active in the scene, not used up (fire-once relays that already fired), and its task item, objective and dialogue variable requirements met (`CanFire` tells). `SetEnabled(id, on)` switches all outputs of a relay or a trigger volume (`TriggerMultiple`).

`SceneLogic.Fired` is raised after every relay that fired its outputs, with the relay's id, name, fire-once flag and whether its outputs were switched on (`Enabled`):

```csharp
SceneLogic.Fired.Subscribe(this, fired => Log.Info($"relay {fired.Name} ({fired.Id}) fired"));
```

It is one Harmony postfix on `Relay.triggerOutputs(object)`, which every relay subclass calls; the prefix returns at once while nobody listens. Calls the relay ignored (used up, inactive, requirements not met) aren't reported. Outputs with a delay are still pending when the event runs. `SceneLogic.IsAvailable` is false when that hook failed to apply after a game update.

## Landmarks

`Landmarks` covers the places on the in-game map.

- `All()`: every landmark loaded in the game (the game's and the mods'), with title, whether the player owns it and whether a mod made it. `Has(id)` checks one.
- `Unlock(id)` hands out one of the game's landmarks at its own position, like discovering it. `Place(id, position)` puts one of the game's landmarks on the current map wherever you like.
- `Added` is raised for every landmark added to the current map (discovered, handed out, placed), not for mod landmarks being put back after a load.

Mods add their own with `Create(owner, id, title, position, iconPath)` (a PNG or JPG in the mod folder), `Create(owner, id, title, position, sprite)` (a runtime sprite) or `Mark(owner, id, title, position)` (a red dot, without the "Discovered" notification unless asked). They are kept per map in the framework's save file (`ObenFramework.landmarks.json`) and put back once the map has loaded after every level change and load. Calling `Create` again for the same id on the same map keeps the saved position and updates the rest. `Remove(owner, id)` and `Clear(owner)` take them away; `Saved(modId?)` lists the records.

The game saves the map's landmark lists itself. Right before `SaveController.SaveGame` writes, mod landmarks are taken out of those lists and put back afterwards (also when the save throws), so the game's save never refers to a landmark that only exists while a mod is loaded. A landmark with a runtime sprite can't come back by itself: the mod has to create it again in a later session (its saved position is kept). When a mod unloads its landmarks leave the map, and come back when it loads again. `Landmarks.IsAvailable` is false when the framework's landmark hooks failed to apply after a game update (then `Added` stays quiet and mod marks could end up in the game's save). Limits: 128 per mod, 1024 in all; ids are letters, digits, `.`, `_` and `-`, up to 64; the game sees them as `modId:id`.

## Signs

`Signs.All()` lists the editable signs of the active scene (id, text, line and length limits); `Get(id)` reads one. `SetText(id, text)` sets the text, refused while the player is typing on that sign, while the game saves or loads, or when the text is longer or has more lines than the sign takes. The game saves sign text itself. `Signs.TextChanged` is raised when the player finishes typing (only if the text changed) or a mod set new text, with `ByPlayer` telling which; `Signs.IsAvailable` is false when its hooks failed to apply. Editable signs come from the build menu's Signs group (the small label, for one), not from the levels.

## Saved objects and per-object data

`WorldObjects` works with the objects the game saves: anything with a save GUID or spawned from a prefab the save records.

- `Find(guid)`, `FindByPath(scene, "Root/Child", includeInactive)`, `PathOf(go)`, `GuidOf(go)`, `IsSaved(go)`, `InfoOf(go)` (GUID, name, kind: collectible, item, package, coin or other; position; scene) and `Near(position, radius)`.
- `SetData(owner, info, text)` keeps up to 16384 characters with an object for this save, `GetData(owner, info)` reads it back, `SetData(owner, info, null)` removes it. A mod has 256 KB in all (entries count 128 characters each on top of their text). The data sits in the framework's save file (`ObenFramework.objectdata.json`), per mod, scene and GUID. Objects without a GUID can't hold data, and neither can placed furniture: the game gives it a new GUID on every save.

## Where the player has been

`WorldLocation.CurrentScene`, `KnownScenes()` (every scene by the game's own list) and `VisitedScenes()`: what the game records (`SaveSceneManager.visitedScenes`), the scene now, the levels the loaded save holds a file for, and the scenes entered since it loaded (loading another save starts that list again), each once, without the main menu and the intro.

## Dev bridge

| Command | Parameters | |
| --- | --- | --- |
| `scene.state` | `kind?` (branches, counters, states, sequences, timers, relays), `match?` | the active scene's logic objects with ids |
| `scene.set` | `kind` (branch, counter, add, state, sequence, timer, interval, outputs), `id`, `value`, `trigger?` | change one |
| `scene.fire` | `id` | fire a relay if it would fire now |
| `landmarks.list` | `owned?` | loaded landmarks and the saved mod landmarks |
| `landmarks.unlock` | `id` | hand out one of the game's |
| `landmarks.mark` | `id`, `title?`, `pos?`, `remove?` | a red dot owned by DevBridge |
| `signs.list`, `signs.set` | `id`, `text` | editable signs |
| `objects.near` | `radius?`, `pos?`, `max?` | saved objects near the player |
| `world.visited` | | current, visited and known scenes |

```sh
oben call scene.state kind=relays match=door
oben call scene.set kind=state id=<id> value=Open trigger=true
oben call landmarks.mark id=stash title="My stash"
```
