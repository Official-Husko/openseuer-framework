# Object state, the save gate and save slots

`ObenFramework.Saves` can read and restore the saved state of live game objects without writing a save, tell whether the game is saving or loading, and list and trigger the game's own save slots. This is what a co-op host needs to hand its world to a joining player, and what any mod needs to copy one object's state onto another. For per-save data a mod keeps for itself see [saves.md](saves.md); for how the game's save system works see [../../game/save-system.md](../../game/save-system.md) (section numbers below point there).

Nothing here touches a save file on disk. Capture and apply run the objects' own save and load callbacks against private lists and the game's own serializer.

## SaveState

```csharp
string blob = SaveState.Capture(door);          // game save JSON for one object
bool ok = SaveState.Apply(otherDoor, blob);     // restore it through the object's own load code

Dictionary<string, string> tree = SaveState.CaptureTree(go);         // every savable under go, by GUID
SaveApplyResult r = SaveState.ApplyTree(go, tree, allowedGuids);

Dictionary<string, string> globals = SaveState.CaptureGlobals();     // what Globals.tnmt would hold
SaveApplyResult g = SaveState.ApplyGlobals(globals, allowedGuids);
```

- A blob is exactly the JSON the game writes for that object in a `.tnmt` file: `{"GUID":"...","Data":{ ...fields..., "$type":"Door"}}`. Checked against the game's own file writer: the bytes are identical, and reading a blob back and writing it again gives the same text.
- `Capture` runs the object's save phases (`OnSavingGamePrimary` to `OnSavingGameLatePrimary`, only the ones it overrides), takes the entry the object wrote under its GUID, and serializes it. It returns null when the object is not a `SavableScript`, has no GUID, a save or load is in flight, or the object wrote nothing.
- `Apply` runs the object's load phases with the blob as the only entry, so the game's `ReplaceProperties` rules decide what is copied (see the limits below). It returns true only if the object actually took the entry. Before applying it captures the object's current state, and puts that back if a load phase throws halfway.
- `CaptureTree` and `ApplyTree` do the same for every savable under a GameObject, matched by GUID. Phases run the way the game runs them: all objects for one phase, then the next. Inactive objects are left out unless asked for, as a real save leaves them out. Apply handles objects that load in earlier phases first, so managers are restored before what depends on them.
- `CaptureGlobals` runs the save callbacks of the whole active scene and the game's extra root list (as an autosave would) and returns the entries the game files under `Globals.tnmt`: money, inventory, time of day, relationships, crime, the tenement, and so on. In the tenement that was about 48 entries and 285 thousand characters, taking some 50 ms. Level objects are not included. `ApplyGlobals` matches by GUID against the live singletons.
- Everything returns empty or false while `SaveGate.IsBusy`. Call from the main thread, after the world is ready (`GameEvents.WhenWorldReady`).

### Blobs from other players

A blob read from the network is untrusted, because the game's reader creates whatever `$type` a file names. `Apply`, `ApplyTree` and `ApplyGlobals` therefore check every blob first:

- at most 4 million characters and 256 levels of nesting, no comments;
- the GUID in the blob must match the object it is applied to, and the root `$type` is forced to that object's own type;
- any other `$type` must resolve to a known type that is not, and does not hold, a Unity object from another assembly (no GameObjects, components or transforms);
- a `Type` plus `Data` pair, which the game's reader would turn into a `$type`, is refused.

Refusals are logged and counted in `SaveApplyResult.Failed`. For blobs from a peer also pass `allowedGuids`, the GUIDs you are willing to let that peer change; anything else is skipped.

## Limits

A blob carries what the game's save carries, and no more. It is not a copy of the object.

