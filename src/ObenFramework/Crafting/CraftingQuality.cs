// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader.Runtime;

namespace ObenFramework.Crafting;

/// <summary>
/// Scales the quality of what stations make. A factor of 1.2 makes crafted items 20% better, keeping the
/// result between <see cref="MinQuality"/> and <see cref="MaxQuality"/> (the game's own range). The key works
/// like <see cref="CraftingSpeed"/>'s: <see cref="AllStations"/>, a station class or a station name. Recipes
/// with a fixed quality and items whose quality is fixed are left alone. Factors from several mods multiply
/// and are removed when their mod unloads.
/// </summary>
public static class CraftingQuality
{
    /// <summary>
    /// The framework's crafting hooks are in place. False when they failed to apply (a game update changed what
    /// they patch); the events then stay quiet and what this class changes may not take.
    /// </summary>
    public static bool IsAvailable => CraftingModule.HooksInPlace;

    public const string AllStations = FactorTable.All;
    public const float MinQuality = 0.05f;
    public const float MaxQuality = 2f;
    public const float MaxFactor = 10f;

    private static readonly OwnedFactors Factors = new OwnedFactors();

    /// <summary>Sets <paramref name="owner"/>'s quality factor for a key, replacing its earlier one for that key.</summary>
    public static void Set(ITracker owner, string key, float factor)
    {
        if (float.IsNaN(factor) || factor <= 0f || factor > MaxFactor)
            throw new ArgumentOutOfRangeException(nameof(factor), $"The factor must be above 0 and at most {MaxFactor}.");
        Factors.Set(owner, key, factor);
    }

    public static bool Remove(ITracker owner, string key) => Factors.Remove(owner, key);

    public static void Clear(ITracker owner) => Factors.Clear(owner);

    /// <summary>The combined factor for a station.</summary>
    public static float For(CraftingBase station) =>
        station == null || !Factors.Table.Any ? 1f : Factors.Table.Combined(0f, MaxFactor, station.GetType().Name, station.ManuName);

    /// <summary>A quality after the station's factor, within the game's range.</summary>
    public static float Adjust(CraftingBase station, float quality)
    {
        float factor = For(station);
        return factor == 1f ? quality : QualityMath.Scale(quality, factor, MinQuality, MaxQuality);
    }

    /// <summary>Every factor set now, for listing: owner, key and factor.</summary>
    public static List<(object Owner, string Key, float Factor)> Entries() => Factors.Table.Entries();
}
