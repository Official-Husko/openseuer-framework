namespace ObenUIKit.Menu.Settings;

/// <summary>Putting a number from a config file back inside its slider's range. No Unity types, so it is unit tested.</summary>
internal static class RangeFix
{
    /// <summary>
    /// <paramref name="value"/> clamped to <paramref name="min"/>..<paramref name="max"/>; something that isn't a
    /// number (hand-edited "NaN" or "Infinity") becomes <paramref name="fallback"/>, clamped as well.
    /// </summary>
    public static double Fix(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            value = double.IsNaN(fallback) || double.IsInfinity(fallback) ? min : fallback;
        return value < min ? min : value > max ? max : value;
    }
}
