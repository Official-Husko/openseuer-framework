using System;

namespace GameTweaks;

/// <summary>
/// The arithmetic behind the tweaks: reading a factor from the config safely and applying it to the
/// game's stack sizes, prices, hours and durations. No Unity types, so it is unit tested on its own.
/// </summary>
public static class TweakMath
{
    /// <summary>
    /// A factor from the config file, which the player may have edited by hand: NaN or infinity become
    /// <paramref name="fallback"/>, everything else is clamped to <paramref name="min"/>..<paramref name="max"/>.
    /// </summary>
    public static float Factor(float value, float min, float max, float fallback = 1f)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;
        return value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// A stack size scaled by <paramref name="factor"/>. Items that don't stack (1 or less) stay as they
    /// are, and a stack never drops below 1.
    /// </summary>
    public static int Stack(int stack, float factor)
    {
        if (stack <= 1 || factor == 1f)
            return stack;
        double scaled = Math.Round(stack * (double)factor, MidpointRounding.AwayFromZero);
        return scaled < 1d ? 1 : scaled > int.MaxValue ? int.MaxValue : (int)scaled;
    }

    /// <summary>A price scaled by <paramref name="factor"/>, rounded to the nearest whole coin, never negative.</summary>
    public static int Price(int price, float factor)
    {
        if (factor == 1f)
            return price;
        double scaled = Math.Round(price * (double)factor, MidpointRounding.AwayFromZero);
        return scaled < 0d ? 0 : scaled > int.MaxValue ? int.MaxValue : (int)scaled;
    }

    /// <summary>
    /// Whole hours scaled by <paramref name="factor"/> and rounded up, so a short job never shows as
    /// taking no time unless it really takes none.
    /// </summary>
    public static int Hours(int hours, float factor)
    {
        if (factor == 1f || hours <= 0)
            return hours;
        double scaled = Math.Ceiling(hours * (double)factor);
        return scaled > int.MaxValue ? int.MaxValue : (int)scaled;
    }

    /// <summary>
    /// The hours a renovation order starts with. Story-scripted orders keep theirs; an instant build
    /// starts at zero; otherwise the time is scaled.
    /// </summary>
    public static float OrderHours(float hours, float factor, bool instant, bool scripted)
    {
        if (scripted)
            return hours;
        if (instant)
            return 0f;
        return hours * factor;
    }

    /// <summary>
    /// A duration when things run <paramref name="speed"/> times as fast. A speed of 0 or less (or not a
    /// number) leaves the duration alone rather than dividing by it.
    /// </summary>
    public static float Faster(float duration, float speed)
    {
        if (!(speed > 0f) || speed == 1f)
            return duration;
        return duration / speed;
    }

    /// <summary>Minutes of spoiling scaled by <paramref name="factor"/> (0 stops spoiling).</summary>
    public static float Spoil(float minutes, float factor) => factor == 1f ? minutes : minutes * factor;
}
