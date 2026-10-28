using System;

namespace ObenFramework.Clock;

/// <summary>A moment of the in-game calendar, read at once with <see cref="GameClock.Now"/>.</summary>
public readonly struct ClockTime
{
    public readonly int Year;
    public readonly int Month;
    public readonly int DayOfMonth;

    /// <summary>Days since the game began.</summary>
    public readonly int Day;

    public readonly DayOfWeek Weekday;
    public readonly int Hour;
    public readonly int Minute;

    public ClockTime(int year, int month, int dayOfMonth, int day, DayOfWeek weekday, int hour, int minute)
    {
        Year = year;
        Month = month;
        DayOfMonth = dayOfMonth;
        Day = day;
        Weekday = weekday;
        Hour = hour;
        Minute = minute;
    }

    public override string ToString() => $"{Year:0000}-{Month:00}-{DayOfMonth:00} {Hour:00}:{Minute:00} ({Weekday}, day {Day})";
}
