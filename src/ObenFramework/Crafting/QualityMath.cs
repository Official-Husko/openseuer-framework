using System;

namespace ObenFramework.Crafting;

/// <summary>
/// The formulas behind <see cref="CraftingSpeed"/> and <see cref="CraftingQuality"/>. No Unity types, so it is
/// unit tested on its own.
/// </summary>
internal static class QualityMath
{
    /// <summary>The longest process time the game's int timers hold safely, in seconds.</summary>
    public const double MaxProcessSeconds = 2_000_000_000.0;

    /// <summary>A quality times a factor, kept within <paramref name="min"/>..<paramref name="max"/>.</summary>
    public static float Scale(float quality, float factor, float min, float max)
    {
        if (factor == 1f)
            return quality;
        float scaled = quality * factor;
        return scaled < min ? min : scaled > max ? max : scaled;
    }

    /// <summary>
    /// The process time factor a craft gets at <paramref name="speed"/>: the game's own factor divided by the
    /// speed, capped so the base time times the factor still fits the game's timers.
    /// </summary>
    public static float TimeFactor(float gameFactor, float speed, int baseSeconds)
    {
        if (speed == 1f || speed <= 0f || float.IsNaN(speed))
            return gameFactor;
        float factor = gameFactor / speed;
        if (baseSeconds > 0)
            factor = (float)Math.Min(factor, MaxProcessSeconds / baseSeconds);
        return factor;
    }
}
