# Sessions, new games and start choices

`ObenFramework.NewGames` (`src/ObenFramework/NewGames/`) covers how a game starts: play sessions, the New Game screen, options mods add to it, and the backstory choices a new character picks. Difficulty is in [difficulty.md](difficulty.md), the tutorial prompts in [tutorial.md](tutorial.md).

## Sessions

A session starts when a save is loaded or a new game reaches its first level, and runs until the next load or the main menu. Doors and fast travel save and reload the level, but they continue the session. Use sessions for setup that belongs to a loaded game rather than to one level.

```csharp
using ObenFramework.NewGames;

Sessions.WhenStarted(this, kind => Log.Info($"Session started: {kind}"));
if (Sessions.IsNewGame) { /* first time this character plays */ }
```

| Member | Meaning |
|---|---|
| `Started` | `GameEvent<SessionKind>`, raised on the first `WorldReady` of a session; not for level changes |
| `WhenStarted(owner, action)` | subscribes, and runs at once when a session is already running |
| `Current` | how the running session began: `SaveLoad`, `NewGame`, `Direct` (a level loaded from the menu with the console's `new_scene`), `Unknown` (the framework was loaded into a running world) or `None` (main menu) |
| `IsRunning` | a session is running and its world is ready |
| `IsNewGame` | the running session began as a new game |

How it tells them apart (`SessionRules`, unit tested): a level change announces `PlayerWillChangeLevel` and then a load of the autosave it just wrote; a load from the menu or the pause menu announces only `PlayerWillLoadGame`; a new game announces neither and goes through the "Open Sewer Intro" scene first, which is skipped. The session kind survives a framework hot reload.

## The new game event

```csharp
NewGameEvents.Started.Subscribe(this, info =>
    Log.Info($"{info.FullName}, {info.Age}, difficulty {info.Difficulty?.Name}, preset {info.DifficultyPreset}"));
```

`NewGameEvents.Started` fires when the player confirms a new character (a postfix on `NewGame.SaveCharacter`), still in the main menu, before the intro loads; there is no world yet. `NewGameInfo` has `FirstName`, `LastName`, `FullName`, `Age`, `Gender`, `Difficulty` (a `DifficultySettings` copy of what the game will start with), `DifficultyPreset` (a mod preset key or null) and `Options` (every new game option value, picked or default). `NewGameEvents.Last` keeps the last one for this run of the game. When the first level is ready, `Sessions.Started` follows with `SessionKind.NewGame`.

## New game options

Mods add options to the New Game screen. The values are stored with the save and read for the rest of that game.

```csharp
// In OnLoad:
NewGameOptions.RegisterToggle(this, "permadeath", "Permadeath", "Dying deletes the save.", false);
NewGameOptions.RegisterChoice(this, "start", "Starting money", null,
    new[] { "poor", "normal", "rich" }, new[] { "Poor", "Normal", "Rich" }, 1);
NewGameOptions.RegisterInteger(this, "rats", "Rats per room", null, 0, 10, 2);
NewGameOptions.RegisterFloat(this, "rent", "Rent factor", null, 0.5f, 2f, 0.25f, 1f, NewGamePlacement.CustomPanel);

// Later, in a level:
bool permadeath = NewGameOptions.GetBool(this, "permadeath");
string start = NewGameOptions.GetChoice(this, "start");
```

- The kinds are a toggle (stored "1" or "0"), a choice (the chosen id; titles are only shown, so keep ids stable), a whole number from min to max, and a decimal from min to max in steps (at most 1000 steps; the default must be on a step). Keys are 1 to 64 letters, digits, `_` or `-`. Mistakes in a registration throw `ArgumentException`; registering a key twice logs a warning and returns null.
- A registration ends when its mod unloads; the stored values stay. Options can be registered at any time (a mod reloading registers them again); the screen updates (`NewGameOptions.ListChanged`).
- `NewGamePlacement` groups options in the window: `BelowDifficulty` first, `CustomPanel` ("custom rules") after.
- When the player confirms the new character, every registered option is stored, picked or default, in `<save>/mods/ObenFramework.newgame.json` (with the difficulty preset). The file is written with the first save of the new game.
- `GetValue/GetBool/GetChoice/GetInteger/GetFloat` read the stored value of the game being played; a stored value that no longer fits the option (an id that was removed, a number out of range) is moved to the nearest valid one. A save started before the option existed reads the option's default, and `WasChosen` tells the two apart. At the main menu they read what the New Game screen shows. `Values()` lists every stored value, including those of mods that aren't loaded.
- Removing a mod leaves its entries in the file untouched; nothing in the game's own save files is involved.

### The screen

ObenUIKit puts a **Mod options** button on the New Game screen's difficulty page, just above Back, while at least one option is registered. It opens a kit window with one group per mod: toggles and choices as the game's cycles, small whole-number ranges as a cycle of their numbers, larger ranges and decimals as sliders, and each option's description under it. **Defaults** puts every option back. The button is a copy of the page's own Back button (game font, colours and sounds) with its Relay objects removed; it isn't reachable by the screen's keyboard navigation, the window is. The code is in `src/ObenUIKit/NewGame/`.

## Start choices

The backstory cards a new character picks in the starting apartment (`StartManager`, saved with the game).

```csharp
foreach (var choice in StartChoices.Selected())
    Log.Info($"{choice.Id}: {choice.Title} (+{choice.Oc} OC, +{choice.Rm} RM)");
bool hasCat = StartChoices.Has("Cat");
StartChoices.Chosen.Subscribe(this, choice => Log.Info("Picked " + choice.Id));
```

`Relationship` gives "Single", "Married" or "In a relationship" the way the game works it out (`Family` means married, `Girlfriend`/`Boyfriend` a relationship). The ids in 0.5.13: `DevilsAngels Police CrazyNeighbor NoOne`, `DrinkingAlcohol SmokingMushrooms EveryDay IFeelHealthy`, `MushroomAddict ShroomshineEnjoyer FilthyPoor ShouldntBeHere`, `BlackHallucinations BrightColors CrazyNeighbor2 Nothing`, `Girlfriend Boyfriend GoodFriend SomeoneHere`, `Family Someone Cat Alone`, `WifeKids HusbandKids Wife Husband`. The game's Lua function `HasSelectedStart(id)` answers the same as `Has`.

## Bridge commands

| Command | Does |
|---|---|
| `session.info` | session kind, running, new game |
| `newgame.last` | the last `NewGameInfo` of this run |
| `newgame.options [mod] [key value]` | registered options with screen and stored values; with `mod`, `key` and `value` sets the screen value |
| `start.choices` | picks and relationship |
