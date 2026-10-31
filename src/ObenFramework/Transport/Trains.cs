// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Transport;

/// <summary>
/// Trains: the rail vehicles of the level (locomotives, wagons and the player's minitrain), the minitrain's
/// fuel, track segments, switches and wagon loaders. Driving them is in <see cref="TrainActions"/>.
/// <see cref="Moved"/> reports departures, stops and crashes; departures and stops are checked a few times a
/// second, and only while someone listens. Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class Trains
{
    private const float CheckSeconds = 0.25f;

    private static readonly AccessTools.FieldRef<MinitrainSwitch, Vector3[]> BranchRotations =
        AccessTools.FieldRefAccess<MinitrainSwitch, Vector3[]>("branchRotations");
    private static readonly AccessTools.FieldRef<Minitrain, TrackTrain> CabTrain = AccessTools.FieldRefAccess<Minitrain, TrackTrain>("train");

    private static readonly Dictionary<TrackTrain, bool> Moving = new Dictionary<TrackTrain, bool>();
    private static float _nextCheck;

    /// <summary>A train departed, stopped or crashed.</summary>
    public static readonly WorldEvent<TrainEvent> Moved = new WorldEvent<TrainEvent>(nameof(Moved));

    /// <summary>True when the crash hook is in place, so <see cref="Moved"/> reports crashes.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Transport");

    /// <summary>Every rail vehicle the level's train controller knows.</summary>
    public static List<TrackTrain> All()
    {
        var list = new List<TrackTrain>();
        var controller = TrainController.instance;
        if (controller == null || controller.TrackTrains == null)
            return list;
        foreach (var train in controller.TrackTrains.Values)
        {
            if (train != null)
                list.Add(train);
        }
        return list;
    }

    /// <summary>A vehicle by its GUID, or null.</summary>
    public static TrackTrain Find(string id)
    {
        var controller = TrainController.instance;
        if (controller == null || string.IsNullOrEmpty(id) || controller.TrackTrains == null)
            return null;
        return controller.TrackTrains.TryGetValue(id, out var train) && train != null ? train : null;
    }

    public static TrainInfo Describe(TrackTrain train)
    {
        if (train == null)
            throw new ArgumentNullException(nameof(train));
        var data = train.GetComponentInChildren<MinitrainWagonData>(true);
        string cargo = data != null && data.connectedStorage != null ? Containers.IdOf(data.connectedStorage) : null;
        return new TrainInfo(train, train.currentSpeed, train.targetSpeed, train.active, train.braking, CoupledTo(train.frontCoupler),
            CoupledTo(train.backCoupler), data != null ? data.wagonType.ToString() : "None", cargo, CabOf(train) != null);
    }

    /// <summary>The minitrain cab the player drives, else the first one of the level; null when there is none. A scan.</summary>
    public static MinitrainInfo? Minitrain
    {
        get
        {
            global::Minitrain first = null;
            foreach (var cab in ObjectIds.Active<global::Minitrain>())
            {
                if (cab.playerUsing)
                    return DescribeCab(cab);
                if (first == null)
                    first = cab;
            }
            return first != null ? DescribeCab(first) : (MinitrainInfo?)null;
        }
    }

    /// <summary>The minitrain cab driving a vehicle, or null. A scan.</summary>
    public static global::Minitrain CabOf(TrackTrain train)
    {
        if (train == null)
            return null;
        foreach (var cab in ObjectIds.Active<global::Minitrain>())
        {
            if (CabTrain(cab) == train)
                return cab;
        }
        return null;
    }

    /// <summary>Track segments of the loaded scenes with their branches. A scan.</summary>
    public static List<TrackSegmentInfo> Segments()
    {
        var list = new List<TrackSegmentInfo>();
        foreach (var segment in ObjectIds.Active<Track_Segment>())
        {
            var next = new List<string>();
            foreach (var branch in segment.Branches)
                next.Add(branch != null && branch.nextSegment != null ? branch.nextSegment.GUID : null);
            var occupant = segment.occupiedBy;
            var previous = segment.previousSegment;
            list.Add(new TrackSegmentInfo(segment.GUID, segment.occupied, occupant != null ? occupant.GUID : null, (int)segment.activeBranchIndex,
                previous != null ? previous.GUID : null, next));
        }
        return list;
    }

    /// <summary>The track switches (levers that pick a branch). A scan.</summary>
    public static List<MinitrainSwitch> Switches() => ObjectIds.Active<MinitrainSwitch>();

    public static string IdOf(MinitrainSwitch trackSwitch) => ObjectIds.IdOf(trackSwitch);

    public static MinitrainSwitch FindSwitch(string id) => ObjectIds.Find<MinitrainSwitch>(id);

    /// <summary>The branch the switch is set to, or -1 without a switch.</summary>
    public static int Branch(MinitrainSwitch trackSwitch) => trackSwitch != null ? trackSwitch.currentBranchIndex : -1;

    /// <summary>How many branches a switch has.</summary>
    public static int BranchCount(MinitrainSwitch trackSwitch)
    {
        var rotations = trackSwitch != null ? BranchRotations(trackSwitch) : null;
        return rotations != null ? rotations.Length : 0;
    }

    /// <summary>
    /// Throws a switch to a branch. Like the lever, it refuses (false) while a train is on the switch's segment,
    /// unless <paramref name="force"/>. The lever swings over first, so the branch changes a moment later
    /// (at once when forced).
    /// </summary>
    public static bool SetBranch(MinitrainSwitch trackSwitch, int branch, bool force = false)
    {
        if (trackSwitch == null)
            throw new ArgumentNullException(nameof(trackSwitch));
        if (branch < 0 || branch >= BranchCount(trackSwitch))
            throw new ArgumentOutOfRangeException(nameof(branch), $"The switch has {BranchCount(trackSwitch)} branches.");
        if (SaveGate.IsBusy || (!force && Blocked(trackSwitch)))
            return false;
        using (WorldChanges.EnterMod())
        {
            if (force)
                trackSwitch.ChangeTrackForce(branch);
            else
                trackSwitch.ChangeTrack(branch);
        }
        return true;
    }

    // The lever's own check: a train on the segment the switch sets.
    private static bool Blocked(MinitrainSwitch trackSwitch)
    {
        var segment = trackSwitch.trackSegment;
        if (segment == null)
            return true;
        if (!trackSwitch.isReversed)
            return segment.occupied;
        return segment.previousSegment != null && segment.previousSegment.occupied;
    }

    /// <summary>The wagon loaders (chutes that fill ore wagons). A scan.</summary>
    public static List<MinitrainWagonLoader> Loaders() => ObjectIds.Active<MinitrainWagonLoader>();

    public static string IdOf(MinitrainWagonLoader loader) => ObjectIds.IdOf(loader);

    public static MinitrainWagonLoader FindLoader(string id) => ObjectIds.Find<MinitrainWagonLoader>(id);

    /// <summary>Turns a loader on or off, and sets whether the player may take from it.</summary>
    public static bool SetLoader(MinitrainWagonLoader loader, bool on, bool? freeToTake = null)
    {
        if (loader == null)
            throw new ArgumentNullException(nameof(loader));
        if (SaveGate.IsBusy)
            return false;
        loader.Toggle(on);
        if (freeToTake.HasValue)
            loader.SetOwnerPermissions(freeToTake.Value);
        return true;
    }

    // Departures and stops: a few checks a second while someone listens; nothing at all otherwise.
    internal static void Update()
    {
        if (!Moved.HasSubscribers || Time.unscaledTime < _nextCheck || !WorldChanges.Active)
            return;
        _nextCheck = Time.unscaledTime + CheckSeconds;
        var controller = TrainController.instance;
        if (controller == null || controller.TrackTrains == null)
            return;
        foreach (var pair in controller.TrackTrains)
        {
            var train = pair.Value;
            if (train == null)
                continue;
            bool moving = Mathf.Abs(train.currentSpeed) > 0.01f;
            if (!Moving.TryGetValue(train, out bool was))
            {
                Moving[train] = moving;
                continue;
            }
            if (moving == was)
                continue;
            Moving[train] = moving;
            Moved.Raise(new TrainEvent(train, moving ? TrainEventKind.Departed : TrainEventKind.Stopped, train.currentSpeed, null));
        }
    }

    internal static void Reset()
    {
        Moving.Clear();
        _nextCheck = 0f;
    }

    private static MinitrainInfo DescribeCab(global::Minitrain cab)
    {
        var tank = cab.fuelTank;
        var train = CabTrain(cab);
        return new MinitrainInfo(cab, tank != null ? tank.currentFuelAmount : 0f, tank != null ? tank.maxFuelAmount : 0f, cab.isOn, cab.playerUsing,
            train != null ? train.currentSpeed : 0f);
    }

    private static string CoupledTo(TrackTrain.TrainCoupler coupler) =>
        coupler != null && coupler.coupledTo != null && coupler.coupledTo.parent != null ? coupler.coupledTo.parent.GUID : null;
}

