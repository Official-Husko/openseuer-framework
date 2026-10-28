// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Items;

/// <summary>How a take compared with what was asked for.</summary>
internal enum TakeResult
{
    Exact,
    Short,
    Over,
    Nothing,
}

/// <summary>
/// The checks behind <see cref="PlayerItems.Take"/>: the game's remove call can't be trusted to take exactly
/// what it was asked (a liquid in a bottle shares the id it matches on), so the count before and after is
/// compared. No Unity types, so it is unit tested on its own.
/// </summary>
internal static class TakeMath
{
    public const string LiquidCategory = "Liquid";

    /// <summary>Compares the counts around a take. <paramref name="removed"/> is negative when units appeared.</summary>
    public static TakeResult Account(int requested, int before, int after, out int removed)
    {
        removed = before - after;
        if (removed <= 0)
            return TakeResult.Nothing;
        if (removed == requested)
            return TakeResult.Exact;
        return removed > requested ? TakeResult.Over : TakeResult.Short;
    }

    public static bool IsLiquid(string[] categories) =>
        categories != null && Array.IndexOf(categories, LiquidCategory) >= 0;
}
