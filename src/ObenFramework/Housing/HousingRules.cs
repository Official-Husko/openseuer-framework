using System;
using System.Globalization;

namespace ObenFramework.Housing;

/// <summary>An apartment's size. The first four follow the game's <c>UpgradeType.ApartmentType</c>.</summary>
public enum ApartmentSize
{
    Small = 0,
    Medium = 1,
    Large = 2,
    Shop = 3,
    Unknown = 4,
}

/// <summary>Where an apartment's cleaning job is.</summary>
public enum CleaningState
{
    NotStarted,
    Running,
    Done,

    /// <summary>There is no apartment with that number.</summary>
    NoSuchApartment,
}

/// <summary>
/// The plain rules of the housing module: limits on timers, the meaning of the cleaning day, renovation
/// ids. No Unity types, so it is unit tested on its own.
/// </summary>
internal static class HousingRules
{
    /// <summary>The game's own eviction notice, in game hours.</summary>
    public const float DefaultEvictionHours = 18f;

    /// <summary>The game's own move-in time, in game hours.</summary>
    public const float DefaultMoveInHours = 18f;

    public const float MinTimerHours = 1f / 3600f;
    public const float MaxTimerHours = 24f * 365f;

    /// <summary>An eviction or move-in time the game's integer second counters can hold: one game second to a year.</summary>
    public static float ClampTimerHours(float hours)
    {
        if (float.IsNaN(hours) || hours < MinTimerHours)
            return MinTimerHours;
        return hours > MaxTimerHours ? MaxTimerHours : hours;
    }

    public static float HoursOf(int seconds) => seconds / 3600f;

    /// <summary>The game keeps -1 before a cleaning starts, 0 once done, and the day it finishes while it runs.</summary>
    public static CleaningState CleaningOf(int completeDay) =>
        completeDay < 0 ? CleaningState.NotStarted : completeDay == 0 ? CleaningState.Done : CleaningState.Running;

    /// <summary>The game's apartment size number as an <see cref="ApartmentSize"/>.</summary>
    public static ApartmentSize SizeOf(int gameSize) => gameSize >= 0 && gameSize <= 3 ? (ApartmentSize)gameSize : ApartmentSize.Unknown;

    /// <summary>A renovation's id: the apartment number (0 for the building) and the upgrade's id.</summary>
    public static string RenovationId(int apartment, string upgradeId) => apartment.ToString(CultureInfo.InvariantCulture) + "/" + upgradeId;

    public static bool TryParseRenovationId(string id, out int apartment, out string upgradeId)
    {
        apartment = 0;
        upgradeId = null;
        if (string.IsNullOrEmpty(id))
            return false;
        int split = id.IndexOf('/');
        if (split <= 0 || split == id.Length - 1)
            return false;
        if (!int.TryParse(id.Substring(0, split), NumberStyles.Integer, CultureInfo.InvariantCulture, out apartment) || apartment < 0)
            return false;
        upgradeId = id.Substring(split + 1);
        return true;
    }

    /// <summary>A hotel stay: 1 to 7 nights, like the hotel desk offers.</summary>
    public const int MaxHotelNights = 7;

    public static void RequireNights(int nights)
    {
        if (nights < 1 || nights > MaxHotelNights)
            throw new ArgumentOutOfRangeException(nameof(nights), $"1 to {MaxHotelNights} nights.");
    }
}
