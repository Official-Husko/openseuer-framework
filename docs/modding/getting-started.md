# Getting started

This walks through how mods are found and run, and how to make one. For the tools (`oben`, the dev bridge, scripts, MCP) see [dev-tools.md](dev-tools.md).

## How the loader starts

There is no BepInEx and no proxy DLL. `oben install` copies `ObenLoader.dll`, `0Harmony.dll` and `Mono.Cecil.dll` into `Obenseuer_Data/Managed` and adds the loader to two files Unity reads at startup:

- `ScriptingAssemblies.json`, the list of assemblies Unity loads, so `ObenLoader.dll` loads like one of the game's own.
- `RuntimeInitializeOnLoads.json`, the list of methods Unity calls before the first scene, so it calls `ObenLoader.Entry.Init`.

Both files are backed up once as `*.obenbak` and put back by `oben uninstall`.

On startup the loader:

1. creates `Mods/` and `ObenLoader/logs/` next to the game's exe,
2. reads `ObenLoader/settings.json` (switched-off mods, auto reload, verbose log, refusing mods made for other game versions, the loading panel),
3. finds every folder in `Mods/` that has a `mod.json`,
4. orders them by their dependencies and loads them.

The log is `ObenLoader/logs/latest.log` (`previous.log` is the last session's). `oben logs` prints its end.

## A mod folder

Every mod should live in a folder of its own under `Mods/`. A DLL dropped straight into `Mods/` still loads if it is a mod (it declares itself with `[ObenModInfo]`, or uses the loader and gets an id from its file name), with a warning in the log; its settings and files go in `Mods/<id>/`. Loose DLLs that aren't mods are skipped with a log line.

```
Mods/
  MyMod/
    mod.json
    MyMod.dll
    MyMod.pdb      (optional, gives line numbers in stack traces)
    config.json    (created by the mod on first run, if it has settings)
```

### mod.json

```json
{
  "id": "MyMod",
  "name": "My Mod",
  "version": "0.1.0",
  "author": "you",
  "description": "One line about what it does.",
  "dependencies": { "ObenFramework": "0.1.0" },
  "hotReload": true
}
```

| Field | Default | Meaning |
| --- | --- | --- |
| `id` | folder name | Unique id. Also the mod's Harmony id and log tag. 1 to 64 letters, digits, `.`, `_` or `-` (no spaces, not starting or ending with `.`, not a Windows device name like `CON`); a mod with another id doesn't load and says why. |
| `name` | `id` | Display name. |
| `version` | `0.0.0` | Checked against other mods' `dependencies`. Numbers like `1.2.0`; one that can't be read loads with a warning and counts as `0.0`. |
| `assemblies` | every DLL in the folder | DLLs that make up the mod. With a list, other DLLs in the folder are libraries: loaded on first use and never reloaded. |
| `dependencies` | none | `"id": "minimum version"` (`"*"` for any, `">=1.2"` works too). Missing, failed or too old dependencies stop the mod from loading, and so does an id or minimum version that can't be read. |
| `loadAfter` | none | Load after these if they're installed, without needing them. |
| `autoPatch` | `true` | Apply every `[HarmonyPatch]` class in the mod automatically; one class that fails stops the mod from loading. With `false`, call `ModPatches.ApplyClasses(this)` in `OnLoad` to apply them area by area instead (see [framework/patching.md](framework/patching.md)). |
| `hotReload` | `false` | Load from memory and reload when the DLL is rebuilt. |
| `essential` | `false` | Can't be switched off or unloaded (the framework). |
| `enabledByDefault` | `true` | `false` means installed but off until switched on. |
| `gameVersion` | any | The game versions the mod was made for. On another version it loads with a warning in the log and on the Errors page (`ModInfo.Warning`). |
| `strictGameVersion` | `false` | `true` refuses to load on a game version outside `gameVersion` (or a build outside `gameBuilds`) instead of warning. |
| `anyGameVersion` | `false` | `true` says the mod works with every game version (same as `"gameVersion": "*"`): no version or build check and no warning on the Errors page, and `RefuseOtherGameVersions` doesn't refuse it, but the log notes on every start that it wasn't checked. |
| `gameBuilds` | none | Steam build ids the mod was made for (`["25969064"]`), checked like `gameVersion`. Only checked when the game came from Steam: the build id is read from Steam's `appmanifest_951240.acf`, so a sideloaded or DRM-free copy skips it. |
| `gameHashes` | none | SHA-256 hashes, or prefixes of at least 8 hex digits, of the game's `Assembly-CSharp.dll` the mod was made for (`["72c44ab48d2ea50f"]`). Checked like `gameVersion` on every copy, whichever store it came from. With both lists, either one matching is enough (a hotfix that only rebuilt the exe keeps the code hash). |

`gameVersion` takes `0.5.13` (that version), `0.5` or `0.5.x` (anything starting with those numbers), comparisons like `>=0.5.10` and `<0.6`, several of them separated by spaces (all must hold), and alternatives separated by `||`: `">=0.5.10 <0.6 || 0.5.08"`. The game's version is `Application.version` (0.5.13 at the time of writing); `oben call game.info` shows it, and the first line of `latest.log` names the Steam build when it is known (`Loader.GameBuildId`; read from Steam's app manifest, also a copy kept in the game folder) and the game's code hash (`Loader.GameCodeHash`, the SHA-256 of `Assembly-CSharp.dll`). The exe's hash (`Loader.GameExeHash`) is in the log and the error report, for telling copies apart. `FrameworkInfo.IsVerifiedGame` says whether the running game is the one the framework was verified on.

