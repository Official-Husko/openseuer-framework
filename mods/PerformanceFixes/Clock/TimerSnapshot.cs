using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace PerformanceFixes.Clock;

/// <summary>
/// The game's in-game timers (cooldowns, delayed events, NPC schedules) are advanced every frame,
/// and each pass starts by copying the list of timers into a new array so timers can remove
/// themselves while it runs. This does the same pass over a reused list instead of a new array,
/// and does nothing at all when no timer exists. Timers still fire in the same order and a timer
/// added by a callback still waits for the next pass.
/// </summary>
internal sealed class TimerSnapshot : ModFeature
{
    public const string Key = "TimerSnapshot";

    public override string Id => Key;
    public override string Name => "In-game timers advance without garbage";
    public override string Description => "Advances the game's timers over a reused list instead of copying them to a new array each frame.";

    protected override void OnDisable() => TimerSnapshotPatch.Clear();
}

[HarmonyPatchCategory(TimerSnapshot.Key)]
[HarmonyPatch(typeof(TimeOfDayAzure), "UpdateTimers")]
internal static class TimerSnapshotPatch
{
    private static readonly List<TimeOfDayAzure.Timer> Snapshot = new List<TimeOfDayAzure.Timer>(32);
    private static bool _busy;

    internal static void Clear()
    {
        Snapshot.Clear();
        _busy = false;
    }

    private static bool Prefix(List<TimeOfDayAzure.Timer> ___timers, int ___deltaTime)
    {
        if (___timers.Count == 0)
            return false;
        if (_busy)
            return true;

        _busy = true;
        try
        {
            Snapshot.AddRange(___timers);
            for (int i = 0; i < Snapshot.Count; i++)
                Advance(___timers, Snapshot[i], ___deltaTime);
        }
        finally
        {
            Snapshot.Clear();
            _busy = false;
        }
        return false;
    }

    private static void Advance(List<TimeOfDayAzure.Timer> timers, TimeOfDayAzure.Timer timer, int deltaTime)
    {
        if (!timer.stopped)
            timer.remainingInSeconds -= deltaTime;
        if (timer.remainingInSeconds >= 0)
            return;

        try
        {
            timer.Callback?.Invoke();
            timer.Callback2?.Invoke(timer);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        timers.Remove(timer);
    }
}
