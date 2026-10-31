// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Transport;

/// <summary>
/// Driving trains from code: engine, target speed, brakes, coupling and uncoupling. A minitrain cab is started
/// and stopped through the cab, so its lights, sounds and fuel behave as when the player does it. Every call
/// returns false (and changes nothing) when the vehicle can't do it right now, or while a save is written or loaded.
/// </summary>
public static class TrainActions
{
    /// <summary>
    /// Starts or stops the engine. A minitrain's cab runs its start-up first, so its engine comes on a moment
    /// later; an unfuelled minitrain starts but doesn't move.
    /// </summary>
    public static bool SetEngine(TrackTrain train, bool on)
    {
        if (train == null)
            throw new ArgumentNullException(nameof(train));
        if (SaveGate.IsBusy)
            return false;
        var cab = Trains.CabOf(train);
        using (WorldChanges.EnterMod())
        {
            if (cab == null)
                train.Toggle(on);
            else if (on)
                cab.StartMinitrain();
            else
                cab.StopMinitrain();
        }
        return cab != null ? cab.isOn == on : train.active == on;
    }

    /// <summary>
    /// Sets the speed the train works towards (negative backwards; the game clamps it to the train's range).
    /// While the player drives a minitrain, their levers set it again every physics step.
    /// </summary>
    public static bool SetTargetSpeed(TrackTrain train, float speed)
    {
        if (train == null)
            throw new ArgumentNullException(nameof(train));
        if (float.IsNaN(speed) || float.IsInfinity(speed))
            throw new ArgumentOutOfRangeException(nameof(speed));
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            train.SetSpeed(speed);
        return true;
    }

    public static bool Brake(TrackTrain train, bool on)
    {
        if (train == null)
            throw new ArgumentNullException(nameof(train));
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            train.ApplyBrakes(on);
        return train.braking == on;
    }

    /// <summary>
    /// Couples two vehicles end to end the way their couplers would on contact: only when the ends touch, both
    /// are free and ready, the vehicles aren't already one train, and neither goes faster than its coupler allows.
    /// </summary>
    public static bool Couple(TrackTrain train, TrainEnd end, TrackTrain other, TrainEnd otherEnd)
    {
        if (train == null)
            throw new ArgumentNullException(nameof(train));
        if (other == null)
            throw new ArgumentNullException(nameof(other));
        var coupler = CouplerOf(train, end);
        var otherCoupler = CouplerOf(other, otherEnd);
        if (SaveGate.IsBusy || !CanCouple(train, coupler, other, otherCoupler))
            return false;
        using (WorldChanges.EnterMod())
            coupler.Couple(otherCoupler);
        return coupler.coupledTo != null;
    }

    /// <summary>Uncouples whatever is coupled at one end. False when nothing is.</summary>
    public static bool Release(TrackTrain train, TrainEnd end)
    {
        if (train == null)
            throw new ArgumentNullException(nameof(train));
        var coupler = CouplerOf(train, end);
        if (coupler == null || coupler.coupledTo == null || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            coupler.Release();
        return coupler.coupledTo == null;
    }

    private static bool CanCouple(TrackTrain train, TrackTrain.TrainCoupler coupler, TrackTrain other, TrackTrain.TrainCoupler otherCoupler)
    {
        if (train == other || coupler == null || otherCoupler == null || train.train == other.train)
            return false;
        if (coupler.parent != train || otherCoupler.parent != other || coupler.coupledTo != null || otherCoupler.coupledTo != null)
            return false;
        foreach (var wagon in train.train.wagons)
        {
            if (other.train.wagons.Contains(wagon))
                return false;
        }
        return Ready(coupler) && Ready(otherCoupler) && Touching(coupler, otherCoupler) && Slow(train, coupler) && Slow(other, otherCoupler);
    }

    private static bool Ready(TrackTrain.TrainCoupler coupler)
    {
        var trigger = coupler.couplingTrigger;
        return trigger != null && trigger.active && trigger.cooldown == null;
    }

    private static bool Touching(TrackTrain.TrainCoupler coupler, TrackTrain.TrainCoupler other)
    {
        var a = coupler.couplingTrigger.GetComponent<Collider>();
        var b = other.couplingTrigger.GetComponent<Collider>();
        return a != null && b != null && a.bounds.Intersects(b.bounds);
    }

    private static bool Slow(TrackTrain train, TrackTrain.TrainCoupler coupler)
    {
        float max = coupler.couplingTrigger.maxSpeed;
        return max <= 0f || Mathf.Abs(train.currentSpeed) <= max;
    }

    private static TrackTrain.TrainCoupler CouplerOf(TrackTrain train, TrainEnd end) => end == TrainEnd.Front ? train.frontCoupler : train.backCoupler;
}