/// <summary>
/// One collision runs the game's crash step on both vehicles, and a wagon hands it on to its locomotive: only the
/// outermost call is reported, and each pair of vehicles once a frame. A crash shows as the vehicle's crash
/// cooldown starting: the bounce back already moves it again by the time the step returns.
/// </summary>
[HarmonyPatch(typeof(TrackTrain), nameof(TrackTrain.Crash))]
internal static class TrainCrashPatch
{
    private static readonly AccessTools.FieldRef<TrackTrain, Coroutine> Cooldown = AccessTools.FieldRefAccess<TrackTrain, Coroutine>("crashCooldown");
    private static readonly HashSet<TrackTrain> Reported = new HashSet<TrackTrain>();
    private static int _depth;
    private static int _frame = -1;

    // The speed it hit at; 0 when it can't crash now (standing, or still cooling down from the last one).
    [HarmonyPrefix]
    private static void Before(TrackTrain __instance, out float __state)
    {
        __state = Cooldown(__instance) == null ? __instance.currentSpeed : 0f;
        _depth++;
    }

    [HarmonyPostfix]
    private static void After(TrackTrain __instance, GameObject other, float __state)
    {
        try
        {
            if (_depth != 1 || __state == 0f || Cooldown(__instance) == null || !Trains.Moved.HasSubscribers || !WorldChanges.Active)
                return;
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                Reported.Clear();
            }
            var otherTrain = other != null ? other.GetComponentInParent<TrackTrain>() : null;
            if (!Reported.Add(__instance) || (otherTrain != null && !Reported.Add(otherTrain)))
                return;
            Trains.Moved.Raise(new TrainEvent(__instance, TrainEventKind.Crashed, __state, otherTrain != null ? otherTrain.GUID : null));
        }
        catch (Exception e) { TransportModule.Log.Error("Train crash hook", e); }
    }

    [HarmonyFinalizer]
    private static void Done() => _depth--;
}
