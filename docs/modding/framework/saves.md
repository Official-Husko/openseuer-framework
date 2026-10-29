# Per-save mod data

`ObenFramework.Saves` lets a mod keep its own data for each save game. The framework stores it as JSON next to the game's save files, never inside them, so a save still loads without the mod and the game's `.tnmt` files stay exactly as the game wrote them.

For how the game's own save system works see [../../game/save-system.md](../../game/save-system.md). Section references below (such as 5.2) point there.

## Using it

```csharp
public sealed class MyData
{
    public int Visits;
    public List<string> Seen = new List<string>();
}

public sealed class MyMod : ObenMod
{
    private ModSaveData<MyData> _save;

    public override void OnLoad()
    {
        _save = ModSaves.Register<MyData>(this);

        // Optional: react when a save's data arrives. The world is not restored yet.
        _save.DataLoaded.Subscribe(this, data => Log.Info($"{data.Visits} visits so far"));
        GameEvents.WhenWorldReady(this, _ => _save.Value.Visits++);
    }
}
```

- `ModSaves.Register<T>(owner, key = null)` returns a `ModSaveData<T>`. `T` is a class with a parameterless constructor; its defaults are what a new game starts with. It must round trip through Newtonsoft.Json (public fields and properties, lists, dictionaries, enums, nested classes).
- `Value` is never null. Read it through the property every time: the framework **replaces** the object when a save loads, so a reference cached from before is stale afterwards.
- Change `Value` whenever you like. Nothing has to be called to save: the framework writes every registered object after each game save. A handler on `GameEvents.Saving` runs before that if you want to update fields right before the write.
- `Reset()` puts defaults back. `DataLoaded` fires after every replacement (load, reset, main menu).
- Several objects per mod: pass a `key`, which gives `<modId>.<key>.json`. Registering the same mod and key twice throws.
- The registration ends when the owner unloads. On a hot reload the current data is parked in the loader's `ReloadState` and handed to the new copy, so unsaved changes survive a reload. This only applies if no save was loaded in between; otherwise the loaded save's file wins.
- `ModSaves.Peek<T>(modId, key)` reads what any mod stored in the loaded save without registering (a copy; null when there is nothing). Handy for reading another mod's data or your own from before you register.
- A file over 64 MB isn't read back: it is set aside as `.corrupt` and the data starts from defaults, as for a file that can't be parsed. Newtonsoft's own limit of 64 levels of nesting applies too.
- Add a version field to `T` yourself if the shape will change. Unknown JSON members are ignored and missing ones keep the constructor's defaults, so adding fields is safe.

## What gets written where

```
Saves/<Character>/<SaveName>/
    Info.tnmt  Globals.tnmt  <Level>.tnmt  Globals.dialogue  <map>.png    (the game's)
    mods/
        MyMod.json
        MyMod.visited.json
```

File content is `{ "Format": 1, "Data": { ...your object... } }`. `Format` is the layout of the file itself, not of your data. Names are `<modId>[.<key>].json` with anything that isn't a letter, digit, `-` or `_` replaced by `_`.

A file that can't be parsed is copied to `<name>.json.corrupt`, the error is logged, and the mod gets defaults (so the next save doesn't silently destroy the only copy).

## When it is read and written, and why

The rules come from how `SaveController.SaveGame` handles folders (5.2, 9.1).

