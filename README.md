# ObenseuerExtended

A mod loader, a modding framework, tools and mods for [Obenseuer](https://store.steampowered.com/app/951240/Obenseuer/).

No BepInEx needed. The loader hooks into Unity's own startup files, so the game loads it like one of its own assemblies.

## What's in here

| Path | What it is |
| --- | --- |
| `src/ObenLoader/` | The mod loader: finds mods, loads them in dependency order, hot reloads them while the game runs. |
| `src/ObenKit/` | The framework mods build on: game events, player, items, saves, UI, input. |
| `mods/` | One folder per mod. |
| `tools/ObenTool/` | The `oben` command line tool: install the loader, launch the game, read logs, talk to a running game. |
| `docs/game/` | Notes on how the game's own code works. |
| `docs/modding/` | Guides for the loader, the framework and the tools. |

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and the game installed. The build compiles against the game's own assemblies, and nothing from the game is in this repo.

The build looks for the game in the default Steam library. If yours is somewhere else, set `OBENSEUER_DIR` or create `build/Local.props`:

```xml
<Project>
  <PropertyGroup>
    <ObenGameDir>/path/to/Obenseuer</ObenGameDir>
  </PropertyGroup>
</Project>
```

Then:

```sh
dotnet build -c Release
```

When the game folder exists, every build copies itself into it: the loader into `Obenseuer_Data/Managed`, mods into `Mods/`.
