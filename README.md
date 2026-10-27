# ObenseuerExtended

A mod loader, a modding framework, tools and mods for [Obenseuer](https://store.steampowered.com/app/951240/Obenseuer/).

No BepInEx needed. The loader hooks into Unity's own startup files, so the game loads it like one of its own assemblies.

## What's in here

| Path | What it is |
| --- | --- |
| `src/ObenLoader/` | The mod loader: finds mods, loads them in dependency order, hot reloads them while the game runs. |
| `src/ObenFramework/` | The framework mods build on: game events, player, items, saves, UI, input. |
| `mods/` | One folder per mod. DevBridge (local control socket for tools) and Script Runner (run C# in the live game) so far. |
| `tools/ObenTool/` | The `oben` command line tool: install the loader, launch the game, read logs, talk to a running game, run scripts, MCP server. |
| `docs/game/` | Notes on how the game's own code works. |
| `docs/modding/` | Guides for the loader, the framework and the tools. Start with [getting-started.md](docs/modding/getting-started.md) and [dev-tools.md](docs/modding/dev-tools.md). |
| `docs/research/` | Bigger investigations: the reference co-op mod, performance. |
| `scripts/` | Example scripts for `oben run`. |
| `TODO.md` | What's planned: framework modules, ObenUIKit, ObenSave, co-op, a cheat menu, performance fixes. |

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

Then hook the loader into the game once and start it:

```sh
dotnet tools/ObenTool/bin/Release/net10.0/oben.dll install
dotnet tools/ObenTool/bin/Release/net10.0/oben.dll launch --wait 150
```

On Linux the tool launches the game the same way Heroic does (umu-launcher, the game's Proton build and Wine prefix). See [docs/modding/dev-tools.md](docs/modding/dev-tools.md).