A player who would rather go without a mod than run one made for another game version sets `"RefuseOtherGameVersions": true` in `ObenLoader/settings.json`: every version or build mismatch then refuses the mod, as if each had `strictGameVersion`.

### Declaring the mod in code instead

A folder without a mod.json is a mod too when one of its DLLs declares itself on its `ObenMod` class:

```csharp
[ObenModInfo("MyMod", "My Mod", "1.0.0", Author = "Me", GameVersion = "0.5.x")]
[ObenModDependency("ObenFramework", "0.1.0")]
public sealed class MyMod : ObenMod { ... }
```

The loader reads the attributes without loading the DLL. That DLL is the mod; other DLLs in the folder are its libraries, as with an `assemblies` list. `Author`, `Description`, `GameVersion`, `AnyGameVersion` and `HotReload` can be set; everything else keeps the mod.json defaults, so use a mod.json for `essential`, `loadAfter`, `gameBuilds` and the rest. A mod.json in the folder always wins over the attributes.

## The entry class

A mod's code starts in one class that derives from `ObenMod`. Override only what you need; the loader never calls the rest, so unused callbacks cost nothing.

```csharp
using ObenFramework.Events;
using ObenLoader;

public sealed class MyMod : ObenMod
{
    public override void OnLoad()
    {
        Log.Info("Hello");
        GameEvents.WhenWorldReady(this, scene => Log.Info($"In {scene} now"));
    }

    public override void OnUnload()
    {
        // Undo anything that wasn't Track()ed.
    }
}
```

| Member | What it's for |
| --- | --- |
| `OnLoad()` | Runs once per load, after the DLL is loaded and its Harmony patches applied. At game start no scene exists yet. |
| `OnAllLoaded()` | After every mod in the same batch finished `OnLoad`. |
| `OnUnload()` | Before the mod unloads or reloads. |
| `OnUpdate()`, `OnLateUpdate()`, `OnFixedUpdate()`, `OnGUI()` | Unity's per-frame callbacks. Keep them allocation-free. |
| `OnSceneLoaded(scene, mode)` | Every scene load. |
| `OnQuit()` | The game is closing. |
| `Log` | Logger tagged with the mod id. |
| `Harmony` | Harmony instance with the mod's id; unpatched on unload. |
| `Track(disposable)` | Anything passed here is disposed when the mod unloads. |
| `CreateObject(name)` | A `DontDestroyOnLoad` GameObject that goes away with the mod. |
| `StartCoroutine(routine)` | A coroutine that's stopped when the mod unloads. |
| `LoadConfig<T>()` | Typed settings in the mod folder's `config.json`: `Value`, `Save()`, `Reload()`, `Changed`, `Watch(owner, read, changed)` for one value, `Defaults` and `ResetToDefaults()`. Every save logs the values that changed. Enums are saved by name (`"F7"`, `"Lamp, Fridge"`; numbers in older files still read). A value that doesn't fit its field (a misspelt name, text where a number belongs) keeps that field's default with a warning in the mod's log; only a file that isn't JSON at all falls back to the defaults as a whole. The file is listed in the mod's `Configs`, and the mod menu's Settings page shows it by itself; `[System.ComponentModel.Browsable(false)]` on the class keeps it off. |

