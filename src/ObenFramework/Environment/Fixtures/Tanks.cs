// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Env;

/// <summary>
/// Liquid containers (<c>LiquidStorage</c>): barrels, IBC tanks, sinks, hoses and the water supply.
/// Contents and locks are saved with the game. Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class Tanks
{
    /// <summary>The item category of everything a container can hold (Clean Water is 10840, Diesel 15900).</summary>
    public const string LiquidCategory = "Liquid";

    /// <summary>
    /// A container's liquid or level changed: a pour from its window, a shower or filler, a mod. Steps that come
    /// quickly one after another are reported together once the level settles (within half a second, or every
    /// two seconds while it keeps moving); a change from <see cref="Fill"/> or <see cref="Empty"/> is reported at
    /// once. Lock changes come through <see cref="Locks.Changed"/>. Not raised while a save loads.
    /// </summary>
    public static readonly WorldEvent<TankChange> TankChanged = new WorldEvent<TankChange>(nameof(TankChanged));

    /// <summary>True when the hooks are in place, so <see cref="TankChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    public static List<TankInfo> All()
    {
        var list = new List<TankInfo>();
        foreach (var tank in ObjectIds.Active<LiquidStorage>())
            list.Add(Describe(tank));
        return list;
    }

    public static LiquidStorage Find(string id) => ObjectIds.Find<LiquidStorage>(id);

    public static string IdOf(LiquidStorage tank) => ObjectIds.IdOf(tank);

    public static TankInfo Describe(LiquidStorage tank) => new TankInfo(IdOf(tank), tank.name, LiquidOf(tank), tank.LiquidAmount, tank.Capacity,
        tank._Lock != null && tank._Lock.Locked, WorldWater.Supply == tank);

    /// <summary>
    /// Fills a container with <paramref name="amount"/> litres of a liquid item (water, fuel, moonshine),
    /// replacing what was in it. The amount is capped at the capacity. Throws for a container without a
    /// fixed capacity (taps, sinks) and for an item that is not a liquid.
    /// </summary>
    public static bool Fill(LiquidStorage tank, int itemId, float amount)
    {
        if (tank == null)
            throw new ArgumentNullException(nameof(tank));
        RequireCapacity(tank);
        if (!(amount >= 0f) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount), "Give 0 or more litres.");
        var item = ItemLookup.ById(itemId) ?? throw new ArgumentException($"No item {itemId}.", nameof(itemId));
        if (!string.Equals(item.Category, LiquidCategory, StringComparison.Ordinal))
            throw new ArgumentException($"{item.Title} is not a liquid (category {item.Category}).", nameof(itemId));
        if (SaveGate.IsBusy)
            return false;

        float litres = ItemLiquidData.SetAndGetLiquidAmount(Math.Min(amount, tank.Capacity));
        using (WorldChanges.EnterMod())
        {
            if (litres <= 0f)
                tank.OnEmpty();
            else
                tank.SetLiquid(new ItemReference(item), litres, null, true);
        }
        TankWatch.Flush(tank);
        return litres <= 0f ? tank.IsEmpty() : LiquidOf(tank) == itemId;
    }

    /// <summary>Empties a container, like pouring it out. Throws for taps and sinks fed from elsewhere.</summary>
    public static bool Empty(LiquidStorage tank)
    {
        if (tank == null)
            throw new ArgumentNullException(nameof(tank));
        RequireCapacity(tank);
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            tank.OnEmpty();
        TankWatch.Flush(tank);
        return tank.IsEmpty();
    }

    /// <summary>Locks or unlocks a container that has a lock. Returns false when it has none.</summary>
    public static bool SetLocked(LiquidStorage tank, bool locked)
    {
        if (tank == null)
            throw new ArgumentNullException(nameof(tank));
        if (tank._Lock == null)
            return false;
        if (tank._Lock.Locked == locked)
            return true;
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (locked)
                tank.Lock();
            else
                tank.Unlock();
        }
        return tank._Lock.Locked == locked;
    }

    internal static int LiquidOf(LiquidStorage tank) => tank.Liquid != null && !tank.IsEmpty() ? tank.Liquid.ID : 0;

    private static void RequireCapacity(LiquidStorage tank)
    {
        if (tank.Capacity <= 0f)
            throw new InvalidOperationException($"{tank.name} has no capacity of its own; fill or empty what feeds it.");
    }
}