- **GUID.** Only objects with a public string field named `GUID` that is non-empty can be captured or applied (6.1). A private field or a property is ignored. Runtime objects get their GUID from code and must have the same one on both machines. Two objects with the same GUID in a tree collide: the later capture wins and the log says so.
- **What the serializer drops (7.1).** Anything marked `[NonSerialized]` or `[fsIgnore]`, static fields, delegates, properties that are not public auto-properties. Unity references (`Transform`, `GameObject`, `Sprite`, inspector links) are not saved at all; the game marks them `[fsIgnore]`.
- **What apply does not restore (7.2).** `ReplaceProperties` copies public, protected and internal fields, and private fields only with `[fsProperty]`. A private `[SerializeField]` field without `[fsProperty]` is in the blob but never restored. Private fields declared in a base class are never restored. Properties are never restored. Objects whose state sits in such fields come back partly restored, exactly as they do when a save loads.
- **Missing members take defaults.** Apply creates a fresh copy of the type and copies every restorable field, including those absent from the blob. A hand-trimmed blob resets the omitted fields to their constructor defaults instead of leaving them alone.
- **Callbacks have their usual side effects.** Capture runs the real save callbacks, so an object that sets a flag while saving (a door's `saved`) sets it. Apply runs the real load callbacks, which may call `Start()` or move things.
- **Phases left out.** The destructible list phases, `OnSavingFile` and the map change phases are not run. So a blob does not contain the destroyed-object list or the runtime-spawned prefabs of `DestructibleList` (6.4, 8.3) beyond what that singleton keeps in its own entry, and applying does not spawn or destroy objects. Recreating spawned objects is the caller's job.
- **Not part of the object blobs.** Dialogue System state (`Globals.dialogue`, Lua variables, quest progress; see `LuaVariables` and `QuestLog`), the map reveal image, and anything a mod keeps with `ModSaves`.
- **Global data needs a live host.** `CaptureGlobals` only sees singletons that exist and are active in the scene (8.4). One that is missing from the scene contributes nothing.
- **Unknown mod types.** A blob of a mod component names that mod's class in `$type`. The receiving side needs the same mod loaded or the blob is refused.

## SaveGate

```csharp
if (SaveGate.IsBusy) return;                       // a save or a load is in flight
SaveGate.WhenIdle(this, () => Send(SaveState.CaptureGlobals()));
```

- `IsSaving` is true inside a save, `IsLoading` from the moment a level change or load is announced until the game has restored the world. `IsBusy` is either. Unlike `SaveController.Loading` it stays on through the scene load (5.6).
- Loads the game abandons (a missing save, an aborted migration, a level change that only saved) never raise `LoadingDone`. The gate drops such a load once the game's flag is off and its loading screen is idle, and as a last resort after five minutes, so it can never stay shut.
- `WhenIdle(owner, action)` runs the action at once when idle, otherwise on the first frame the gate opens. It is dropped if the owner unloads first. This is not the same as the world being ready: after a load the game still moves the player a moment later, so use `GameEvents.WhenWorldReady` for world edits.
- During a `Saved` handler the save is already over, so `IsBusy` is false there.

## SaveSlots

```csharp
foreach (var c in SaveSlots.Characters())
    foreach (var s in SaveSlots.Slots(c))
        Log.Info($"{c.Name}: {s.Slot} at {s.Level}, saved {s.Saved}");

string folder = SaveSlots.CurrentFolder;     // the loaded save, or null
if (SaveSlots.CanSaveNow) SaveSlots.SaveNow("Slot3");
```

- `Characters()` and `Slots()` read the game's own headers (`GetCharacters`, `GetSavedGameInfo`), so they list what the load menu lists plus `Autosave2`, which the menu's character scan skips. A slot whose header cannot be read has `Corrupted` set. Without any saves yet the lists are empty.
- `CurrentFolder` and `CurrentSlot` describe the save last loaded, and are null at the main menu, before the first save of a new game, or when the folder does not belong to the current character.
- `CanSaveNow` is true when the game would allow a save: a world is up, nothing is loading, fading, sleeping, arrested or in conversation, the pause menu's save button is enabled (it is off on difficulties without manual saves), and no earlier `SaveNow` is still pending.
- `SaveNow()` starts the next autosave in the game's rotation; `SaveNow("Slot3")` writes that slot (any of `SaveSlots.SlotNames`), replacing what was there, the way saving does in the pause menu. It goes through the game's own save path, including the saving screen, and starts a couple of frames later. Wait for `GameEvents.Saved` for the result. It returns false when it could not start.

## Debugging

DevBridge has read-only commands for all of this:

```sh
oben call save.capture target=Door component=Door     # blob preview, truncated to 1500 characters
oben call save.capture target="Root/Child" tree=true  # every savable below, keyed by GUID
oben call save.slots
oben call save.busy                                   # next to the game's own Loading flag
```
