// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenLoader.Runtime;

namespace ObenFramework.World;

/// <summary>
/// How long items dropped in the world stay before they disappear. The game's own time is
/// <see cref="GameMinutes"/>; a mod can set its own, which the newest setter wins and which leaves with its mod.
/// It applies to items whose timer starts afterwards (dropped or spawned from then on).
/// </summary>
public static class FloorItemExpiry
{
    /// <summary>The longest time accepted, in game minutes (about two game years).</summary>
    public const int MaxMinutes = 1_000_000;

    private static readonly List<KeyValuePair<object, int>> Overrides = new List<KeyValuePair<object, int>>();

    /// <summary>The game's own expiry time in game minutes, or -1 outside a world.</summary>
    public static int GameMinutes => info_game_logic.instance != null ? info_game_logic.instance.itemExpirationTimeMinutes : -1;

    /// <summary>The time that applies now: the newest override, or null for the game's own.</summary>
    public static int? Current => Overrides.Count > 0 ? Overrides[Overrides.Count - 1].Value : (int?)null;

    /// <summary>Sets the expiry time in game minutes until <paramref name="owner"/> unloads or the returned object is disposed.</summary>
    public static IDisposable Set(ITracker owner, int minutes)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (minutes < 1 || minutes > MaxMinutes)
            throw new ArgumentOutOfRangeException(nameof(minutes), $"Use 1 to {MaxMinutes} game minutes.");

        var entry = new KeyValuePair<object, int>(new object(), minutes);
        Overrides.Add(entry);
        var registration = Disposable.Create(() => Overrides.Remove(entry));
        owner.Track(registration);
        return registration;
    }

    [HarmonyPatch]
    private static class Patches
    {
        // A time passed in is the caller's choice (-1 for items that never expire, an expiration zone's own); leave it.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Collectible), nameof(Collectible.StartExpirationTimer))]
        private static void Starting(ref int? overrideTime)
        {
            if (overrideTime == null && Overrides.Count > 0)
                overrideTime = Overrides[Overrides.Count - 1].Value;
        }
    }
}
