// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player.Survival;

/// <summary>The needs whose rate of change mods can scale.</summary>
public enum PlayerDrain
{
    Hunger,
    Thirst,
    /// <summary>The game's hygiene rate is negative (the player gets dirtier), so a multiplier below 1 slows that.</summary>
    Hygiene,
    AlcoholNeed,
    MushroomNeed,
    SmokingNeed,
    GamblingNeed,
}

/// <summary>One mod's adjustment to one drain, as listed by <see cref="StatDrain.Active"/>.</summary>
public sealed class DrainEntry
{
    internal DrainEntry(string ownerId, PlayerDrain drain, string key)
    {
        OwnerId = ownerId;
        Drain = drain;
        Key = key;
    }

    public string OwnerId { get; }
    public PlayerDrain Drain { get; }

    /// <summary>Tells apart several adjustments from the same mod; empty when the mod only has one.</summary>
    public string Key { get; }

    public float Multiplier { get; internal set; }
    public float OffsetPerHour { get; internal set; }
}

/// <summary>
/// Scales how fast hunger, thirst, hygiene and the addiction cravings change. Every mod sets its own
/// multiplier and they combine: two mods that each halve hunger make it a quarter, and when one mod
/// is removed (or unloads) the other's effect remains. The game's rate (already adjusted for the
/// difficulty) is multiplied by the product of all multipliers, then the offsets are added.
/// </summary>
public static class StatDrain
{
    private static readonly int Count = Enum.GetValues(typeof(PlayerDrain)).Length;
    private static readonly List<DrainEntry> Entries = new List<DrainEntry>();
    private static readonly float[] Multipliers = NewMultipliers();
    private static readonly float[] Offsets = new float[Count];

    /// <summary>
    /// Sets this mod's adjustment of a drain. The same owner, drain and <paramref name="key"/> again
    /// replaces the earlier call. Dispose the result (or let the mod unload) to take the adjustment away.
    /// </summary>
    /// <param name="multiplier">0 stops the drain, 1 changes nothing, 2 doubles it.</param>
    /// <param name="offsetPerHour">Added to the rate afterwards, in the game's units per in-game hour.</param>
    /// <param name="key">Only needed when one mod sets the same drain more than once.</param>
    public static IDisposable Set(ObenMod owner, PlayerDrain drain, float multiplier, float offsetPerHour = 0f, string key = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (!(multiplier >= 0f) || float.IsInfinity(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), "The multiplier must be 0 or more.");
        if (float.IsNaN(offsetPerHour) || float.IsInfinity(offsetPerHour))
            throw new ArgumentOutOfRangeException(nameof(offsetPerHour), "The offset must be a number.");

        var entry = Find(owner.Info.Id, drain, key ?? "");
        if (entry == null)
        {
            entry = new DrainEntry(owner.Info.Id, drain, key ?? "");
            Entries.Add(entry);
        }
        entry.Multiplier = multiplier;
        entry.OffsetPerHour = offsetPerHour;
        Recompute(drain);

        return owner.Track(Disposable.Create(() => Remove(entry)));
    }

    /// <summary>Takes this mod's adjustment away. False when it had none.</summary>
    public static bool Remove(ObenMod owner, PlayerDrain drain, string key = null)
    {
        var entry = Find(owner.Info.Id, drain, key ?? "");
        return entry != null && Remove(entry);
    }

    /// <summary>The adjustments in effect, in the order they were set.</summary>
    public static IReadOnlyList<DrainEntry> Active => Entries.ToArray();

    /// <summary>All multipliers on a drain multiplied together (1 when none).</summary>
    public static float Multiplier(PlayerDrain drain) => Multipliers[(int)drain];

    /// <summary>All offsets on a drain added together (0 when none).</summary>
    public static float Offset(PlayerDrain drain) => Offsets[(int)drain];

    /// <summary>What the game's <paramref name="rate"/> becomes with every adjustment applied.</summary>
    public static float Apply(PlayerDrain drain, float rate)
    {
        float result = rate * Multipliers[(int)drain] + Offsets[(int)drain];
        // A craving has no negative; it just stays at zero.
        return result < 0f && drain >= PlayerDrain.AlcoholNeed ? 0f : result;
    }

    internal static void Init(ObenMod framework) => framework.Track(Disposable.Create(() =>
    {
        Entries.Clear();
        for (int i = 0; i < Count; i++)
        {
            Multipliers[i] = 1f;
            Offsets[i] = 0f;
        }
    }));

    private static bool Remove(DrainEntry entry)
    {
        if (!Entries.Remove(entry))
            return false;
        Recompute(entry.Drain);
        return true;
    }

    private static DrainEntry Find(string ownerId, PlayerDrain drain, string key)
    {
        foreach (var entry in Entries)
        {
            if (entry.Drain == drain && entry.OwnerId == ownerId && entry.Key == key)
                return entry;
        }
        return null;
    }

    private static void Recompute(PlayerDrain drain)
    {
        float product = 1f;
        float offset = 0f;
        foreach (var entry in Entries)
        {
            if (entry.Drain != drain)
                continue;
            product *= entry.Multiplier;
            offset += entry.OffsetPerHour;
        }
        Multipliers[(int)drain] = product;
        Offsets[(int)drain] = offset;
    }

    private static float[] NewMultipliers()
    {
        var all = new float[Count];
        for (int i = 0; i < all.Length; i++)
            all[i] = 1f;
        return all;
    }
}
