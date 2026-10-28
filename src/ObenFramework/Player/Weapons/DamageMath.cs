using System;

namespace ObenFramework.Player;

/// <summary>Scaling whole-number hit damage by a multiplier. No Unity types, so it is unit tested.</summary>
internal static class DamageMath
{
    /// <summary>
    /// <paramref name="damage"/> times <paramref name="multiplier"/>, rounded so the average comes out right:
    /// the fraction is the chance to round up, decided by <paramref name="roll"/> (0 to 1). A pickaxe hit of
    /// 1 at 1.5x is 1 or 2, half the time each. Never below 0.
    /// </summary>
    public static int Scale(int damage, float multiplier, float roll)
    {
        if (damage <= 0 || multiplier == 1f)
            return Math.Max(0, damage);
        if (!(multiplier > 0f))
            return 0;

        double exact = damage * (double)multiplier;
        if (exact >= int.MaxValue)
            return int.MaxValue;
        int whole = (int)Math.Floor(exact);
        return roll < exact - whole ? whole + 1 : whole;
    }
}
