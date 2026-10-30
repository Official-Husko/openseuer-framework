ObenseuerExtended's own mods are built on a framework you can use for yours.

- **The loader** finds mods in the game's `Mods` folder (one folder each, with a `mod.json`), loads them in the order their dependencies need, and reloads a mod while the game runs when you rebuild it. No BepInEx.
- **The framework** wraps the game: events (scenes, saving, loading, the clock), the player, items and recipes, quests and dialogue, NPCs, shops and money, furniture, the world, per-save mod data, Harmony patches that can be switched on and off, the F1 console, and much more.
- **The UI kit** makes windows that look like the game's own menus, with keyboard and controller support, and puts your mod's settings, hotkeys and pages into the MODS menu.
- **The tools:** `oben` installs, launches and checks the game, reads logs, and talks to a running game; a script runner lets you try code in the game without a rebuild.

## Where to start

The guides live with the code, so they always match it:

- [Getting started](https://github.com/Official-Husko/openseuer-framework/blob/main/docs/modding/getting-started.md): the loader, `mod.json`, your first mod, hot reload.
- [Dev tools](https://github.com/Official-Husko/openseuer-framework/blob/main/docs/modding/dev-tools.md): `oben`, the dev bridge, scripts.
- [The framework](https://github.com/Official-Husko/openseuer-framework/tree/main/docs/modding/framework): one page per part.
- [The UI kit](https://github.com/Official-Husko/openseuer-framework/blob/main/docs/modding/framework/uikit.md).
- [How the game works](https://github.com/Official-Husko/openseuer-framework/tree/main/docs/game): notes on the game's own code.
- [Bug reports](https://github.com/Official-Husko/openseuer-framework/blob/main/docs/modding/reports.md): the report format, for tools that read it.

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and the game installed; the build compiles against the game's own files, and nothing from the game is in the repository. The [README](https://github.com/Official-Husko/openseuer-framework#making-your-own-mods) has the steps.
