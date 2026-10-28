// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Transport;

/// <summary>The player got on or off a ladder.</summary>
public readonly struct LadderClimb
{
    public LadderClimb(InteractableLadder ladder, bool climbing)
    {
        Ladder = ladder;
        Climbing = climbing;
    }

    public InteractableLadder Ladder { get; }

    /// <summary>True when the player got on, false when they got off.</summary>
    public bool Climbing { get; }

    public string Id => Ladders.IdOf(Ladder);
}

/// <summary>
/// Ladders: which ones the scene has, blocking them (a blocked ladder can't be climbed), and when the player
/// gets on and off.
/// </summary>
public static class Ladders
{
    private static readonly AccessTools.FieldRef<InteractableLadder, bool> ClimbingField =
        AccessTools.FieldRefAccess<InteractableLadder, bool>("playerClimbing");
    private static readonly AccessTools.FieldRef<bool> ClimbingAnyField =
        AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(typeof(InteractableLadder), "playerClimbingGlobal"));

    /// <summary>The player got on or off a ladder.</summary>
    public static readonly WorldEvent<LadderClimb> Climbed = new WorldEvent<LadderClimb>(nameof(Climbed));

    /// <summary>The player is on any ladder.</summary>
    public static bool PlayerClimbing => ClimbingAnyField();

    public static List<InteractableLadder> All() => ObjectIds.Active<InteractableLadder>();

    public static InteractableLadder Find(string id) => ObjectIds.Find<InteractableLadder>(id);

    public static string IdOf(InteractableLadder ladder) => ObjectIds.IdOf(ladder);

    /// <summary>The player is on this ladder.</summary>
    public static bool IsPlayerOn(InteractableLadder ladder) => ladder != null && ClimbingField(ladder);

    /// <summary>
    /// Blocks or frees a ladder. A ladder the player is on can't be blocked (false). The game saves the flag
    /// with the level, so it lasts.
    /// </summary>
    public static bool SetBlocked(InteractableLadder ladder, bool blocked)
    {
        if (ladder == null)
            throw new ArgumentNullException(nameof(ladder));
        if (blocked && IsPlayerOn(ladder))
            return false;
        ladder.blocked = blocked;
        return true;
    }

    /// <summary>Takes the player off the ladder they are on, at the nearest exit. False when they are on none.</summary>
    public static bool GetOff()
    {
        foreach (var ladder in All())
        {
            if (IsPlayerOn(ladder))
            {
                ladder.StopClimb();
                return true;
            }
        }
        return false;
    }
}

/// <summary>Reports getting on and off ladders from the game's private climb steps.</summary>
internal static class LadderWatch
{
    [HarmonyPatch(typeof(InteractableLadder), "Climb")]
    private static class ClimbPatch
    {
        [HarmonyPostfix]
        private static void After(InteractableLadder __instance, bool onLoadGame)
        {
            try
            {
                if (!onLoadGame && Ladders.Climbed.HasSubscribers && WorldChanges.Active)
                    Ladders.Climbed.Raise(new LadderClimb(__instance, true));
            }
            catch (Exception e) { TransportModule.Log.Error("Ladder climb hook", e); }
        }
    }

    [HarmonyPatch(typeof(InteractableLadder), nameof(InteractableLadder.StopClimb))]
    private static class StopPatch
    {
        [HarmonyPrefix]
        private static void Before(bool ___playerClimbing, out bool __state) => __state = ___playerClimbing;

        [HarmonyPostfix]
        private static void After(InteractableLadder __instance, bool __state)
        {
            try
            {
                if (__state && Ladders.Climbed.HasSubscribers && WorldChanges.Active)
                    Ladders.Climbed.Raise(new LadderClimb(__instance, false));
            }
            catch (Exception e) { TransportModule.Log.Error("Ladder climb hook", e); }
        }
    }
}
