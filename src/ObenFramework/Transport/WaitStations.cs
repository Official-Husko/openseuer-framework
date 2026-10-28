// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Transport;

/// <summary>A wait station and what waiting there does.</summary>
public readonly struct WaitStationInfo
{
    public WaitStationInfo(InteractableWait station, string id, WaitingController.Type kind, int minutes, bool ignoresNeeds, bool canWait,
        bool hunger, bool thirst, bool bladder, bool bowel, bool tiredness, bool hygiene)
    {
        Station = station;
        Id = id;
        Kind = kind;
        Minutes = minutes;
        IgnoresNeeds = ignoresNeeds;
        CanWait = canWait;
        SatisfiesHunger = hunger;
        SatisfiesThirst = thirst;
        SatisfiesBladder = bladder;
        SatisfiesBowel = bowel;
        SatisfiesTiredness = tiredness;
        SatisfiesHygiene = hygiene;
    }

    public InteractableWait Station { get; }
    public string Id { get; }

    /// <summary>What the player does there: waiting, working, having fun, reading, watching TV, ...</summary>
    public WaitingController.Type Kind { get; }

    /// <summary>How long one use lasts, in game minutes.</summary>
    public int Minutes { get; }

    /// <summary>The wait goes on even when a need runs low.</summary>
    public bool IgnoresNeeds { get; }

    /// <summary>The player could use it now (needs and items allowing).</summary>
    public bool CanWait { get; }

    public bool SatisfiesHunger { get; }
    public bool SatisfiesThirst { get; }
    public bool SatisfiesBladder { get; }
    public bool SatisfiesBowel { get; }
    public bool SatisfiesTiredness { get; }
    public bool SatisfiesHygiene { get; }
}

/// <summary>A wait at a station ended.</summary>
public readonly struct WaitStationDone
{
    public WaitStationDone(InteractableWait station, int seconds)
    {
        Station = station;
        Seconds = seconds;
    }

    public InteractableWait Station { get; }

    /// <summary>Game seconds waited.</summary>
    public int Seconds { get; }

    public string Id => WaitStations.IdOf(Station);
}

/// <summary>
/// Wait stations: the benches, desks, TVs, games and jobs where the player passes time (<c>InteractableWait</c>).
/// List them, use one from code, and hear when a wait there ends. Waits and sleep in general are in
/// <c>PlayerSleep</c>.
/// </summary>
public static class WaitStations
{
    private static readonly AccessTools.FieldRef<InteractableWait, int> MinutesField = AccessTools.FieldRefAccess<InteractableWait, int>("WaitInMinutes");
    private static readonly AccessTools.FieldRef<InteractableWait, WaitingController.Type> TypeField =
        AccessTools.FieldRefAccess<InteractableWait, WaitingController.Type>("type");
    private static readonly AccessTools.FieldRef<InteractableWait, bool> IgnoresField = AccessTools.FieldRefAccess<InteractableWait, bool>("ignorePlayerNeeds");

    public static readonly WorldEvent<WaitStationDone> Finished = new WorldEvent<WaitStationDone>(nameof(Finished));

    public static List<InteractableWait> All() => ObjectIds.Active<InteractableWait>();

    public static InteractableWait Find(string id) => ObjectIds.Find<InteractableWait>(id);

    public static string IdOf(InteractableWait station) => ObjectIds.IdOf(station);

    public static WaitStationInfo Describe(InteractableWait station)
    {
        if (station == null)
            throw new ArgumentNullException(nameof(station));
        bool canWait = PlayerStats.instance != null && station.CanWait();
        return new WaitStationInfo(station, IdOf(station), TypeField(station), MinutesField(station), IgnoresField(station), canWait,
            station.satisfyHunger, station.satisfyThirst, station.satisfyBladder, station.satisfyBowel, station.satisfyTiredness, station.satisfyHygiene);
    }

    /// <summary>Whether the player could use the station now, and why not.</summary>
    public static bool CanUse(InteractableWait station, out string reason)
    {
        reason = null;
        var waiting = WaitingController.instance;
        if (station == null)
            reason = "no station";
        else if (SaveGate.IsBusy)
            reason = "the game is saving or loading";
        else if (waiting == null || PlayerStats.instance == null || GameController.instance == null || UseText.instance == null)
            reason = "not in a level";
        else if (!waiting.CanStartWaitingStaus || WaitingController.WaitingBlocked || waiting.IsSleeping || waiting.IsWaiting)
            reason = "the player is already waiting or can't wait now";
        else if (station.IsInteractDisabled())
            reason = "the station is disabled";
        else if (!station.CanWait())
            reason = "the player's needs or items don't allow it";
        return reason == null;
    }

    /// <summary>Uses the station as if the player had. False when it can't be used now (see <see cref="CanUse"/>).</summary>
    public static bool Use(InteractableWait station)
    {
        if (!CanUse(station, out _))
            return false;
        using (WorldChanges.EnterMod())
            station.Interact();
        return true;
    }

    /// <summary>Changes how long one use lasts (game minutes). Not saved: the level's value returns when it loads.</summary>
    public static void SetMinutes(InteractableWait station, int minutes)
    {
        if (station == null)
            throw new ArgumentNullException(nameof(station));
        if (minutes < 1 || minutes > 24 * 60)
            throw new ArgumentOutOfRangeException(nameof(minutes), "1 minute to 24 hours.");
        MinutesField(station) = minutes;
    }
}

[HarmonyPatch(typeof(InteractableWait), nameof(InteractableWait.WaitDone))]
internal static class WaitStationPatch
{
    [HarmonyPostfix]
    private static void After(InteractableWait __instance, int timeWaited)
    {
        try
        {
            if (timeWaited > 0 && WaitStations.Finished.HasSubscribers && WorldChanges.Active)
                WaitStations.Finished.Raise(new WaitStationDone(__instance, timeWaited));
        }
        catch (Exception e) { TransportModule.Log.Error("Wait station hook", e); }
    }
}
