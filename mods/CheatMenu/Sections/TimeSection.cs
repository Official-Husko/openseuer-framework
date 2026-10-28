using System;
using ObenFramework.Clock;
using ObenUIKit.Controls;

namespace CheatMenu.Sections;

/// <summary>The hour, how fast time runs, and the weather.</summary>
internal static class TimeSection
{
    private static readonly string[] SpeedNames = { "Stopped", "0.5x", "Normal", "2x", "5x", "10x", "30x" };
    private static readonly float[] SpeedValues = { 0f, 0.5f, 1f, 2f, 5f, 10f, 30f };

    private const float WeatherFadeSeconds = 3f;

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Time and weather");
        if (!GameClock.IsAvailable)
        {
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }

        ui.Live(() => GameClock.IsAvailable ? $"Day {GameClock.Day}, {GameClock.TimeText}" : "");
        BuildHour(ui, ctx);
        BuildSpeed(ui, ctx);
        BuildWeather(ui, ctx);
    }

    private static void BuildHour(UiBuilder ui, CheatContext ctx)
    {
        int hour = GameClock.Hour;
        ui.Slider("Hour", 0, 23, hour, v => hour = (int)v, "0", wholeNumbers: true);
        ui.Button("Set the time", () => ctx.Run("Set time", () =>
        {
            GameClock.SetTime(hour, 0);
            ctx.Say($"It is {hour:00}:00.");
        }));
    }

    private static void BuildSpeed(UiBuilder ui, CheatContext ctx)
    {
        ui.Cycle("Time speed", SpeedNames, NearestSpeed(GameClock.TimeSpeed), i => ctx.Run("Time speed", () =>
        {
            GameClock.TimeSpeed = SpeedValues[i];
            ctx.Say(SpeedValues[i] == 0f ? "Time is stopped." : $"Time runs at {SpeedNames[i].ToLowerInvariant()} speed.");
        }));
    }

    private static void BuildWeather(UiBuilder ui, CheatContext ctx)
    {
        if (!GameWeather.IsAvailable)
        {
            ui.Label("There is no weather here.", muted: true);
            return;
        }

        string[] names = GameWeather.Names().ToArray();
        int current = Math.Max(0, GameWeather.CurrentIndex);
        ui.Cycle("Weather", names, current, i => ctx.Run("Weather", () =>
        {
            GameWeather.Set(i, WeatherFadeSeconds);
            ctx.Say($"Weather changing to {names[i]}.");
        }));
    }

    private static int NearestSpeed(float speed)
    {
        int best = 0;
        for (int i = 1; i < SpeedValues.Length; i++)
        {
            if (Math.Abs(SpeedValues[i] - speed) < Math.Abs(SpeedValues[best] - speed))
                best = i;
        }
        return best;
    }
}
