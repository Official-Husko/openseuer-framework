using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Clock;

namespace DevBridge.Commands;

internal static class ClockCommands
{
    [BridgeCommand("clock.get", "In-game time, date and time speed.")]
    private static object ClockGet(JObject p)
    {
        RequireClock();
        return new JObject
        {
            ["time"] = GameClock.TimeText,
            ["hour"] = GameClock.Hour,
            ["minute"] = GameClock.Minute,
            ["timeOfDay"] = GameClock.TimeOfDay,
            ["day"] = GameClock.Day,
            ["dayOfMonth"] = GameClock.DayOfMonth,
            ["month"] = GameClock.Month,
            ["year"] = GameClock.Year,
            ["weekday"] = GameClock.Weekday.ToString(),
            ["weekdayIndex"] = GameClock.WeekdayIndex,
            ["daytime"] = GameClock.IsDaytime,
            ["timeSpeed"] = GameClock.TimeSpeed,
        };
    }

    [BridgeCommand("clock.set", "Set the time of day; optionally the time speed.",
        Params = new[] { "hours: 0-24", "minutes: 0-59, default 0", "speed: optional time speed, 1 is normal" })]
    private static object ClockSet(JObject p)
    {
        RequireClock();
        int hours = p["hours"]?.Value<int>() ?? throw new ArgumentException("Missing parameter 'hours'.");
        GameClock.SetTime(hours, p.Int("minutes", 0));
        if (p["speed"] != null)
            GameClock.TimeSpeed = p.Float("speed");
        return ClockGet(p);
    }

    [BridgeCommand("clock.setdate", "Set the calendar date, keeping the time of day.", Params = new[] { "year", "month: 1-12", "day: 3-31 (the game skips the 1st and 2nd)" })]
    private static object ClockSetDate(JObject p)
    {
        RequireClock();
        int year = p["year"]?.Value<int>() ?? throw new ArgumentException("Missing parameter 'year'.");
        int month = p["month"]?.Value<int>() ?? throw new ArgumentException("Missing parameter 'month'.");
        int day = p["day"]?.Value<int>() ?? throw new ArgumentException("Missing parameter 'day'.");
        GameClock.SetDate(year, month, day);
        return GameClock.Now.ToString();
    }

    [BridgeCommand("clock.after", "Log a line after some in-game minutes (tests GameClock.After).", Params = new[] { "minutes: at least 1" })]
    private static object ClockAfter(JObject p)
    {
        int minutes = p.Int("minutes", 0);
        string start = GameClock.IsAvailable ? GameClock.TimeText : "?";
        GameClock.After(DevBridgeMod.Instance, minutes, () =>
            DevBridgeMod.Instance.Log.Info($"clock.after: {minutes} in-game minutes passed since {start}, now {GameClock.TimeText}"));
        return new JObject { ["scheduledAt"] = start, ["minutes"] = minutes, ["waiting"] = GameClock.WaitingCount };
    }

    [BridgeCommand("weather.get", "The active weather.")]
    private static object WeatherGet(JObject p)
    {
        RequireWeather();
        return new JObject { ["index"] = GameWeather.CurrentIndex, ["name"] = GameWeather.Current };
    }

    [BridgeCommand("weather.list", "Every weather name with its index.")]
    private static object WeatherList(JObject p)
    {
        RequireWeather();
        var names = GameWeather.Names();
        var list = new JArray();
        for (int i = 0; i < names.Count; i++)
            list.Add(new JObject { ["index"] = i, ["name"] = names[i] });
        return new JObject { ["current"] = GameWeather.CurrentIndex, ["weathers"] = list };
    }

    [BridgeCommand("weather.set", "Switch the weather.", Params = new[] { "name: weather name or index", "transition: seconds to blend, default 0" })]
    private static object WeatherSet(JObject p)
    {
        RequireWeather();
        string name = GameWeather.Set(p.Str("name"), p.Float("transition", 0f));
        return new JObject { ["index"] = GameWeather.CurrentIndex, ["name"] = name };
    }

    private static void RequireClock()
    {
        if (!GameClock.IsAvailable)
            throw new InvalidOperationException("There is no game clock: not in a gameplay scene (main menu or still loading).");
    }

    private static void RequireWeather()
    {
        if (!GameWeather.IsAvailable)
            throw new InvalidOperationException("There is no weather: not in a gameplay scene with a weather system (main menu, loading or an interior).");
    }
}
