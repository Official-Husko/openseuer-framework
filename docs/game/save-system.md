# Obenseuer save system

Reference notes for the mod framework. Everything here comes from reading the decompiled code, not from running it. Unity 6000.0.77f1, Mono, game assembly `Assembly-CSharp`.

Conventions:
- Paths like `SaveController.cs:475` are relative to `decompiled/Assembly-CSharp/`.
- FullSerializer lives in `decompiled/Assembly-CSharp-firstpass/FullSerializer*/` (one copy of a few attribute types, e.g. `fsCheckChildFields`, is in `Assembly-CSharp/FullSerializer/`).
- "Level" means a Unity scene (`SceneManager.GetActiveScene().name`), e.g. `Interior Player Tenement`.

There are no real saves on this machine. The Wine prefix only has `Controls.json`, `Display.json`, `Quality.json`, `Volume.json` and the player logs, and `Player.log` shows the first-run `DirectoryNotFoundException` from `SaveController.GetCharacters` because `Saves/` does not exist yet. So the on-disk layout below is derived from code. Re-check it against a real save once one exists.

## 1. Overview

- One class, `SaveController` (MonoBehaviour singleton, `DontDestroyOnLoad`), does everything. `SaveController.cs` is 2216 lines.
- A save is a folder of JSON files. Objects save themselves: each save-aware component derives from `SavableScript`, has a public string `GUID` field, and calls `SaveController.SerializeData(this)` in its save override and `SaveController.DeSerializeData(this)` in its load override.
- Serialization is FullSerializer with a custom fast path (`FsStreamWriter`, `FsStreamReader`) that produces/consumes the same JSON.
- Loading does not re-create objects. It deserializes a detached copy of each saved component and copies fields from the copy onto the live scene object (`ReplaceProperties`).
- A level transition is a real save followed by a real load of the same autosave (section 5.3). That matters for anything stored on scene objects.

## 2. On-disk format

### 2.1 Location and layout

Root: `Application.persistentDataPath`, which under Wine is
`.../drive_c/users/steamuser/AppData/LocalLow/Loiste Interactive/Obenseuer/`.

```
Saves/
  <CharacterFolder>/            Esko_Virtanen  (SanitizeCharacterName)
    Autosave/
    Autosave2/
    Slot1/ ... Slot10/
      Info.tnmt                 header only, Data list empty
      Globals.tnmt              global objects, plus the destination level name
      <LevelName>.tnmt          one per visited level, level objects
      Globals.dialogue          Dialogue System (Lua) state, plain text
      <info_map render texture name>.png   map reveal image (RevealCamera)
```

| Item | Detail | Source |
|---|---|---|
| Character folder | `Regex.Replace(first + "_" + last, "[^\w]", "")`, falls back to `Esko_Virtanen` if empty. Two characters with the same name share a folder. | `SaveController.cs:567` |
| Save folder name | `Regex.Replace(savename, "[^\w]", "")`; empty means `Autosave`. Used names: `Autosave`, `Autosave2`, `Slot1`..`Slot10`. | `SaveController.cs:495` |
| `Info.tnmt` | `SaveDataHeader` with an empty `Data` list. | `SaveController.cs:520` |
| `Globals.tnmt` | `SaveDataHeader` holding all global `ObjectDataHeader`s. | `SaveController.cs:521` |
| `<Level>.tnmt` | `SaveDataHeader` holding the level objects of that scene. File name is the scene name, spaces included. | `SaveController.cs:522` |
| `Globals.dialogue` | `PersistentDataManager.GetSaveData()` text (Lua chunk), UTF-8 with BOM. | `SaveController.cs:543` |
| Map PNG | Written by `RevealCamera.OnSavingFile` into `GetCurrentSavePath()`, on a background `Task`. | `RevealCamera.cs:20` |

### 2.2 File encoding

- No compression, no encryption. Plain minified JSON in UTF-8 **with a BOM** (`EF BB BF`).
- Fast path: `FsStreamWriter.WriteTo` writes the BOM itself (`FsStreamWriter.cs:390`). Fallback path: `File.WriteAllText(path, text, Encoding.UTF8)`, also BOM (`SaveController.cs:664`). Reading is `File.ReadAllText(path, UTF8)` (`:701`), which strips the BOM.
- Standard FullSerializer envelope: `$type` (runtime `Type.FullName`, no assembly), `$id` / `$ref` for shared or cyclic objects, `$content` when a non-object value needs a wrapper. `DateTime` is written as round-trip `"o"` text.
- Expected shape of a `.tnmt` (reconstructed, member order not guaranteed):

```
{"SaveName":"Autosave","LevelName":"...","NewlevelEntrypoint":"NONE","CharacterName":"Esko_Virtanen",
 "Date":"2026-10-28T12:00:00.0000000+01:00","ApplicationVersion":"x.y.z",
 "Data":[{"GUID":"Money","Data":{"$type":"Money", ...fields...}}, ...]}
```

- `ObjectDataHeader.Data` is declared `UnityEngine.Object`, so every entry carries a `$type`. That is what ties a save to concrete class names (section 7).
- `LevelName` is not the same in all three files. `Info.tnmt` and `Globals.tnmt` carry the **destination** level (the `newLevel` argument), while `<Level>.tnmt` carries the level it describes (`SaveController.cs:520-522`). Load uses `Globals.tnmt`'s `LevelName` to decide which scene to open.

### 2.3 Other persisted files (not part of saves)

| File | Notes |
|---|---|
| `Display.json`, `Quality.json`, `Controls.json`, `KeyBindings.json` | UTF-8 JSON in `persistentDataPath` (`DisplaySave.cs`, `QualitySave.cs`, `ControlsSave.cs`, `KeyBindingsSave.cs`). |
| `Volume.json` | UTF-16 LE with BOM on this machine (`VolumeSave.cs:93`). Different from the others, keep in mind if you parse it. |
| `CustomImages/<folder>/` | Player-imported images (`CustomImagePicker.cs`). Not tied to a save. |

## 3. Data types

