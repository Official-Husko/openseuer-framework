using System;
using ObenLoader;

namespace ObenFramework.Clock;

/// <summary>
/// The in-game clock and calendar. The game only has one while a gameplay scene is loaded, so check
/// <see cref="IsAvailable"/> before reading it from code that can run at the main menu; the members
/// throw <see cref="InvalidOperationException"/> when there is no clock.
/// </summary>
public static partial class GameClock
{
    /// <summary>
    /// A new in-game minute started, payload is the minute (0-59). Roughly once per real second at normal
    /// speed. Time jumps and sleeping also raise it.
    /// </summary>
    public static readonly ClockEvent MinutePassed = new ClockEvent(nameof(MinutePassed), ClockHooks.Refresh);

    /// <summary>The hour changed, payload is the new hour (0-23). Derived from the minute tick.</summary>
    public static readonly ClockEvent HourChanged = new ClockEvent(nameof(HourChanged), ClockHooks.Refresh);

    /// <summary>
    /// A new in-game day started, payload is the game's day counter (days since the game began). Raised by the
    /// game's day rollover, so it also fires when the player sleeps through midnight.
    /// </summary>
    public static readonly ClockEvent DayChanged = new ClockEvent(nameof(DayChanged), ClockHooks.Refresh);

    /// <summary>
    /// An in-game second passed, payload is how many seconds went by. This is the game's per-frame event
    /// (60 in-game seconds per real second at normal speed); the game only gets a handler for it while
    /// somebody is subscribed here, and a handler must be trivial.
    /// </summary>
    public static readonly ClockEvent SecondPassed = new ClockEvent(nameof(SecondPassed), ClockHooks.Refresh);

    /// <summary>True when the clock exists (a gameplay scene is loaded).</summary>
    public static bool IsAvailable => TimeOfDayAzure.instance != null && TimeOfDayAzure.instance.TimeController != null;

    /// <summary>Time of day in hours, 0 to 24, with the minutes as a fraction (13.5 is 13:30).</summary>
    public static float TimeOfDay => Azure.CurrentTimelineValue;

    /// <summary>Hour of the day, 0-23.</summary>
    public static int Hour => (int)Azure.CurrentHours;

    /// <summary>Minute of the hour, 0-59.</summary>
    public static int Minute => (int)Azure.CurrentMinutes;

    /// <summary>
    /// Days since the game began (the game's day counter, also the Lua variable "Day"). Grows by one at every
    /// midnight and is part of the save.
    /// </summary>
    public static int Day => Azure.dayCounter;

    /// <summary>Day of the month in the game's calendar, starting at 1.</summary>
    public static int DayOfMonth => Azure.GetDay();

    public static int Month => Azure.GetMonth();

    public static int Year => Azure.GetYear();

    /// <summary>Day of the week the way the game counts it: 0 is Monday, 6 is Sunday.</summary>
    public static int WeekdayIndex => Azure.GetDayOfWeek();

    /// <summary>The weekday as <see cref="System.DayOfWeek"/>.</summary>
    public static DayOfWeek Weekday => (DayOfWeek)((WeekdayIndex + 1) % 7);

    /// <summary>The game's own definition of day: between 06:00 and 20:00.</summary>
    public static bool IsDaytime => Azure.IsItDay();

    /// <summary>
    /// How fast in-game time runs relative to normal (1 is normal, 0 stops it). Sleeping and waiting set this
    /// and put it back to 1 afterwards, so a value set here doesn't survive them.
    /// </summary>
    public static float TimeSpeed
    {
        get => Azure.timeSpeedOverrideFactor;
        set => Azure.timeSpeedOverrideFactor = Math.Max(0f, value);
    }

    /// <summary>Sets the time of day, keeping the date. Hours 0-24 as a fraction, like the console's change_time.</summary>
    public static void SetTime(float hours)
    {
        if (hours < 0f || hours > 24f)
            throw new ArgumentOutOfRangeException(nameof(hours), "Time must be between 0 and 24.");
        Azure.SetTimeline(hours);
    }

    /// <summary>Sets the time of day, keeping the date.</summary>
    public static void SetTime(int hours, int minutes)
    {
        if (hours < 0 || hours > 24)
            throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be between 0 and 24.");
        if (minutes < 0 || minutes > 59)
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be between 0 and 59.");
        SetTime(hours + minutes / 60f);
    }

    /// <summary>"13:05" for the current time.</summary>
    public static string TimeText => $"{Hour:00}:{Minute:00}";

    /// <summary>Hooks the clock events to the game. Called once by the framework.</summary>
    internal static void Init(ObenMod framework)
    {
        Countdowns.Clear();
        CountdownOwners.Clear();
        ClockHooks.Init(framework);
        // Waiting actions belong to the game they were scheduled in.
        Events.GameEvents.MainMenu.Subscribe(framework, ClearCountdowns);
        NewGames.Sessions.Starting.Subscribe(framework, _ => ClearCountdowns());
        framework.Track(ObenLoader.Runtime.Disposable.Create(() =>
        {
            Countdowns.Clear();
            CountdownOwners.Clear();
        }));
    }

    private static TimeOfDayAzure Azure
    {
        get
        {
            if (!IsAvailable)
                throw new InvalidOperationException("The game clock isn't available: no gameplay scene is loaded.");
            return TimeOfDayAzure.instance;
        }
    }
}
