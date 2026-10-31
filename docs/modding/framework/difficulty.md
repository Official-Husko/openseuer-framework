# Difficulty

`GameDifficulty` (`src/ObenFramework/NewGames/Difficulty/`, namespace `ObenFramework.NewGames`) reads the game's difficulty, scales its multipliers from a mod, and adds mod presets to the New Game screen.

The game keeps the difficulty in a `DifficultyController` in every scene (under `Game_Logic` in levels) and in the save. A level is a `DifficultyInfo`: a name, the `Difficulty` enum level, a few switches and a `DefaultMultiplier` step (0, a quarter steps up to 4, or infinite) for each multiplier. 0.5.13 offers three levels: **Default**, **Casual** and **Custom** (the enum also has Easy, Hard, Brutal and Creative, but no list uses them). Editing any value on the New Game screen turns the level into Custom.

## Reading

```csharp
using ObenFramework.NewGames;

DifficultySettings now = GameDifficulty.Current();   // null outside a scene with a controller
Log.Info($"{now.Name}: needs x{now.NeedsSpeed}, prices x{now.Prices}");
foreach (var level in GameDifficulty.Levels()) Log.Info(level.Name);
```

`DifficultySettings` is a copy with each multiplier as a number (1 is normal, `TraderWealth` is infinity for unlimited trader money), plus `UserSavesDisabled`, `AutosaveMode`, `MentalHealthDisabled`, `MandatoryStartingAddictionsDisabled`, `RentPayments` and the crafting speeds.

## Factors

```csharp
GameDifficulty.SetFactor(this, DifficultyMultiplier.NeedsSpeed, 0.5f);   // needs drop half as fast
GameDifficulty.SetFactor(this, DifficultyMultiplier.Prices, 1.2f, key: "inflation");
GameDifficulty.RemoveFactor(this, DifficultyMultiplier.NeedsSpeed);
```

- A factor scales the game's own value on top of the level: postfixes on `DifficultyInfo.GetGeneralNeedsSpeed`, `GetAddictionNeedsSpeed`, `GetAddictionRecoverySpeed`, `GetRenovationCost`, `GetRenovationResourceCost`, `GetRenovationSpeed`, `GetRentIncome`, `GetItemPrices`, `GetRespawningTime`, `GetSmvProgressionRateSpeed` and `GetTraderWealth`. Each returns at once while no mod set a factor.
- Factors are 0 to 10; prices and renovation speed can't be 0. Factors from several mods (and several keys of one mod) multiply. Whole-number results (prices, resource costs) are rounded and never go below 1 where the game's value was at least 1.
- Renovation speed: a factor of 2 halves the time. The game stores an upgrade's time unscaled and divides it when read, so the framework also scales the countdown (`TenementController.UpdateProgesses`, keeping fractions), otherwise the shown time and the real one would differ.
- Factors aren't saved and go when the mod unloads. Set them again each session (`Sessions.WhenStarted`). `FactorOf` and `FactorList()` read them back.
- The game's other settings have no factor: item and furniture crafting speed, stack sizes and rent payments can be set in a preset only. The Custom page's "Autosave Mode" is hidden and nothing in 0.5.13 reads it.

## Presets

```csharp
GameDifficulty.RegisterPreset(this, "harsh", "Harsh", "Needs drain faster and traders are poor.",
    Difficulty.Default, s =>
    {
        s.NeedsSpeed = 1.5f;
        s.TraderWealth = 0.5f;
        s.UserSavesDisabled = true;
    });
```

- A preset starts from one of the game's levels other than Custom (in 0.5.13 that means `Default` or `Casual`). Its callback gets `DifficultyPresetSettings` filled with that level and changes what it wants; numbers go to the nearest step the game knows (`DifficultyPresetMap`, unit tested), and `float.PositiveInfinity` gives traders unlimited money.
- A preset that changes values becomes a **Custom** level carrying the preset's name. That is how the game stores it: the save keeps the Custom values in the DifficultyController's own data, so the save keeps working without the mod. A preset without a callback is its base level under another name.
- The New Game screen lists registered presets (sorted by key, `modId.id`) after the game's levels in its difficulty selector. Only the main menu's controller list is extended, so a level's saved index never points at a mod entry; the game's list is put back when the framework unloads. If the player edits a value on a preset, the game turns it into plain Custom.
- Players who don't want mod presets in the selector switch them off in MODS, Settings, UI kit, "Show mod difficulty presets". ObenUIKit passes that on to `GameDifficulty.PresetsOnNewGameScreen`; turning it off while the screen is up takes the presets out at once, and a selected preset falls back to the game's Custom level. Games started with a preset keep it either way.
- When the player confirms the new character, the preset they were on is recorded in the framework's newgame file (`<save>/mods/ObenFramework.newgame.json`). `ActivePreset` returns its key while the mod is loaded and the game's difficulty still matches it.
- In a running game, `SetPreset("modId.id")` switches to a preset now (it returns null on success, otherwise why not), `SetLevel("Casual")` switches to a game level by name like the console's `set_difficulty`, and `ClearPreset()` forgets the recorded preset. All are refused during a save or load. The change is saved with the game.

## Bridge commands

| Command | Does |
|---|---|
| `difficulty.get` | current settings, levels here, factors, active preset |
| `difficulty.set factor=<multiplier> value=<f>` | a DevBridge-owned factor (no `value` removes it) |
| `difficulty.set preset=<modId.id>` / `level=<name>` / `clear=true` | switch preset or level, forget the preset |
| `difficulty.presets [add=<id> name=<name> basedOn=<level> needs=<f>]` | the registered presets; `add` registers a DevBridge-owned test preset first |