**Write on `GameEvents.Saved` (the game's `SavingDone`), into the folder the game just wrote.**
`SaveGame` deletes the destination folder and recreates it whenever it differs from `LastSaveFolderPath` (saving to another slot, the autosave rotation, the first save of a new game), then copies the old folder's other files over. Anything written earlier, for example in `SavingStarted`, disappears. Writing in `SavingDone` is after the delete, the copy and all game files. The framework writes *all* registered objects each time, not just changes, because the destination may be a brand new folder.

The folder comes from `SaveController.GetCurrentSavePath()`, which is valid at that point because `SaveGame` has just set the character and save name (it also copes with the character being a full path after a menu load).

**Read on `GameEvents.Loading` (the game's `LoadingStarted`), from `SaveController.LastSaveFolderPath`.**
That field is set when a load begins and points at the folder being loaded. It is also right after a door or level change: the game saves into an autosave folder, then loads that same folder, so the data written a moment ago is read straight back. Reading at `LoadingStarted` means the data is ready before the game restores any object, so components can use it in their own load phases. If the path is empty or the file is missing (a save from before the mod, or a mod added later) the object is reset to defaults.

**Reset on `GameEvents.MainMenu`.**
Quitting to the menu clears `LastSaveFolderPath`, and a new game never loads anything, so the main menu is the one point that is always passed before a new game. Everything goes back to defaults there, which is what makes a new game start clean.

**Why not mod types inside the `.tnmt` files (9.2).**
That would put the mod's class names into `$type`. Without the mod loaded, those saves go through the slow migration path on every load and the data is lost. It also needs a component alive in every scene at every save. A side file has neither problem.

### Surviving the game's copying and deleting

- Saving to another slot copies the whole last-loaded folder, including `mods/`, to the new destination and then we overwrite our files with current values. Files of mods that aren't loaded right now are carried along untouched, so disabling a mod for a while doesn't lose its data (as long as you keep saving over the same lineage of slots).
- Deleting a save in the load menu deletes the folder recursively, and our files go with it, exactly like the game's own.
- A save to an existing slot deletes that slot first. Stale mod files in the old slot do not leak into the new save, only what came from the last-loaded folder plus what we write.
- `mods` is a sub folder of the save folder; the game ignores unknown entries there. We never create anything directly under `Saves/` (every directory there is probed as a character) or folders named `Slot*` / `Autosave*`.
- Files are written through `JsonFile.Write`: a temp file in the same folder, then a replace, so a crash leaves either the old or the new file.
- The map PNG is written by a background task after `SavingDone`. We don't enumerate or copy the save folder at that point, so it doesn't matter to us.

### Limits

- A hard kill between the game finishing its save and our write (a few milliseconds, same call) would leave the previous mod data next to the new game data.
- Two characters with the same name share a folder (the game's rule), and so share mod data.
- Do not apply loaded data to the world in `DataLoaded`; the scene is not restored yet. Use `GameEvents.WorldReady` (or `WhenWorldReady`).
- Load events also fire for the reload after every door. `DataLoaded` fires then too, with the data that was just saved. If your mod holds state that is *not* in the data object, it is on you to keep it across that reload.

## How to test it in the game

There are no saves yet on the development machine, so the first save has to be made by hand. A throwaway mod or a script (Script Runner, `oben run`) is enough:

```csharp
// registers in OnLoad of a test mod
var save = ModSaves.Register<Counter>(this);   // class Counter { public int N; }
```

1. Start a new game, wait for the world. Set `save.Value.N = 5` (`oben eval` or a script).
2. Save to a slot from the pause menu. Find `Saves/<Character>/Slot1/mods/<modId>.json` under the game's `persistentDataPath` (Wine: `.../AppData/LocalLow/Loiste Interactive/Obenseuer/`). It should contain `"N": 5`. `Info.tnmt` and the other game files must be unchanged in format.
3. Change `N` to 9, walk through a door. This autosaves and reloads: `N` is 9 afterwards and `Autosave/mods/<modId>.json` has 9.
4. Quit to the main menu. `N` is 0 (defaults) and `DataLoaded` fired.
5. Load Slot1 from the menu. `N` is 5.
6. Save from there to Slot2. `Slot2/mods/<modId>.json` exists with 5 (a cross-slot save, which deletes and recreates the folder).
7. Delete Slot1 from the load menu; `Slot2` still has its file.
8. Hot reload the test mod with a changed `N` and no save in between: the value is kept. Load a save and reload again: the saved value wins.
9. Corruption: damage the JSON, load that slot. A `.corrupt` copy appears, the log has an error, `N` is 0.
