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
| `TimeSpeed` | speed factor, 1 is normal (24 real minutes per day), 0 stops time |
| `SetTime(float hours)`, `SetTime(int hours, int minutes)` | jump to a time of day, same ranges as the console's `change_time` |

`TimeSpeed` is the game's `timeSpeedOverrideFactor`. Sleeping and waiting set it themselves and put it back to 1 when they end, so a speed set by a mod doesn't survive them.

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
- Nothing fires while the game holds the clock still (loading, the first frames of a scene).

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

`clock.get`, `clock.set` (`hours`, `minutes`, optional `speed`), `weather.get`, `weather.list`, `weather.set` (`name` as name or index, optional `transition`). At the main menu or in a scene without the system they return an error saying so.

```sh
oben call clock.get
oben call clock.set hours=6 minutes=30
oben call weather.list
oben call weather.set name=Rain
```

See [../dev-tools.md](../dev-tools.md) for the tools.
