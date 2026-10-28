// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Economy;

/// <summary>
/// The opening-hours rule of the game's <c>OpeningTimes</c>, without Unity types: open from the opening hour
/// up to (not including) the closing hour; a closing hour before the opening hour runs past midnight; equal
/// hours mean closed all day.
/// </summary>
internal static class ShopHoursMath
{
    public static bool IsOpenAt(int opens, int closes, float hour)
    {
        if (opens == closes)
            return false;
        if (opens > closes)
            return hour >= opens || hour < closes;
        return hour >= opens && hour < closes;
    }

    /// <summary>Hours open in a day (0 to 24).</summary>
    public static int HoursOpen(int opens, int closes)
    {
        if (opens == closes)
            return 0;
        return opens < closes ? closes - opens : 24 - opens + closes;
    }

    /// <summary>The game's weekday index (0 is Monday) for a <see cref="System.DayOfWeek"/>.</summary>
    public static int GameWeekday(System.DayOfWeek day) => ((int)day + 6) % 7;
}
