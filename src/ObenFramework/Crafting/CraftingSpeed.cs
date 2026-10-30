// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader.Runtime;

namespace ObenFramework.Crafting;

/// <summary>
/// Makes stations craft faster or slower. A speed of 2 halves the crafting time, 0.5 doubles it. The key is
/// <see cref="AllStations"/>, a station class (<c>manufacturing</c>, <c>manufacturingProcess</c>,
/// <c>Dishwasher</c>) or the name a station shows in its window (<c>Stove</c>). Speeds from several mods
/// multiply and are removed when their mod unloads. A craft keeps the speed it started with; the windows show
/// the changed time. Plants in grow boxes grow at their own pace (see <see cref="Plants"/>).
/// </summary>
public static class CraftingSpeed
{
    /// <summary>
    /// The framework's crafting hooks are in place. False when they failed to apply (a game update changed what
    /// they patch); the events then stay quiet and what this class changes may not take.
    /// </summary>
    public static bool IsAvailable => CraftingModule.HooksInPlace;

    public const string AllStations = FactorTable.All;
    public const float MinSpeed = 0.01f;
    public const float MaxSpeed = 100f;

    private static readonly OwnedFactors Speeds = new OwnedFactors();

    /// <summary>Sets <paramref name="owner"/>'s speed for a key, replacing its earlier one for that key.</summary>
    public static void Set(ITracker owner, string key, float speed)
    {
        if (float.IsNaN(speed) || speed < MinSpeed || speed > MaxSpeed)
            throw new ArgumentOutOfRangeException(nameof(speed), $"Speed must be {MinSpeed} to {MaxSpeed}.");
        Speeds.Set(owner, key, speed);
    }

    /// <summary>Removes one of the owner's speeds. True when there was one.</summary>
    public static bool Remove(ITracker owner, string key) => Speeds.Remove(owner, key);

    /// <summary>Removes all of the owner's speeds.</summary>
    public static void Clear(ITracker owner) => Speeds.Clear(owner);

    /// <summary>The combined speed for a station: all stations, its class and its name.</summary>
    public static float For(CraftingBase station) =>
        station == null || !Speeds.Table.Any ? 1f : Speeds.Table.Combined(MinSpeed, MaxSpeed, station.GetType().Name, station.ManuName);

    /// <summary>Every speed set now, for listing: owner, key and speed.</summary>
    public static List<(object Owner, string Key, float Factor)> Entries() => Speeds.Table.Entries();
}
