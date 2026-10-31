using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.World;
using OS.Items;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>
/// Raises <see cref="Tanks.TankChanged"/>. The game moves a tank's level in small steps (a pour every few
/// milliseconds, a shower every game second, fillers and the janitor in between), all through the
/// <c>LiquidAmount</c> setter. The first step of a batch remembers what the tank held; the batch is reported
/// once the level has stood still for a check, or every two seconds while it keeps moving. Nothing is kept
/// while nobody listens.
/// </summary>
internal static class TankWatch
{
    private const float CheckSeconds = 0.25f;
    private const float LongestBatch = 2f;

    private struct Batch
    {
        public int Item;
        public float Amount;
        public int SeenItem;
        public float SeenAmount;
        public bool Seen;
        public float Started;
        public ChangeSource Source;
    }

    private static readonly Dictionary<LiquidStorage, Batch> Batches = new Dictionary<LiquidStorage, Batch>();
    private static readonly List<LiquidStorage> Due = new List<LiquidStorage>();
    private static float _nextCheck;

    /// <summary>A write is about to change <paramref name="tank"/>: remember what it held, once per batch.</summary>
    internal static void Writing(LiquidStorage tank)
    {
        if (tank == null || !Tanks.TankChanged.HasSubscribers || !WorldChanges.Active || Batches.ContainsKey(tank))
            return;
        var source = WorldChanges.Current;
        // The tank's window pours a little every few milliseconds while the player holds the button.
        if (source == ChangeSource.Game && (tank.fillingItem || tank.unfillingItem))
            source = ChangeSource.LocalPlayer;
        Batches[tank] = new Batch { Item = Tanks.LiquidOf(tank), Amount = tank.LiquidAmount, Started = Time.unscaledTime, Source = source };
    }

    /// <summary>Reports <paramref name="tank"/>'s batch now (a change from code, which is done in one step).</summary>
    internal static void Flush(LiquidStorage tank)
    {
        if (tank != null && Batches.TryGetValue(tank, out var batch))
        {
            Batches.Remove(tank);
            Report(tank, batch);
        }
    }

    internal static void Update()
    {
        if (Batches.Count == 0 || Time.unscaledTime < _nextCheck)
            return;
        _nextCheck = Time.unscaledTime + CheckSeconds;
        Due.Clear();
        foreach (var pair in Batches)
        {
            var tank = pair.Key;
            if (tank == null)
            {
                Due.Add(tank);
                continue;
            }
            var batch = pair.Value;
            int item = Tanks.LiquidOf(tank);
            float amount = tank.LiquidAmount;
            if ((batch.Seen && batch.SeenItem == item && batch.SeenAmount == amount) || Time.unscaledTime - batch.Started >= LongestBatch)
                Due.Add(tank);
            else
                Batches[tank] = new Batch { Item = batch.Item, Amount = batch.Amount, SeenItem = item, SeenAmount = amount, Seen = true, Started = batch.Started, Source = batch.Source };
        }
        foreach (var tank in Due)
        {
            var batch = Batches[tank];
            Batches.Remove(tank);
            if (tank != null)
                Report(tank, batch);
        }
    }

    internal static void Reset()
    {
        Batches.Clear();
        _nextCheck = 0f;
    }

    private static void Report(LiquidStorage tank, Batch batch)
    {
        int item = Tanks.LiquidOf(tank);
        float amount = tank.LiquidAmount;
        if ((item == batch.Item && Mathf.Abs(amount - batch.Amount) < 0.001f) || !WorldChanges.Active)
            return;
        Tanks.TankChanged.Raise(new TankChange(tank, batch.Item, batch.Amount, item, amount, batch.Source));
    }
}

/// <summary>
/// The level setter, and the two methods that change the liquid itself before they set the level
/// (<c>ClearLiquid</c> drops the liquid first), so the batch remembers the liquid as it was.
/// </summary>
[HarmonyPatch]
internal static class TankAmountPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.PropertySetter(typeof(LiquidStorage), nameof(LiquidStorage.LiquidAmount));
        yield return AccessTools.Method(typeof(LiquidStorage), nameof(LiquidStorage.ClearLiquid));
        yield return AccessTools.Method(typeof(LiquidStorage), nameof(LiquidStorage.SetLiquid), new[] { typeof(ItemReference), typeof(object[]) });
    }

    [HarmonyPrefix]
    private static void Before(LiquidStorage __instance)
    {
        try
        {
            TankWatch.Writing(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Tank hook", e);
        }
    }
}