Framework APIs that take the mod as an owner (`GameEvents.X.Subscribe(this, ...)`, `GameConsole.Register(this, ...)`) clean up after themselves, so most mods don't need `OnUnload` at all.

The most used per-mod services are also calls on the mod itself, with `using ObenFramework;` and `using ObenUIKit;` (inside the class, write `this.` in front):

```csharp
var data = this.SaveData<MyData>();                    // ModSaves.Register: per-save data
this.Postfix(typeof(MarketShop), "Pay", typeof(Hooks), nameof(Hooks.AfterPay));   // ModPatches, also Prefix, Finalizer, Transpiler
this.Hotkey("toggle", "Toggle the panel", KeyCode.F7, Toggle);                 // Hotkeys.Register
this.SettingsPage(LoadConfig<MyConfig>(), "My mod");                          // ModSettings.Register
```

`ModRegistry.IsInstalled(id)` and `IsLoaded(id)` tell whether another mod is there, for optional features that use it.

## Logging

`Log` writes to `ObenLoader/logs/latest.log`, the in-game log buffer (`oben logs`) and Unity's Player.log, tagged with the mod id, and to the mod's own file `ObenLoader/logs/mods/<mod id>.log` (with frame numbers; `<mod id>.previous.log` keeps the last session's, or the earlier part once a file passes 4 MB; `"ModLogFiles": false` in `ObenLoader/settings.json` turns these off). Errors also land in `errors.log` and on the Errors page of the Mods menu.

| Call | Use |
| --- | --- |
| `Log.Debug/Info/Warn/Error(message)` | Plain lines. Debug lines only reach the file with `"VerboseLog": true` in `ObenLoader/settings.json`. |
| `Log.Error(message, exception)` | The exception and its stack go under the message. |
| `Log.Debug(key, message)` | Change-only: written when `message` differs from the last one under `key`. For state you check every frame. |
| `Log.InfoOnce/WarnOnce(key, message)`, `Log.ErrorOnce(key, message, e)` | Written the first time `key` comes up, then not again until the mod reloads. Returns whether it was written. A repeat costs one set lookup, so they are fine in code that runs every frame. |
| `Log.Failed(where, e)` | For Harmony handlers and game event handlers, which must never throw: "where failed" once per place. |
| `Log.ForgetOnce(prefix)` | Lets the once-keys starting with `prefix` (all with null) log again, e.g. after a level change. |
| `Log.LineLimit` | Lines per second before the logger starts dropping them (default 100). Dropped lines are counted in one note when the second is over. |

Errors are kept readable when something fails over and over: the same error (the same message, or the same exception type thrown at the same place) is written once in 10 seconds, and at most 20 errors in 10 seconds; a note says how many were held back. Every one of them still counts in `errors.log`. `Log.DroppedLines` and `Log.HeldBackErrors` count what this logger left out; `LogHub.DroppedLines` and `LogHub.HeldBackErrors` the whole session's (`oben call log.stats`, and the developer menu's session page). Pending notes are written when the game closes.

`"VerboseLog": true` is for tracking a problem down: debug lines reach the files, and errors and the once-only warnings and failures are written every time (the line limit still holds). `LogHub.Verbose` tells whether it is on. If a log file can't be written (disk full, file locked), the loader says so once and tries again every 5 seconds.

`LogHub.LineWritten` is raised for every line a mod or the loader writes; the API tracer mirrors the log into its trace with it.

A new logger comes with every load, so the once-keys and the change-only memory start over after a hot reload.

## Hot reload

With `"hotReload": true`, rebuilding a mod while the game runs swaps in the new code within about two seconds. The loader:

- loads the DLL from memory, so the file isn't locked,
- polls the DLL's write time once a second and reloads once it has stopped changing,
- unloads the mod and everything that depends on it (`OnUnload`, tracked cleanup, Harmony unpatch), then loads them all again,
- gives each reloaded copy a new assembly name (`MyMod-r2`, `-r3`...) and points references at the newest copies, because Mono binds a name to the first assembly it saw.

