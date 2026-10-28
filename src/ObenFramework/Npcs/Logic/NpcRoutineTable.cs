// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Npcs;

/// <summary>Days of the week a routine step runs on. Combine them: <c>NpcDays.Monday | NpcDays.Friday</c>.</summary>
[Flags]
public enum NpcDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    Every = Weekdays | Weekend,
}

/// <summary>A span of minutes on some days of the week. <see cref="To"/> is exclusive.</summary>
internal struct NpcWindow
{
    public int Days;
    public int From;
    public int To;
}

/// <summary>What a routine should do with its NPC in the scene the player is in.</summary>
internal enum NpcPresence
{
    /// <summary>Nothing: the NPC is elsewhere and not here either.</summary>
    None,

    /// <summary>The step is in this scene and the NPC is not here yet.</summary>
    Spawn,

    /// <summary>The step is in this scene and the NPC is here.</summary>
    Keep,

    /// <summary>The NPC is here (because the routine put it here) but its step is elsewhere.</summary>
    Leave,
}

/// <summary>
/// The time maths behind NPC routines: week minutes counted from Monday 00:00, windows that may run past
/// midnight, and which window covers a moment. No Unity types, so it is unit tested.
/// </summary>
internal static class NpcRoutineTable
{
    public const int MinutesPerDay = 1440;
    public const int MinutesPerWeek = MinutesPerDay * 7;
    public const int EveryDay = (int)NpcDays.Every;

    /// <summary>Minutes since Monday 00:00 for a weekday as the game counts it (0 Monday to 6 Sunday).</summary>
    public static int WeekMinute(int weekdayIndex, int hour, int minute) =>
        ((weekdayIndex % 7 + 7) % 7) * MinutesPerDay + hour * 60 + minute;

    /// <summary>Minutes since Monday 00:00 for a <see cref="DayOfWeek"/>.</summary>
    public static int WeekMinute(DayOfWeek day, int hour, int minute) => WeekMinute(((int)day + 6) % 7, hour, minute);

    /// <summary>Null when the step's days and times make sense, else the reason they don't.</summary>
    public static string Check(int days, int from, int to)
    {
        if (days <= 0 || days > EveryDay)
            return "days must name at least one day from Monday to Sunday";
        if (from < 0 || from >= MinutesPerDay)
            return "from must be 00:00 to 23:59";
        if (to <= 0 || to > MinutesPerDay)
            return "to must be 00:01 to 24:00";
        if (from == to)
            return "from and to must differ";
        return null;
    }

    /// <summary>
    /// The windows of a step. One that ends before it starts runs past midnight and becomes two: the evening
    /// on its own days and the early hours of the day after each of them.
    /// </summary>
    public static NpcWindow[] Windows(int days, int from, int to)
    {
        if (to > from)
            return new[] { new NpcWindow { Days = days, From = from, To = to } };
        return new[]
        {
            new NpcWindow { Days = days, From = from, To = MinutesPerDay },
            new NpcWindow { Days = NextDays(days), From = 0, To = to },
        };
    }

    public static bool Covers(NpcWindow window, int weekMinute)
    {
        int day = weekMinute / MinutesPerDay;
        int minute = weekMinute % MinutesPerDay;
        return (window.Days & (1 << day)) != 0 && minute >= window.From && minute < window.To;
    }

    /// <summary>Index of the first window covering the moment, or -1. Earlier steps win where they overlap.</summary>
    public static int ActiveIndex(IList<NpcWindow> windows, int weekMinute)
    {
        for (int i = 0; i < windows.Count; i++)
        {
            if (Covers(windows[i], weekMinute))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// What to do with the NPC given the scene its step is in (null when it has none right now), the scene
    /// the player is in, whether the NPC is here and whether the routine put it here. An NPC the routine did
    /// not spawn is never taken away.
    /// </summary>
    public static NpcPresence Presence(string stepScene, string activeScene, bool here, bool spawnedByRoutine)
    {
        if (stepScene != null && stepScene == activeScene)
            return here ? NpcPresence.Keep : NpcPresence.Spawn;
        return here && spawnedByRoutine ? NpcPresence.Leave : NpcPresence.None;
    }

    // Each day moved one later, Sunday wrapping to Monday.
    private static int NextDays(int days) => ((days << 1) | (days >> 6)) & EveryDay;
}
