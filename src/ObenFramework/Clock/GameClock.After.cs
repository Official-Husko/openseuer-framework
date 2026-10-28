// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader.Runtime;

namespace ObenFramework.Clock;

public static partial class GameClock
{
    internal static readonly MinuteCountdowns Countdowns = new MinuteCountdowns();

    private static readonly HashSet<ITracker> CountdownOwners = new HashSet<ITracker>();

    /// <summary>How many <see cref="After"/> actions are waiting.</summary>
    public static int WaitingCount => Countdowns.Count;

    /// <summary>
    /// Runs <paramref name="action"/> once <paramref name="minutes"/> in-game minutes have passed (sleeping
    /// and waiting count, frozen time doesn't). Dispose the result to cancel. Everything a mod scheduled is
    /// dropped when it unloads, when a different game is loaded or a new one started, and at the main
    /// menu; level changes keep it. The action runs in its own try/catch.
    /// </summary>
    public static IDisposable After(ITracker owner, int minutes, Action action)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var entry = Countdowns.Add(owner, minutes, action);
        if (CountdownOwners.Add(owner))
        {
            owner.Track(Disposable.Create(() =>
            {
                CountdownOwners.Remove(owner);
                Countdowns.CancelAll(owner);
                ClockHooks.Refresh();
            }));
        }
        ClockHooks.Refresh();
        return Disposable.Create(() =>
        {
            if (Countdowns.Cancel(entry))
                ClockHooks.Refresh();
        });
    }

    /// <summary>Called by the minute hook.</summary>
    internal static void TickCountdowns() =>
        Countdowns.Tick(e => GameEvents.Log?.Error("A GameClock.After action threw", e));

    /// <summary>Drops every waiting action: the game they belonged to is gone.</summary>
    internal static void ClearCountdowns()
    {
        if (Countdowns.Count == 0)
            return;
        Countdowns.Clear();
        ClockHooks.Refresh();
    }

    /// <summary>
    /// Sets the calendar date, keeping the time of day. Year 1 to 9999, month 1 to 12, day 1 to the
    /// month's length. <see cref="Day"/> (days since the game began) doesn't change.
    /// </summary>
    public static void SetDate(int year, int month, int day)
    {
        if (year < 1 || year > 9999)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be between 1 and 9999.");
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        int days = DateTime.DaysInMonth(year, month);
        if (day < 1 || day > days)
            throw new ArgumentOutOfRangeException(nameof(day), $"Day must be between 1 and {days} for that month.");
        Azure.SetDate(year, month, day);
    }

    /// <summary>The whole date and time read at once.</summary>
    public static ClockTime Now =>
        new ClockTime(Year, Month, DayOfMonth, Day, Weekday, Hour, Minute);
}