Things to know:

- Static fields start over after a reload. To carry plain data across, use `ReloadState.Put` in `OnUnload` and `ReloadState.Take` in `OnLoad`.
- Old copies stay in memory until the game exits; Mono can't unload assemblies.
- Anything not tracked (a `new GameObject`, an event handler on a game object) stays behind after unload. Track it.
- The loader itself, Harmony and Cecil can't be reloaded.
- Patching another mod's methods (rather than the game's) works across its reloads too: MonoMod caches the types a patched method uses, and for generic types over a mod's own types (`List<MyRecord>`) that cache can't tell the copies apart, so the loader empties it whenever a new copy loads.

## Building a mod in this repo

1. Make `mods/MyMod/` with `MyMod.csproj`, `mod.json` and your code. `mods/Directory.Build.props` already references the game's assemblies and Harmony, and `mods/Directory.Build.targets` packages the folder and copies it into the game after every build.

   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <RootNamespace>MyMod</RootNamespace>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="../../src/ObenLoader/ObenLoader.csproj" />
       <ProjectReference Include="../../src/ObenFramework/ObenFramework.csproj" />
     </ItemGroup>
   </Project>
   ```

2. Add it to `ObenseuerExtended.slnx`.
3. `dotnet build ObenseuerExtended.slnx -c Release`. If the game is running and the mod is new, load it without restarting: `oben call mods.rescan` then `oben call mods.load id=MyMod`.

## Game events

`ObenFramework.Events.GameEvents` covers the moments most mods care about. Every handler runs in its own try/catch, because the game itself calls its save events without one and a throwing handler there can leave a black screen.

| Event | When |
| --- | --- |
| `MainMenu` | The main menu loaded. |
| `SceneLoaded` (scene name) | Any scene loaded. |
| `WorldReady` (scene name) | A playable level is up: save applied, player in place. Once per level load; doors reload the level too. |
| `LevelChanging` | The player is leaving the level (a save and reload follow). |
| `LoadAborted` (true for a level change) | A level change or save load was announced but didn't happen (missing or broken save, or it stayed in the same scene). The player is still where they were; `WorldReady` doesn't fire again. |
| `Saving`, `Saved` (save folder) | Around a save; `Saving` comes before any file is written. Keep mod files in the save folder, written in `Saved`. |
| `Loading`, `Loaded` (save folder) | Around a save load. Wait for `WorldReady` before touching the world. |

The save folder is the full path, like `.../Saves/Mira_Kallio/Slot2`; a level change writes and loads `Autosave`. A handler that doesn't need it can leave the argument out: `GameEvents.Saving.Subscribe(this, () => ...)`.

`GameEvents.WhenWorldReady(this, scene => ...)` runs now if the world is ready and again after every level load. `GameEvents.OnceWorldReady(this, scene => ...)` runs once: now, or at the next `WorldReady`; dispose what it returns to cancel. For things that belong to a loaded game rather than a level, use `ObenFramework.NewGames.Sessions.WhenStarted` (once per save load or new game).

`ObenFramework.Game.WorldState` has `InWorld`, `InMainMenu`, `IsReady`, `IsLoadingSave`, `IsPaused` and `SceneName`.

## Console commands

The game's developer console (F1) can carry mod commands:

```csharp
GameConsole.Register(this, "heal", "heal [amount]", args =>
{
    float amount = args.Length > 0 ? float.Parse(args[0]) : 100f;
    PlayerStats.instance.health.currentStatus = amount;
    return $"Health set to {amount}";
});
```

They show up in `help` under "Mod commands" and disappear when the mod unloads.

- A name is one word without quotes (the console splits a line on spaces, keeping `"quoted parts"` whole); anything else throws. Two mods can't register the same name. A mod command with the name of one of the game's replaces it while the mod is loaded.
- The handler gets the words after the name and returns text to print (rich text works), or null. A handler that throws prints "name failed: ..." in red.
- `GameConsole.Print(text)` prints at any time, `Open()` and `Close()` open and shut the console as F1 does, `IsOpen` says whether it's up, and `Execute(line)` runs a line as if typed and returns what it printed.
- `GameConsole.IsAvailable` is false when the console hooks couldn't go on (the game changed); commands still register but nothing runs them.
