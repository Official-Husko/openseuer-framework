# Game clock and weather

`ObenFramework.Clock` wraps the game's `TimeOfDayAzure` and `WeatherManager`. Background on both is in [../../game/core-systems.md](../../game/core-systems.md) section 6.

Both only exist in gameplay scenes. `GameClock.IsAvailable` and `GameWeather.IsAvailable` tell you; the other members throw `InvalidOperationException` with a plain message when there is nothing to read. Interiors can have a clock but no weather.

## GameClock

| Member | Meaning |
|---|---|
| `TimeOfDay` | hours 0 to 24 as a float, 13.5 is 13:30 |
| `Hour`, `Minute` | 0-23, 0-59 |
| `TimeText` | `"13:05"` |
| `Day` | the game's day counter, days since the start (the Lua variable `Day`), saved with the game |
| `DayOfMonth`, `Month`, `Year` | the game's calendar |
| `WeekdayIndex` | the game's numbering, 0 is Monday, 6 is Sunday |
| `Weekday` | the same as `System.DayOfWeek` |
| `IsDaytime` | the game's own rule, 06:00 to 20:00 |
| `TimeSpeed` | the game's speed factor, 1 is normal (a day takes `dayLenghtInMinutes` real minutes, 96 in 0.5.13), 0 stops time |
| `SetTime(float hours)`, `SetTime(int hours, int minutes)` | jump to a time of day, same ranges as the console's `change_time` |
| `SetDate(year, month, day)` | set the calendar date, keeping the time; `Day` doesn't change. The day starts at 3 (`FirstDayOfMonth`): the game's months skip the 1st and 2nd |
| `Now` | a `ClockTime` with all of the above read at once |
| `After(owner, minutes, action)` | run `action` once that many in-game minutes have passed; dispose the result to cancel |

`SetTime` and `SetDate` throw while a save is written or loaded (`SaveGate.IsBusy`): the load sets the clock itself and would undo the change.

The game's calendar has no 1st or 2nd: after the last day of a month its clock rolls to the 1st, and the game moves it on to the 3rd at the next minute (`TimeOfDayAzure.IncreaseDayCounter`). A date set to the 1st or 2nd would stay until the next midnight, so `SetDate` refuses them.

`After` counts the game's minute tick, so sleeping and waiting count and frozen time doesn't. What a mod scheduled is dropped when it unloads, when another game is loaded or a new one starts, and at the main menu; level changes keep it. `WaitingCount` says how many actions wait.

`TimeSpeed` is the game's `timeSpeedOverrideFactor`. Sleeping and waiting set it themselves and put it back to 1 when they end, so a speed set by a mod doesn't survive them, and two mods setting it overwrite each other. Use `TimeControl` for anything a mod holds on to.

## TimeControl

Freezing the clock and changing its speed, held per mod:

```csharp
TimeControl.Freeze(this);          // time stops until Unfreeze(this) or the mod unloads
TimeControl.Unfreeze(this);
TimeControl.SetSpeed(this, 2f);    // twice as fast, 0.01 to 100
TimeControl.ClearSpeed(this);
```

| Member | Meaning |
| --- | --- |
| `IsFrozen`, `IsFrozenBy(owner)` | some mod freezes time / this one does |
| `Speed` | the speed in force: the newest one set, 1 when none |
| `IsSuspended` | the game itself is running time fast right now (sleeping, waiting, a crafting wait), so the overrides stand aside |
| `IsAvailable` | the clock hook is in place (its patch area `Clock` didn't fail) |
| `Holders()` | who holds a freeze or a speed, for tools |

- Any freeze stops time; it runs again once every freezing mod has let go. Of the speeds the newest wins, and when its mod clears it the one set before applies again.
- Everything a mod holds goes when it unloads, and all overrides end when the player returns to the main menu, loads another save or starts a new game. Level changes keep them.
- It doesn't touch the game's own speed factor (`GameClock.TimeSpeed`). It shortens or stops the day length the game hands the Azure time controller each frame, so while the game speeds time up for sleeping, waiting or a crafting wait the overrides stand aside and those always finish; they apply again afterwards. Slowdowns the game makes (card games run at 0.25) are combined with them.
- Frozen time means no `MinutePassed`, `HourChanged` or `DayChanged`, and no timers of the game that count in-game time.

Values are read from the Azure time controller, not from the game's `currentTimeAndDay`, which lags a minute behind inside the game's own minute event.

### Events

```csharp
GameClock.HourChanged.Subscribe(this, hour => Log.Info($"It is {hour}:00"));
GameClock.DayChanged.Subscribe(this, day => ...);      // payload: GameClock.Day
GameClock.MinutePassed.Subscribe(this, minute => ...); // payload: minute 0-59
GameClock.SecondPassed.Subscribe(this, seconds => ...);
```

- Subscribe with the owner (`this`), so a hot reload removes the handler. Without an owner you get an `IDisposable` back.
- Payloads are ints. `SecondPassed` gets how many in-game seconds went by.
- Each handler runs in its own try/catch. The game invokes its clock events inside one try/catch per event, so a single throwing handler would otherwise silence the rest; here it only logs.
- The game's events are only hooked while somebody subscribes. `MinutePassed` and `HourChanged` share one hook on the game's `MinutePassed`; `DayChanged` and `SecondPassed` each have their own. The hooks are removed again when the framework unloads. Nothing runs per frame unless a mod subscribes to `SecondPassed`.
- `SecondPassed` is the game's per-frame event (about 60 in-game seconds per real second). About 35 vanilla classes already use it. Prefer `MinutePassed` or `HourChanged`, and keep a `SecondPassed` handler trivial.
- `HourChanged` is derived from the minute event, so a time jump (`SetTime`, sleeping) raises it when the hour differs. The first minute after the hook starts only records the hour, it doesn't raise.
- Nothing fires while a save is written or loaded (`SaveGate.IsBusy`). After a level change the new scene runs on its own time for a moment before the save sets the clock, so those ticks would look like hours passing. The first minute after a load only records the hour, like the first minute after subscribing.

## GameWeather

| Member | Meaning |
|---|---|
| `Count`, `Names()` | the profiles, in index order |
| `CurrentIndex`, `Current` | active weather (index -1 and name null if the game has none selected) |
| `TryFind(name, out index)` | case-insensitive |
| `Set(string nameOrIndex, float transitionSeconds = 0)` | switch by name, or by index as text like the console; returns the real name |
| `Set(int index, float transitionSeconds = 0)` | switch by index |

`Set` calls `WeatherManager.SetWeather(index, changeSpeed: true, transition)`, which is what `change_weather` does. It also rolls for thunder the way the game does. The rain and wind intensity accessors on `WeatherManager` print to the log whenever they are above zero, so they are deliberately not wrapped.

## Dev bridge

`clock.get`, `clock.set` (`hours`, `minutes`, optional `speed`), `clock.setdate` (`year`, `month`, `day` from 3), `clock.after` (`minutes`, logs a line when they passed), `weather.get`, `weather.list`, `weather.set` (`name` as name or index, optional `transition`). At the main menu or in a scene without the system they return an error saying so.

`time.control` shows the overrides; `time.freeze` (`on`, default true) and `time.speed` (`speed`, or `clear=true`) hold them in the bridge's name.

```sh
oben call clock.get
oben call clock.set hours=6 minutes=30
oben call time.freeze
oben call time.freeze on=false
oben call time.speed speed=10
oben call time.speed clear=true
oben call weather.list
oben call weather.set name=Rain
```

See [../dev-tools.md](../dev-tools.md) for the tools.
