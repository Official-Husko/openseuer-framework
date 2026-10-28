// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Env;

/// <summary>A snapshot of one liquid container.</summary>
public sealed class TankInfo
{
    internal TankInfo(string id, string name, int liquidItemId, float amount, float capacity, bool locked, bool isSupply)
    {
        Id = id;
        Name = name;
        LiquidItemId = liquidItemId;
        Amount = amount;
        Capacity = capacity;
        Locked = locked;
        IsSupply = isSupply;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>The liquid's item id, or 0 when empty.</summary>
    public int LiquidItemId { get; }

    /// <summary>Litres in it.</summary>
    public float Amount { get; }

    /// <summary>Litres it holds; negative for taps and sinks fed from elsewhere, which never run dry by themselves.</summary>
    public float Capacity { get; }

    public bool Locked { get; }

    /// <summary>It is the scene's water supply (see <see cref="WorldWater.Supply"/>).</summary>
    public bool IsSupply { get; }

    public bool IsEndless => Capacity < 0f;
}

/// <summary>
/// Liquid containers (<c>LiquidStorage</c>): barrels, IBC tanks, sinks, hoses and the water supply.
/// Contents and locks are saved with the game.
/// </summary>
public static class Tanks
{
    /// <summary>The item category of everything a container can hold (Clean Water is 10840, Diesel 15900).</summary>
    public const string LiquidCategory = "Liquid";

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
    public static void Fill(LiquidStorage tank, int itemId, float amount)
    {
        if (tank == null)
            throw new ArgumentNullException(nameof(tank));
        if (tank.Capacity <= 0f)
            throw new InvalidOperationException($"{tank.name} has no capacity of its own; fill what feeds it.");
        if (!(amount >= 0f) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount), "Give 0 or more litres.");
        var item = ItemLookup.ById(itemId) ?? throw new ArgumentException($"No item {itemId}.", nameof(itemId));
        if (!string.Equals(item.Category, LiquidCategory, StringComparison.Ordinal))
            throw new ArgumentException($"{item.Title} is not a liquid (category {item.Category}).", nameof(itemId));

        float litres = ItemLiquidData.SetAndGetLiquidAmount(Math.Min(amount, tank.Capacity));
        using (WorldChanges.EnterMod())
        {
            if (litres <= 0f)
                tank.OnEmpty();
            else
                tank.SetLiquid(new ItemReference(item), litres, null, true);
        }
    }

    /// <summary>Empties a container, like pouring it out.</summary>
    public static void Empty(LiquidStorage tank)
    {
        if (tank == null)
            throw new ArgumentNullException(nameof(tank));
        using (WorldChanges.EnterMod())
            tank.OnEmpty();
    }

    /// <summary>Locks or unlocks a container that has a lock. Returns false when it has none.</summary>
    public static bool SetLocked(LiquidStorage tank, bool locked)
    {
        if (tank == null)
            throw new ArgumentNullException(nameof(tank));
        if (tank._Lock == null)
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

    private static int LiquidOf(LiquidStorage tank) => tank.Liquid != null && !tank.IsEmpty() ? tank.Liquid.ID : 0;
}