| Type | Visibility | Members | Source |
|---|---|---|---|
| `SaveController.SaveDataHeader` | `internal` nested | `string SaveName, LevelName, NewlevelEntrypoint, CharacterName; DateTime Date; string ApplicationVersion; List<ObjectDataHeader> Data`. Ctor trims `CharacterName`, stamps `Date = DateTime.Now` and `ApplicationVersion = Application.version`. | `:97` |
| `SaveController.ObjectDataHeader` | `internal` nested | `string GUID; UnityEngine.Object Data` | `:83` |
| `SaveController.SaveInfo` | public nested | `SaveName, LevelName, NewlevelEntrypoint, CharacterName (trimmed on set), Date (string), ApplicationVersion, DateDisplayString` | `:154` |
| `SaveController.CharacterSaves` | public nested | `CharacterName, FolderName (full path), LatestDate, bool[] Slots, bool Autosave` | `:126` |

`SaveDataHeader` and `ObjectDataHeader` are `internal`, so a mod needs reflection or a publicized reference to touch them. `SaveInfo` and `CharacterSaves` are public.

### 3.1 GetSavedGameInfo and GetCharacters

```csharp
public static SaveInfo GetSavedGameInfo(string savePath, out Exception exception)   // :755
public static List<CharacterSaves> GetCharacters()                                 // :1236
```

- `GetSavedGameInfo(folder)` reads `<folder>/Info.tnmt` only, deserializes it as a full `SaveDataHeader`, and copies the metadata. Missing file gives an empty `SaveInfo` (`SaveName == null`, shown as "Empty slot"). A failed deserialize sets `exception` (shown as "Corrupted"). `SaveInfo.Date` is `DateTime.ToString(InvariantCulture)`.
- `GetCharacters()` lists every directory under `Saves/` (throws if `Saves/` is missing; the caller catches it). For each it checks `Slot10..Slot1/Info.tnmt` and `Autosave/Info.tnmt`, takes `CharacterName` from the first header it manages to read, and drops directories with no readable header. `Autosave2` is **not** considered here. A character that only has `Autosave2` will not appear. Duplicate display names get a numeric suffix.
- `LoadMenu` warns when `SaveInfo.ApplicationVersion != Application.version` (`LoadMenu.cs:525`) and when the folder name does not match the saved character name (`FolderMatchesCharacterName`, `SaveController.cs:583`). Do not rename save folders.

## 4. Autosave rotation

- Two autosave folders, `Autosave` and `Autosave2`. `GetOldestAutosave()` (`:342`, private static) reads both `Info.tnmt` dates and returns `"Autosave2"` only if `Autosave` is newer, otherwise `"Autosave"`. With nothing on disk it returns `"Autosave"`. So the saves alternate.
- `Autosave(MonoBehaviour)` (`:458`) starts `AutosaveDelay()` (`:463`): show the full-screen saving UI, wait two frames and `WaitForEndOfFrame`, call `SaveGame(GetOldestAutosave())`, wait again, hide the UI. Triggers: `TriggerAutosave` (collider exit), `logic_autosave` (map logic), `SaveController.ChangeLevel` and `ChangeLevelSame`.
- Manual saves go through `LoadMenu.Save()` which calls `SaveController.SaveGame(slotName)` (`LoadMenu.cs:566`).
- `AutosaveMode` (Default / EveryDay / EveryHour) is a difficulty option only; nothing in `SaveController` reads it.
- Deleting: `RemoveCharacter(string savePath)` (`:1312`) is `Directory.Delete(recursive)`. The menu uses it for single slots too (`LoadMenu.cs:690`). Mod files inside a save folder go with it.

## 5. SaveController internals

### 5.1 Statics and events

| Member | Notes |
|---|---|
| `public static SaveController instance` | Set in `Awake`; duplicates are destroyed (`:292`). `SaveGame` creates it if missing. |
| `public static bool Loading { get; private set; }` | Not a reliable "load in progress" flag, see 5.6. |
| `public static string LastSaveFolderPath` | Full path of the folder most recently **loaded**. Set in `LoadGameWithMigration` (`:1546`). `PauseMenu.QuitMainMenu` sets it to `""` (`PauseMenu.cs:448`). Not updated by saves. |
| `public static List<GameObject> DontDestroyOnLoadObjects` | Extra roots that receive the `*Special` and `OnMapChanging` callbacks (`:1517`). Only `Achievements` and `NPCDirector` use it. |
| `internal static readonly fsSerializer JSON_serializer` | The save serializer, with `SavedAssetConverter` added (`:285`). Default `fsConfig`. |
| private `LevelName`, `SaveName`, `CharacterName` | Current save context. `GetCurrentSavePath()` builds from them. |
| private `tempSavedata_Global`, `tempSavedata_Level` | The in-memory object lists for the save or load in progress. |
| private `Guids` (`Dictionary<string, UnityEngine.Object>`) | GUID to live object, filled only by successful `DeSerializeData`, cleared at the start of each load (`:779`). |

Events (all `public static event`, delegates are `public delegate void OnXxx()` nested in `SaveController`):

| Event | Raised from | When |
|---|---|---|
| `PlayerWillChangeLevel` | `ChangeLevelWhenReady`, `:329` | Via `ChangeLevel` only. `Loading` is already `true`. Before `OnMapChanging` and before the save. |
| `PlayerWillLoadGame` | `LoadGameWithMigration`, `:1531` | Start of every load, including the reload that follows a level change. Wrapped in `try { } catch { }`. Fires before `LastSaveFolderPath` is updated. |
| `SavingStarted` | `SaveGame`, `:512` | After `tempSavedata_*` are allocated, before the first `OnSavingGamePrimary`. |
| `SavingDone` | `SaveGame`, `:545` | After all files, including `Globals.dialogue` and the `OnSavingFile` callbacks, are written. |
| `LoadingStarted` | `LoadSaveGameDifferentScene`, `:798` | Target scene is loaded, before any `OnLoading*` callback. |
| `LoadingDone` | `LoadSaveGameDifferentScene`, `:821` | After the last callback and the Dialogue System restore, right after `Loading = false`. |

