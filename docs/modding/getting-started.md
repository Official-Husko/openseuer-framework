# Getting started

This walks through how mods are found and run, and how to make one. For the tools (`oben`, the dev bridge, scripts, MCP) see [dev-tools.md](dev-tools.md).

## How the loader starts

There is no BepInEx and no proxy DLL. `oben install` copies `ObenLoader.dll`, `0Harmony.dll` and `Mono.Cecil.dll` into `Obenseuer_Data/Managed` and adds the loader to two files Unity reads at startup:

- `ScriptingAssemblies.json`, the list of assemblies Unity loads, so `ObenLoader.dll` loads like one of the game's own.
- `RuntimeInitializeOnLoads.json`, the list of methods Unity calls before the first scene, so it calls `ObenLoader.Entry.Init`.

Both files are backed up once as `*.obenbak` and put back by `oben uninstall`.

On startup the loader:

1. creates `Mods/` and `ObenLoader/logs/` next to the game's exe,
2. reads `ObenLoader/settings.json` (switched-off mods, auto reload, verbose log),
3. finds every folder in `Mods/` that has a `mod.json`,
4. orders them by their dependencies and loads them.

The log is `ObenLoader/logs/latest.log` (`previous.log` is the last session's). `oben logs` prints its end.

## A mod folder

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
  "dependencies": { "ObenKit": "0.1.0" },
  "hotReload": true
}
```

| Field | Default | Meaning |
| --- | --- | --- |
| `id` | folder name | Unique id. Also the mod's Harmony id and log tag. |
| `name` | `id` | Display name. |
| `version` | `0.0.0` | Checked against other mods' `dependencies`. |
| `assemblies` | every DLL in the folder | DLLs that make up the mod. With a list, other DLLs in the folder are libraries: loaded on first use and never reloaded. |
| `dependencies` | none | `"id": "minimum version"` (`"*"` for any). Missing, failed or too old dependencies stop the mod from loading. |
| `loadAfter` | none | Load after these if they're installed, without needing them. |
| `autoPatch` | `true` | Apply every `[HarmonyPatch]` class in the mod automatically. |
| `hotReload` | `false` | Load from memory and reload when the DLL is rebuilt. |
| `essential` | `false` | Can't be switched off or unloaded (the framework). |
| `enabledByDefault` | `true` | `false` means installed but off until switched on. |

## The entry class

A mod's code starts in one class that derives from `ObenMod`. Override only what you need; the loader never calls the rest, so unused callbacks cost nothing.

```csharp
using ObenKit.Events;
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
| `LoadConfig<T>()` | Typed settings in the mod folder's `config.json`. |

Framework APIs that take the mod as an owner (`GameEvents.X.Subscribe(this, ...)`, `GameConsole.Register(this, ...)`) clean up after themselves, so most mods don't need `OnUnload` at all.

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

## Building a mod in this repo

1. Make `mods/MyMod/` with `MyMod.csproj`, `mod.json` and your code. `mods/Directory.Build.props` already references the game's assemblies and Harmony, and `mods/Directory.Build.targets` packages the folder and copies it into the game after every build.

   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <RootNamespace>MyMod</RootNamespace>
     </PropertyGroup>
     <ItemGroup>
       <ProjectReference Include="../../src/ObenLoader/ObenLoader.csproj" />
       <ProjectReference Include="../../src/ObenKit/ObenKit.csproj" />
     </ItemGroup>
   </Project>
   ```

2. Add it to `ObenseuerExtended.slnx`.
3. `dotnet build ObenseuerExtended.slnx -c Release`. If the game is running and the mod is new, load it without restarting: `oben call mods.rescan` then `oben call mods.load id=MyMod`.

## Game events

`ObenKit.Events.GameEvents` covers the moments most mods care about. Every handler runs in its own try/catch, because the game itself calls its save events without one and a throwing handler there can leave a black screen.

| Event | When |
| --- | --- |
| `MainMenu` | The main menu loaded. |
| `SceneLoaded` (scene name) | Any scene loaded. |
| `WorldReady` (scene name) | A playable level is up: save applied, player in place. Once per level load; doors reload the level too. |
| `LevelChanging` | The player is leaving the level (a save and reload follow). |
| `Saving`, `Saved` (save folder) | Around a save. Keep mod files in the save folder, written in `Saved`. |
| `Loading`, `Loaded` | Around a save load. Wait for `WorldReady` before touching the world. |

`ObenKit.Game.WorldState` has `InWorld`, `InMainMenu`, `IsReady`, `IsLoadingSave`, `IsPaused` and `SceneName`.

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
