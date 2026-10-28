// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Player;

/// <summary>Arithmetic for waits and the time picker. No Unity types, so it is unit tested on its own.</summary>
internal static class WaitMath
{
    /// <summary>The longest wait or pick, one in-game day.</summary>
    public const int MaxMinutes = 1440;

    /// <summary>Whole minutes in a number of game seconds.</summary>
    public static int MinutesPassed(int seconds) => seconds <= 0 ? 0 : seconds / 60;

    /// <summary>0 &lt;= min &lt;= max &lt;= one day.</summary>
    public static bool ValidRange(int minMinutes, int maxMinutes) =>
        minMinutes >= 0 && maxMinutes >= minMinutes && maxMinutes <= MaxMinutes;

    public static int Clamp(int minutes, int minMinutes, int maxMinutes)
    {
        if (minutes < minMinutes)
            return minMinutes;
        return minutes > maxMinutes ? maxMinutes : minutes;
    }

    /// <summary>The game's clock slider counts seconds; this rounds to the nearest minute.</summary>
    public static int MinutesFromSlider(float seconds) => (int)Math.Floor(seconds / 60f + 0.5f);

    /// <summary>
    /// Rounds <paramref name="minutes"/> to the nearest step counted from <paramref name="minMinutes"/>,
    /// never past the last whole step that fits under <paramref name="maxMinutes"/>.
    /// </summary>
    public static int Snap(int minutes, int minMinutes, int maxMinutes, int stepMinutes)
    {
        if (stepMinutes < 1)
            stepMinutes = 1;
        int top = minMinutes + (maxMinutes - minMinutes) / stepMinutes * stepMinutes;
        int clamped = Clamp(minutes, minMinutes, top);
        return Clamp(minMinutes + (clamped - minMinutes + stepMinutes / 2) / stepMinutes * stepMinutes, minMinutes, top);
    }

    /// <summary>"HH:MM" for a number of minutes, as the game's sleep clock shows it.</summary>
    public static string Clock(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
}