None of these are invoked inside try/catch (except `PlayerWillLoadGame`'s swallow-all). Exception behaviour is in section 9.

### 5.2 SaveGame

```csharp
public static void SaveGame(string savename = "", string newLevel = "", string newLevelEntrypoint = "NONE")  // :475
```

Synchronous, main thread, no try/catch around the whole thing. Order:

1. `RevealCamera.WaitForPendingMapWrite()` (blocks on the previous background PNG write).
2. Show saving UI, ensure `instance` exists, `SaveName` = sanitized `savename` or `"Autosave"`, `GetCharacterName()` from `PlayerIdentity.identity`, `LevelName` = active scene.
3. If `newLevel != ""` (a level change): run `OnMapChanging` on the scene and on `DontDestroyOnLoadObjects`.
4. Allocate `tempSavedata_Global` / `tempSavedata_Level`, raise `SavingStarted`.
5. Callback phases in order (see 5.5): Primary, Secondary, Tertiary, `DestructibleList.OnSavingGameDestructibleList()` (called directly on the singleton, not through the scan), Special (DontDestroyOnLoad list), `OnSavingGame`, `OnSavingGameLatePrimary`.
6. Serialize three headers to memory (`SerializeSaveFile`, `:684`): Info (empty Data), Globals (`newLevel`), Level (`LevelName`).
7. Folder handling, where `text` is `<persistentDataPath>/Saves/<Character>/<SaveName>`:
   - `IsSameFolder(LastSaveFolderPath, text)` true: write in place. Nothing is deleted, other files stay.
   - Otherwise: `Directory.Delete(text, recursive: true)` if it exists, create it, then if `LastSaveFolderPath` is non-empty `CopyDirectory(LastSaveFolderPath, text, skip: Info.tnmt, Globals.tnmt, <LevelName>.tnmt, Globals.dialogue)` (`:527-538`). `CopyDirectory` copies every other file and subfolder with `Parallel.ForEach` (up to 8 threads) but blocks until done.
8. Write `Info.tnmt`, `Globals.tnmt`, `<Level>.tnmt`, `Globals.dialogue`.
9. `OnSavingFile` callbacks, then `SavingDone`, hide UI.

Consequences:
- The level file and `Globals.tnmt` are **rebuilt from scratch** every save from whatever objects were active and serialized this time. Nothing is merged with the old file. Only the level being left is rewritten; other levels' `.tnmt` come from the copy.
- A global object only survives if it exists and is active in the scene at every save (see 8.4).
- Any extra file you put in a save folder is deleted if that folder is the destination of a save from a different last-loaded folder. It is carried forward only via the copy from `LastSaveFolderPath`.

### 5.3 ChangeLevel

```csharp
public static void ChangeLevel(string newlevel, string newLevelEntrypoint = "NONE")   // :307
```

Called by `Changelevel`, `ChangeScene`, `BlackoutTrigger`, `Crime`, `SleepEventController`, and the dev console. With a live `instance` it starts `ChangeLevelWhenReady` (`:322`): wait while `Loading`, set `Loading = true`, raise `PlayerWillChangeLevel`, then:

- empty `newlevel`: `ChangeLevelSame` (`:421`): fade, `SaveGame(GetOldestAutosave(), "", "")`, fade back. Save only, no reload.
- otherwise `ChangeLevelDelay` (`:403`): fade, wait for the fade, `SaveGame(oldest, newlevel, entrypoint)`, then `StartCoroutine(LoadGameWithMigration(CharacterName, oldest))`.

So moving through a door is: `PlayerWillChangeLevel`, `OnMapChanging`, `SavingStarted`, files written, `SavingDone`, `PlayerWillLoadGame`, scene load, `LoadingStarted`, load callbacks, `LoadingDone`. Every transition overwrites an autosave and fires all the save and load events. Mods that care about "a real load from the menu" must tell the two apart (e.g. set a flag in `PlayerWillChangeLevel`, clear it in `LoadingDone`).

Anything that lives only on scene objects and is not saved is reset on every level change.

### 5.4 Load

```csharp
public static IEnumerator LoadGameWithMigration(string characterName = "", string saveName = "", bool fromMenu = false)  // :1522
```

`characterName` is either a sanitized name (from `ChangeLevel`) or a full folder path (from `LoadMenu`, which passes `CharacterSaves.FolderName`). Steps:

1. `RevealCamera.WaitForPendingMapWrite()`, store `CharacterName`/`SaveName`, raise `PlayerWillLoadGame`, `Loading = true`, new empty `tempSavedata_*`.
2. Resolve `savePath`; require `Globals.tnmt`. Set `LastSaveFolderPath = savePath`. If missing: log "Savegame not found", `Loading = false`, fade the loading screen out.
3. Deserialize `Globals.tnmt` (`Deserialize`, section 7). Outcomes:
   - success: continue;
   - errors but result usable and `skippedFieldsOnly` ("loaded with skipped fields"): continue without migration;
   - `InsufficientExecutionStackException`: abort the load;
   - other errors: run `Migrate(...)` (below), abort only if that yields no header.
4. `MigrationController.RunMigrations(ref header)` is an empty stub (`MigrationController.cs`); `ISavegameMigration` is unused. `tempSavedata_Global = header.Data`.
5. Read `<header.LevelName>.tnmt` if present, same error handling; on failure the level is reset and a "Save data corrupted" notification is shown. `tempSavedata_Level` is its `Data`, or an empty list for a first visit.
6. Read `Globals.dialogue`. For saves older than 0.3.40 it rewrites actor names (`:1666-1678`).
7. `PersistentDataManager.LevelWillBeUnloaded()`, then `StartCoroutine(LoadSaveGameDifferentScene(level, fromMenu, dialogueText, entrypoint))`. This is used even if the target is the current scene, so a same-level load still reloads the scene.
8. `Loading = false` immediately after starting that coroutine (`:1732`).

`Migrate` (`:1760`): only runs when a normal deserialize reported a hard error. Sets `Application.runInBackground = true`, parses the JSON to `fsData`, on a `Task.Run` thread resolves every `$ref` into a deep copy, caps the result at 25,000,000 values, prints it back to text, and deserializes again. It can repair a `#:{` corruption pattern. It shows a migration progress UI and is slow on big saves.

### 5.5 Callback phases

`SavableScript` (`SavableScript.cs`) declares 18 `public virtual bool OnXxx()` methods that all return `true` by default. `SaveController` runs them with `ExecuteSaveLoadFunctions(GameObject[], LoadSaveType, bool includeInactive)` (`:1430`): for each root object of the active scene it calls `GetComponentsInChildren(typeof(SavableScript), includeInactive)` and invokes the method on each component whose `gameObject.activeInHierarchy` is true. Each call is wrapped in its own try/catch that logs and continues. The return value is ignored. The `includeInactive` argument has no effect in practice, because the `activeInHierarchy` check applies afterwards.

Order when saving, then loading (target scene):

| # | Save | Load |
|---|---|---|
| 0 | `OnMapChanging` (level change only; scene and DDOL list) | `LoadingStarted` event |
| 1 | `OnSavingGamePrimary` | `OnLoadingGamePrimary` |
| 2 | `OnSavingGameSecondary` | `OnLoadingGameSecondary` |
| 3 | `OnSavingGameTertiary` | `OnLoadingGameTertiary` |
| 4 | `DestructibleList.OnSavingGameDestructibleList()` | `OnLoadingGameSpecial` (DDOL list only) |
| 5 | `OnSavingGameSpecial` (DDOL list only) | `DestructibleList.OnLoadingGameDestructibleList()` (spawns saved prefabs) |
| 6 | `OnSavingGame` | `OnLoadingGameDestructibleListCheck` (default impl destroys objects whose GUID is in `DestroyedGuids`) |
| 7 | `OnSavingGameLatePrimary` | one frame (`yield return null`) |
| 8 | files written, `OnSavingFile` | `OnLoadingGame`, then `OnLoadingGameLatePrimary`, then `OnLoadingGameDestructibleListCheck` again |
| 9 | `SavingDone` | `OnMapChanged` (only if the level changed or an entrypoint was given), player moved to entrypoint 0.1 s later, Dialogue System restore, `Loading = false`, `LoadingDone`, fade in |

`OnSavingGameDestructibleList` and `OnLoadingGameDestructibleList` in `SavableScript` are not called by the scan at all; only the `DestructibleList` singleton's override is invoked, directly.

Usage in the game (count of classes overriding each): `OnSavingGame` 158, `OnLoadingGame` 151, `OnLoadingGameLatePrimary` 12, `OnMapChanged` 7, `OnLoadingGamePrimary` 6, `OnMapChanging` 5, the rest 1 to 3. Practically all ordinary objects use `OnSavingGame` / `OnLoadingGame`. Primary to Tertiary are for managers that other objects depend on (e.g. `FurnitureManager.OnLoadingGameSecondary` restores placed furniture before `OnLoadingGame` runs).

### 5.6 The Loading flag

`Loading` is `true` only during `ChangeLevelWhenReady` until the reload starts, and during the file-reading and migration part of `LoadGameWithMigration`. It is set back to `false` at `:1732` as soon as the scene-load coroutine has started and hit its first yield, i.e. **before the scene has actually loaded** and before any `OnLoading*` callback. The coroutine sets it to `false` again at `:820`. Vanilla code that polls it (`AnimalController`, `NPCInfo`, `TimeOfDayAzure`, `Inventory`, ...) mostly uses it to wait out the migration. For "is the world restored yet", pair `LoadingStarted` with `LoadingDone` yourself.

## 6. GUIDs

### 6.1 How an object gets its save id

- The contract is a **public** field named `GUID` of type `string` on the runtime type (public, instance or static/const). `SerializeData`, `DeSerializeData`, `GetObjectGUID` and `SetObjectGUID` all do `data.GetType().GetField("GUID")` (default flags: public, instance + static). A private, protected or property `GUID` is silently ignored and `SerializeData` just returns `false`.
- The value must be non-empty. An empty GUID is treated as "not saveable".
- Three sources in vanilla code:
  1. Scene objects: a string baked into the scene by the level designers (`Door`, `Breakable`, `Button`, ...). The decompile shows only the field, `[fsIgnore] public string GUID;`. The values live in the scene assets, not in code.
  2. Singletons: a constant, e.g. `public const string GUID = "Money";` (`Money.cs:152`), `"Crime"`, `"BuildingSystem"`, `"DestructibleList"`, `"Relationships"`. Statics work too (`AnimalController.GUID`).
  3. Runtime objects: assigned in code from a parent (`FurnitureManager`: `GUID + index`; `BuildingSystem`: `"BuildingSystem" + n`; `GrowingSpot`: `GUID + "_plant"`; `InstantiatedPrefabRef`: `guid + "storage"`).
- Uniqueness is per list: global entries and level entries are separate lists, but `HasSaveData(guid)` checks both. Keep GUIDs unique across everything.

### 6.2 Sub-GUIDs

`InstantiatedPrefabRef.GenerateSubGUIDS(...)` and `SetSubGUIDs(...)` (`InstantiatedPrefabRef.cs:88-93`) give GUIDs to every child component that has a public string `GUID` when a prefab is spawned at runtime (placed furniture, apartment prefabs):

```
<originalGUID>_<iter>_<TypeName>_<index>        index = per-type counter within the prefab
```

On load (`loadingSave: true`) it first checks `SaveController.HasSaveData(thatGuid)`. If no such entry exists it asks `SaveController.GetOldSubGUIDs(originalGUID, iter)` for saved GUIDs that match `<originalGUID>_<iter>_<number>` (the older numeric-suffix scheme, detected by `GetOldSubGUIDPrefix`, `:931`) grouped by type, and reuses them in order. The same method also sets `global` on children that have a `bool global` field.

### 6.3 Lookup and replace

`DeSerializeData(UnityEngine.Object data, bool global = false)` (`:865`):

1. Read the GUID as above.
2. Pick `tempSavedata_Global` if `global`, otherwise `tempSavedata_Level`.
3. `UpdateLookup` (`:895`) builds, once per list instance, `Dictionary<string, Queue<int>>` from GUID to list positions (duplicates queue up in file order).
4. `ReplaceWithSavedata` (`:1003`) takes the first live position, calls `ReplaceProperties(live, saved)`, records `Guids[guid] = live`, and **nulls the list slot**. A second call for the same GUID gets the next duplicate or `false`.

Returns `true` if saved data was applied. `false` means "no data for this GUID" (first visit, new object), and the component should leave its scene defaults alone.

`GetObjectByGUID(string guid)` (`:1360`) only knows objects restored by `DeSerializeData` during the current load. `GetObjectByGUID(string guid, Type typ)` (`:1330`) scans `FindObjectsOfType(typ)` with reflection, which is slow. `SavableScript.SavableObjectReference` (`SavableScript.cs:8`) is a `[fsProperty] string GUID` wrapper that resolves lazily through the second form and caches the result.

### 6.4 DestructibleList

`DestructibleList` (`DestructibleList.cs`, singleton, GUID `"DestructibleList"`) is saved as level data. It holds:
- `DestroyedGuids`: GUIDs of scene objects the player removed. At load every `SavableScript` whose `GUID` is in the list is `Destroy`ed by the default `OnLoadingGameDestructibleListCheck`.
- `prefabs` (`[fsProperty] List<InstantiatedPrefab>`): runtime-spawned items. At load each is re-created with `Resources.Load(prefab.PrefabPath)`. A prefab not loadable through `Resources.Load` (for example one that comes from a mod asset bundle) logs "Not found!" and is dropped.

## 7. Serialization rules

### 7.1 What is written

The serializer is `new fsSerializer()` with the default `fsConfig` (`fsConfig.cs`) plus `SavedAssetConverter` (`SaveController.cs:285`). Member selection comes from `fsMetaType.CollectProperties` (`fsMetaType.cs:108`), walking the type and all base types:

- Serialized: public instance fields; any field with `[SerializeField]` or `[fsProperty]`; public auto-properties with a getter and a setter.
- Not serialized: anything with `[NonSerialized]` or `[fsIgnore]`, static fields, delegates, compiler-generated fields, indexers, and members whose type is marked `[fsIgnore]`.
- No converter for `UnityEngine.Object` is registered, so a field typed `Transform`, `GameObject`, `Sprite` etc. would be walked by reflection. The game marks all of these `[fsIgnore]` (compare `Door.cs`: every inspector reference carries `[fsIgnore]`). Do the same.
- Built-in converters cover primitives, enums (by name), arrays, `List<>`, dictionaries, `KeyValuePair`, `Guid`, `Type`, `DateTime`, and Unity direct converters for `AnimationCurve`, `Bounds`, `Gradient`, `Keyframe`, `LayerMask`, `Rect`, `RectOffset`. `Vector3`, `Quaternion`, `Color` go through the reflected converter (public fields).
- Shipped `ScriptableObject` types in `SavedAssets.assetTypes` (`TaskInfo`, `TaskObjective`, `NoteInfo`, `BookInfo`, `Furniture`, `TenementUpgrade`, `TenementUpgradeGroup`, `TenementContractor`, `Landmark`) are re-linked on load to the Addressables asset with the same `addressableAssetPath` instead of being duplicated (`SavedAssetConverter.cs`, `SavedAssets.cs`). A new asset of one of these types in a mod needs a resolvable Addressables path or it comes back as a fresh instance.

### 7.2 What ReplaceProperties copies back

`ReplaceProperties(UnityEngine.Object original, UnityEngine.Object replacement)` (`:1028`) is where "serialized" and "restored" diverge. For the runtime type it walks `GetFields(Instance | Public | NonPublic)` (cached per type in `replaceFields`) and uses `GetFieldRule` (`:1074`, cached per `FieldInfo` in `fieldRules`):

| Field | Copied to the live object? |
|---|---|
| Not private (public, protected, internal), no attribute | yes |
| Not private, `[fsIgnore]` | no |
| Private, `[fsProperty]` | yes |
| Private, `[SerializeField]` only | **no** (written to the file, never restored) |
| Private, no attribute | no |
| `[fsCheckChildFields]` (on a nested class, struct, or array of them) | children copied one by one, the container object is kept |
| Private field declared in a **base** class | no (`GetFields` on the derived type does not return base privates), even with `[fsProperty]` |
| Properties | never, only fields |

- `type != replacement.GetType()` returns without doing anything.
- A field that is copied is copied **even when the JSON had no value for it**. The replacement object is created with the type's default constructor, so absent members hold their initializer or `default`. A field that you add in a later version of a mod component, if it is public and not `[fsIgnore]`, will be overwritten with the constructor default when an older save is loaded, not left at the inspector value.
- The replacement is made with `Activator.CreateInstance(type, nonPublic: true)` (`fsMetaType.CreateInstance`, `fsMetaType.cs:248`). For a `MonoBehaviour` this is a plain `new`: the constructor and field initializers run, `Awake`/`OnEnable`/`Start` do not. Keep constructors free of side effects (no registering `this` in static lists).
- `[fsCheckChildFields]` on a `List<T>` does nothing useful; it is for nested objects and arrays (`SetArrayField` matches elements by index).
- `[fsCheckChildFields]` exists to avoid replacing a nested object reference that other code or Unity-serialized state already holds. `Door._Lock` is the typical use.

### 7.3 Fast path versus FullSerializer

- Writing (`FsStreamWriter.TrySerialize`, `FsStreamWriter.cs:308`): builds compiled per-type plans and writes the JSON bytes directly. Per file, if any type in the graph is unsupported (a custom converter, a processor other than `ISerializationCallbackReceiver`, a versioned type, multi-dimensional arrays, `Stack<>`, duplicate or null dictionary key names) it logs a warning and the whole file is written with `fsSerializer` + `fsJsonPrinter.CompressedJson` instead. Same JSON either way.
- Reading (`FsStreamReader.TryDeserialize`, `FsStreamReader.cs:193`): same idea, falls back to `fsJsonParser.Parse` + `SavedAssets.CollapseGraphs` + `JSON_serializer.TryDeserialize` (`SaveController.Deserialize`, `:1375`).
- If a compiled accessor throws `InvalidProgramException`, `FieldAccessException`, `MethodAccessException`, `TypeLoadException` or `VerificationException`, codegen is disabled globally and plans are rebuilt with reflection.
- `Deserialize` returns the object **and** an `Exception` for any error or warning, via `fsResult.AssertSuccessWithoutWarnings`. `skippedFieldsOnly` is true when the result still `Succeeded` and is non-null (only warnings, such as "Ignoring type specifier"). That distinction decides whether `LoadGameWithMigration` migrates.

### 7.4 Unknown data

- A JSON member that the current class does not have is ignored without any warning (`fsReflectedConverter.TryDeserialize` iterates the class's members, not the JSON keys; the fast reader does the same in `ReadReflected`).
- A `$type` that cannot be found fails with `Unable to locate specified type "<name>"` (`fsSerializer.cs:680`). `fsTypeCache` searches every loaded assembly by `FullName`, so a mod's types resolve when the mod assembly is loaded. The failed list element is skipped (not replaced with null) and the overall result is `Failed`. That sends `LoadGameWithMigration` through `Migrate` (slow path, section 5.4), after which the load continues with the surviving entries. Inference from the code: removing a mod that put its own component types into the save costs a migration pass on every load until the next save drops them, and the mod's data is lost.
- A `$type` that exists but is not assignable to the declared type is ignored with a warning, and the declared type is used.
- Renaming a class or moving it to another namespace changes `FullName` and orphans old entries the same way.

## 8. Which components save themselves

### 8.1 Opt-in checklist

A component takes part in saves only if all of these hold:

1. It derives (directly or indirectly) from `SavableScript`. The scan is `GetComponentsInChildren(typeof(SavableScript))`; there is no interface or attribute route.
2. It lives under a **root object of the active scene** that is active in the hierarchy, or its root is in `SaveController.DontDestroyOnLoadObjects` (then only the `OnMapChanging`, `OnSavingGameSpecial`, `OnLoadingGameSpecial` phases reach it; root objects only).
3. It has a public `GUID` string (non-empty) as in section 6.
4. It overrides a save phase to call `SerializeData`, and the matching load phase to call `DeSerializeData`, and applies state afterwards.
5. Its fields follow the rules in 7.1 and 7.2.

The standard pattern (from `Door.cs:243`):

```csharp
public override bool OnSavingGame()  { saved = true; /* capture transform etc. */ return SaveController.SerializeData(this); }
public override bool OnLoadingGame() { Start(); if (SaveController.DeSerializeData(this)) { /* apply fields */ return true; } return false; }
```

Notes on the pattern:
- Capture derived state (positions, "saved" flags) into serialized fields **before** `SerializeData`, and apply it after `DeSerializeData`. The object is serialized later, in step 6 of `SaveGame`, from the live component, so late changes before that point are also captured.
- `Door.OnLoadingGame` calls `Start()` itself. The `OnLoading*` phases (Primary through Tertiary and the first parts of the load) can run before Unity has called `Start()` on scene objects, because only one frame passes between the destructible-list step and `OnLoadingGame`. Do not assume `Start` has run.
- Pass `global: true` for data that must survive level changes (`Money`, `BuildingSystem`, `AnimalController`, ...). About 92 call sites use `global`. Some types decide at runtime with a `bool global` field (`FurniturePlaceable`, `NPCInfo`, `CraftingBase`, `RandomAudioPlayer`).
- Many managers use several phases, e.g. `BuildingSystem` saves in `OnSavingGameSecondary`, `FurnitureManager` loads in `OnLoadingGameSecondary`.

### 8.2 Examples in the code

| Class | Pattern | File |
|---|---|---|
| `Door` | level data, private fields via `[fsProperty]`, `[fsCheckChildFields] _Lock` | `Door.cs` |
| `Money` | global singleton, `const GUID`, `[SerializeField][fsProperty] private int rMoneyAmount` | `Money.cs:140-230` |
| `DestructibleList` | level singleton plus runtime prefab list | `DestructibleList.cs` |
| `RevealCamera` | uses `OnSavingFile` to write a side file and `OnLoadingGame` to read it back | `RevealCamera.cs` |
| `TriggerAutosave` | smallest example | `TriggerAutosave.cs` |
| `Achievements`, `NPCDirector` | register in `DontDestroyOnLoadObjects` | `Achievements.cs:350`, `NPC/NPCDirector.cs:143` |

### 8.3 Spawned objects

Objects created at runtime are only restored if something recreates them. The built-in mechanisms are `DestructibleList.SpawnItem(GameObject, string prefabfolder = "Prefabs/Items/")` (records a `Resources` path) and the furniture systems (`BuildingSystem.FurnitureInfo.SavePath/LoadFromPath`). A mod that spawns its own objects needs its own recreate-on-load logic.

### 8.4 Visibility rules that drop data

- Only objects active in the hierarchy during the save callbacks are serialized. An object that is disabled when the save happens is absent from the new level file, so its previously saved state is gone after that save.
- `Globals.tnmt` is rebuilt every save. A global object that is not present in the current scene at save time loses its data. Global data therefore needs a host that exists in every scene (a singleton in a DontDestroyOnLoad root registered in `DontDestroyOnLoadObjects`, using the `Special` phases).
- `SerializeData` / `DeSerializeData` only work between the start of a save or load and its end. After `LoadingDone` the load lists are cleared (`:818`), so a late `DeSerializeData` returns `false`.

## 9. Mod data alongside a save

Three options, with what can go wrong.

### 9.1 Side files in the save folder (recommended default)

- Folder: `SaveController.instance.GetCurrentSavePath()` (`:1227`) is `<persistentDataPath>/Saves/<Character>/<SaveName>`, using the static `CharacterName`/`SaveName`. It handles the case where `CharacterName` is a full path (after a menu load). `RevealCamera` already stores a PNG there.
- **Write** in `SavingDone` (or a `SaveGame` postfix). By then the game has already deleted/recreated the folder and copied the old contents, and nothing else will clean up. Writing earlier (`SavingStarted`) loses the file whenever the destination differs from `LastSaveFolderPath`, because the folder is deleted at step 7.
- **Read** from `SaveController.LastSaveFolderPath` in `LoadingStarted` (or in `LoadingDone`). It points at the folder that was just loaded, including after a level change reload. Treat `null` or `""` as "no mod data yet" (new game, or after returning to the main menu).
- The copy step carries files forward from the last-loaded folder to the next destination, so a mod file in `Autosave/` appears in `Slot3/` after saving to Slot3 and then gets overwritten by your `SavingDone` write. Name per-level files with the level name if you want the same layout the game uses.
- Names to avoid: `Info.tnmt`, `Globals.tnmt`, `<LevelName>.tnmt`, `Globals.dialogue`, and the map PNG. The first four are overwritten each save; the PNG is written asynchronously.
- Do not create anything under `Saves/` itself: every directory there is probed as a character folder by `GetCharacters`. Do not create folders named `Slot*` or `Autosave*` inside the character folder.
- The map PNG is written by a background `Task` after `SavingDone`. Do not enumerate or copy the whole save folder in `SavingDone` as if it were final. Call `RevealCamera.WaitForPendingMapWrite()` first if you need that (it is public static).
- Writes are not atomic in the game. Write to a temp name in the same folder then `File.Move` your own files, so a crash leaves the previous version.
- Use a namespaced name (`<modid>.json` or `<modid>/` subfolder) and a version field inside it.
- Because the folder is deleted on a cross-slot save, always write *all* of your state in `SavingDone` rather than only changes.

### 9.2 Own SavableScript types inside the .tnmt

Doing this makes the mod's data part of the game's own files and gets per-object GUID matching for free, but:
- The save then names mod classes in `$type`. Without the mod loaded the load goes through `Migrate` (7.4) and drops those entries.
- The component has to exist in the scene (or DDOL list) at every save and be active (8.4), and its phase methods must never throw (they are caught per component but the rest of that component's phase is skipped).
- Constructor side effects run on the throwaway copy.
- Prefer one framework-owned singleton with a stable `const string GUID` and a `Dictionary<string,string>` payload, hosted on a DDOL root added to `DontDestroyOnLoadObjects`, over many small mod components. The type and namespace must never change once released.

### 9.3 Dialogue System text

`Globals.dialogue` is the Pixel Crushers Dialogue System state, applied after all object callbacks (`PersistentDataManager.ApplySaveData`, `:816`). Two hooks exist that would carry small mod strings without adding types to the `.tnmt` files:
- `DialogueLua.SetVariable(string, object)` / `GetVariable(...)`: variables are written into the `Variable={...}` table by `AppendVariableData` (`PersistentDataManager.cs`) and come back through the Lua restore.
- `PersistentDataManager.GetCustomSaveData` (`public static GetCustomSaveDataDelegate`, returns `string`), appended verbatim to the dialogue text.

This is untested here. It is also lossy for large payloads and interacts with the Dialogue System database reset, so treat it as a fallback for tiny flags.

## 10. Safe moments to touch the world

After a load, in order:

1. `LoadingStarted`: scene is open, nothing restored yet. `Awake`/`OnEnable` of scene objects have run; `Start` may not have.
2. During the phases: only objects with a lower phase are restored. Your own component can rely on earlier phases of other components only.
3. The spawned prefabs from `DestructibleList` appear in step 5, and get their `Start` during the single frame yield. Objects re-parented by `DestructibleList.OnSpawnLate` move two frames after that.
4. `OnMapChanged` (if applicable), then `MovePlayerToEntrypointDelayed(entrypoint)`: the player is teleported **0.1 s real time after** this, i.e. after `LoadingDone` (`:826-840`).
5. The Dialogue System restore happens immediately before `LoadingDone`. Any Lua variable you read or set during the object phases is reset by `DialogueManager.ResetDatabase` and then overwritten.
6. `LoadingDone`: all `OnLoading*` finished, `tempSavedata_*` cleared, `Loading == false`. Vanilla subscribers (`MarketShop`, `TenementController`, `BlackoutController`, `NaturalLightSourceChecker`, `ItemAchievementList`, `TenementUtilities`) run in subscription order and do their own deferred work. The fade-in starts right after the event.

Practical rule: do world edits in a coroutine started from `LoadingDone` that waits at least two frames, and, if the player position matters, also at least 0.2 s of real time (or hook `MovePlayerToEntrypoint`). Do not use `SaveController.Loading` to decide this.

Order of subscription: a mod that subscribes at plugin load runs before vanilla objects that subscribe in `Awake`/`OnEnable`/`Start` of a later scene.

## 11. Harmony hook points

All `SaveController` members below are on the class `SaveController` (global namespace). Prefer the public static events for notifications and use Harmony for interception.

| Purpose | Target | Notes |
|---|---|---|
| Observe every save | `static void SaveGame(string savename = "", string newLevel = "", string newLevelEntrypoint = "NONE")` | Postfix does not run if a save throws. Args show the slot (`""` means Autosave), destination level and entrypoint. |
| Observe load request | `static IEnumerator LoadGameWithMigration(string characterName = "", string saveName = "", bool fromMenu = false)` | Prefix runs at call time (iterator creation), and callers start the coroutine at once. For work inside the iterator use the events. |
| Level change request | `static void ChangeLevel(string newlevel, string newLevelEntrypoint = "NONE")` | Fires before the fade. `PlayerWillChangeLevel` is the later event. |
| Autosave | `static void Autosave(MonoBehaviour monoBehaviour)`, `static IEnumerator AutosaveDelay()`, private `static string GetOldestAutosave()` | Patch the last to change which slot rotates. |
| Per-object save/load | `static bool SerializeData(UnityEngine.Object data, bool global = false)`, `static bool DeSerializeData(UnityEngine.Object data, bool global = false)` | Postfix `__result` tells if data was written/applied. Prefix `DeSerializeData` can skip or substitute. They also run for mod-added components. |
| Field copy | private `static void ReplaceProperties(UnityEngine.Object original, UnityEngine.Object replacement)` | Prefix to inspect or fix up the throwaway copy; postfix to post-process the live object. |
| Phase loop | private `static void ExecuteChildSaveLoadFunctions(GameObject gameObject, LoadSaveType loadSaveType, bool includeInactive = false)` | `LoadSaveType` is a private enum, take the parameter as `int`/`__1`. Called once per root object per phase. |
| Raw JSON in | `static object Deserialize(string fileName, Type type, string serializedState, out Exception exception, out bool skippedFieldsOnly)` | `fileName` is only the short name (`Globals.tnmt`, `<Level>.tnmt`, `Info.tnmt`). A prefix can rewrite `serializedState` before parsing. The 4-arg overload forwards to this one. |
| Raw JSON out | `static string Serialize(Type type, object value)`; `static void WriteSaveFile(string path, string content)` | The normal `.tnmt` writes use the **private** `WriteSaveFile(string, SaveFileContent)` with bytes, not text, so `Serialize` and the public `WriteSaveFile` only see fallbacks and `Globals.dialogue`. To post-process saved files, read them back in `SavingDone`. |
| Disk read | `static string ReadSaveFile(string path)` | Postfix can alter the text of any save file. |
| Save list / info | `static SaveInfo GetSavedGameInfo(string savePath, out Exception exception)`, `static List<CharacterSaves> GetCharacters()` | For menu display changes. |
| Current folder | `string GetCurrentSavePath()` (instance method) | Same value `RevealCamera` uses. |
| Level exists | `static bool CheckIfLevelExists(string newlevel)` | |

Calls available to mods without Harmony (all public): `SaveController.SerializeData/DeSerializeData/HasSaveData(string)/GetObjectByGUID/GetObjectGUID/SetObjectGUID/GetOldSubGUIDs/Serialize/Deserialize/ReadSaveFile/WriteSaveFile/GetCharacters/GetSavedGameInfo/RemoveCharacter/ChangeLevel/SaveGame/Autosave`, the six events, and the public fields `instance`, `Loading`, `LastSaveFolderPath`, `DontDestroyOnLoadObjects`.

## 12. Gotchas

Threading and timing
- Everything in save/load runs on the main thread. `SaveGame` blocks the frame (including `Parallel.ForEach` file copies). Keep handlers short.
- The only background work: `Migrate` (`Task.Run`, touches `fsData` only), `RevealCamera` PNG writes (a `Task` that outlives `SavingDone`), `CopyDirectory` workers. Unity APIs from those threads are not allowed. Do not call Unity from continuations of those tasks.
- `RevealCamera.WaitForPendingMapWrite()` runs at the start of `SaveGame`, `LoadGameWithMigration` and `RemoveCharacter`, so a pending PNG write stalls the next save or load until it finishes.

Exceptions
- `SavingStarted`, `SavingDone`, `LoadingStarted`, `LoadingDone`, `PlayerWillChangeLevel` are plain `?.Invoke()`. An exception in one handler aborts the remaining handlers and propagates:
  - from `SavingStarted`/`SavingDone`: out of `SaveGame`. Nothing is written for the first, the saving UI stays up for the second;
  - from `LoadingStarted`/`LoadingDone`: kills the load coroutine. For `LoadingDone` that is after `Loading = false` but before the fade-in, so the screen stays black;
  - from `PlayerWillChangeLevel`: leaves `Loading == true` forever, which blocks every later level change and parts of the UI.
  Wrap every handler body in try/catch.
- `PlayerWillLoadGame` swallows exceptions (`catch {}`) but later subscribers are still skipped after a throw.

Silent drops
- `SerializeData` returns `false` with only a log line (or nothing) when `GUID` is missing, non-public, not a string, or empty. Check the return value in your override.
- `[SerializeField] private` without `[fsProperty]` is saved but never restored. Base-class privates never restore.
- Properties never restore. A public auto-property is still written, which is wasted space.
- Unknown JSON members vanish silently. Newly added public fields take the constructor default on old saves (7.2).
- Duplicate GUIDs: the second object only gets data if a second entry exists; otherwise it gets nothing. No warning.
- A level's `.tnmt` is wiped of any object that was inactive at save time (8.4).
- `DestructibleList.DestroyedGuids` destroys any `SavableScript` with a matching GUID on load, including mod components with colliding GUIDs.

Reflection caches
- `SaveController.replaceFields` / `fieldRules`, `FsStreamWriter.Plans/Storages/EnumPlans`, `FsStreamReader.Plans`, `fsMetaType` and `fsTypeCache` are static and never invalidated (except `fsTypeCache`, which resets when an assembly loads, and the two codegen-disable paths). Attribute changes on already-serialized types and types loaded after first use are not picked up. Load mod assemblies before the first save or load of the session.
- Anything that Harmony-patches member lists (adding fields at runtime) cannot work. Types are fixed at first use.
- A custom `fsConverter` added with `JSON_serializer.AddConverter(...)` forces the whole file onto the slow FullSerializer path (7.3). It works, but cost scales with file size.

Misc
- `SaveController.Serialize` ends with `AssertSuccessWithoutWarnings`, which throws on warnings as well as errors. Use it for your own types only if they are clean.
- `DeSerializeData` swallows exceptions (logs "has null data object"), returns `false`.
- Character folders are keyed on the player's name, so two playthroughs with the same name share `Autosave`/`Autosave2` and slots.
- `ChangeLevel`'s no-instance branch (`:309-318`) calls `instance.StartCoroutine` right after `SaveGame` creates the instance; it works but only because `SaveGame` runs first.
- `LoadMenu` passes the full folder path as `characterName`. `GetCurrentSavePath()` handles both forms, but static `CharacterName` is not a plain name until the next `SaveGame` calls `GetCharacterName()`.
- Wine/Proton: path comparison in the game is case-insensitive (`StringComparison.OrdinalIgnoreCase`). Keep your own file names consistently cased anyway.
