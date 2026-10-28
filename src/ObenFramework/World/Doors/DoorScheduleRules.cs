using System;
using System.Collections.Generic;

namespace ObenFramework.World;

/// <summary>Opening hours of one day: open from <see cref="Opens"/> until <see cref="Closes"/> (whole hours, 0 to 24).</summary>
public readonly struct OpeningHours : IEquatable<OpeningHours>
{
    public OpeningHours(DayOfWeek day, int opens, int closes)
    {
        Day = day;
        Opens = opens;
        Closes = closes;
    }

    public DayOfWeek Day { get; }

    /// <summary>The hour the doors unlock, 0 to 23.</summary>
    public int Opens { get; }

    /// <summary>The hour they lock again, after <see cref="Opens"/> and at most 24 (midnight).</summary>
    public int Closes { get; }

    public bool Equals(OpeningHours other) => Day == other.Day && Opens == other.Opens && Closes == other.Closes;

    public override bool Equals(object obj) => obj is OpeningHours other && Equals(other);

    public override int GetHashCode() => ((int)Day * 31 + Opens) * 31 + Closes;

    public override string ToString() => $"{Day} {Opens:00}-{Closes:00}";
}

/// <summary>
/// The rules of the game's door schedules without Unity: its table stores one entry per opening as (day, opens,
/// closes) with day 0 for Monday, and a door is open while the hour is at or past the opening and before the
/// closing. Kept apart so the conversions are covered by tests.
/// </summary>
public static class DoorScheduleRules
{
    private const int Days = 7;

    /// <summary>The game's day number (0 = Monday) for a <see cref="DayOfWeek"/>.</summary>
    public static int ToGameDay(DayOfWeek day) => ((int)day + Days - 1) % Days;

    public static DayOfWeek FromGameDay(int gameDay) => (DayOfWeek)((gameDay % Days + Days + 1) % Days);

    /// <summary>Why a list of hours can't be used, or null when it is fine. An empty list means always closed.</summary>
    public static string Check(IList<OpeningHours> hours)
    {
        if (hours == null)
            return "No hours given.";
        foreach (var entry in hours)
        {
            if (!Enum.IsDefined(typeof(DayOfWeek), entry.Day))
                return $"{entry.Day} is not a day.";
            if (entry.Opens < 0 || entry.Opens > 23)
                return $"{entry}: the opening hour must be 0 to 23.";
            if (entry.Closes <= entry.Opens || entry.Closes > 24)
                return $"{entry}: the closing hour must be after the opening hour and at most 24.";
        }
        return null;
    }

    /// <summary>The game's table entries (day, opens, closes) for a list of hours. Throws on an invalid list.</summary>
    public static List<(int Day, int Opens, int Closes)> ToGame(IList<OpeningHours> hours)
    {
        string problem = Check(hours);
        if (problem != null)
            throw new ArgumentException(problem, nameof(hours));
        var table = new List<(int, int, int)>(hours.Count);
        foreach (var entry in hours)
            table.Add((ToGameDay(entry.Day), entry.Opens, entry.Closes));
        return table;
    }

    /// <summary>Hours from the game's table; entries with a day the game never has are skipped.</summary>
    public static List<OpeningHours> FromGame(IEnumerable<(int Day, int Opens, int Closes)> table)
    {
        var hours = new List<OpeningHours>();
        if (table == null)
            return hours;
        foreach (var (day, opens, closes) in table)
        {
            if (day >= 0 && day < Days)
                hours.Add(new OpeningHours(FromGameDay(day), opens, closes));
        }
        return hours;
    }

    /// <summary>Whether a table has the doors open at a game day (0 = Monday) and hour, the way the game decides it.</summary>
    public static bool IsOpen(IEnumerable<(int Day, int Opens, int Closes)> table, int gameDay, float hour)
    {
        if (table == null)
            return false;
        foreach (var (day, opens, closes) in table)
        {
            if (day == gameDay && hour >= opens && hour < closes)
                return true;
        }
        return false;
    }

    /// <summary>The same hours every day of the week.</summary>
    public static List<OpeningHours> EveryDay(int opens, int closes)
    {
        var hours = new List<OpeningHours>(Days);
        for (int day = 0; day < Days; day++)
            hours.Add(new OpeningHours((DayOfWeek)day, opens, closes));
        return hours;
    }
}
