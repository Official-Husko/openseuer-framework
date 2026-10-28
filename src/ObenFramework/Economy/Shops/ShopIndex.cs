using System.Collections.Generic;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.Economy;

/// <summary>
/// Per-scene caches behind <see cref="Shops"/>: one <see cref="ShopInfo"/> per shop component and the
/// vending machine each stocked entry belongs to (the game's entries don't know their machine, but the
/// price hook needs it). The price and deal patches run often, so they only ever do dictionary lookups here.
/// </summary>
internal static class ShopIndex
{
    // A vending entry we don't know can come from a machine created after the last scan.
    private const float MissRescanSeconds = 2f;

    private static readonly Dictionary<int, ShopInfo> Infos = new Dictionary<int, ShopInfo>();
    private static readonly Dictionary<VendingMachine.VendingMachineItem, VendingMachine> VendingOwners =
        new Dictionary<VendingMachine.VendingMachineItem, VendingMachine>();
    private static float _scannedAt = float.NegativeInfinity;

    internal static void Reset()
    {
        Infos.Clear();
        VendingOwners.Clear();
        _scannedAt = float.NegativeInfinity;
    }

    /// <summary>The shop a component is, or null for anything that isn't a shop.</summary>
    internal static ShopInfo Of(Component component)
    {
        if (component == null)
            return null;

        int id = component.GetInstanceID();
        if (Infos.TryGetValue(id, out var info))
            return info;

        var kind = KindOf(component);
        if (kind == null)
            return null;

        info = new ShopInfo(component, kind.Value, NameOf(component), ObjectAddress.Of(component));
        Infos[id] = info;
        return info;
    }

    internal static ShopInfo OfVending(VendingMachine.VendingMachineItem entry)
    {
        if (entry == null)
            return null;
        if (!VendingOwners.TryGetValue(entry, out var machine) || machine == null)
        {
            if (Time.realtimeSinceStartup - _scannedAt < MissRescanSeconds)
                return null;
            Rescan();
            if (!VendingOwners.TryGetValue(entry, out machine) || machine == null)
                return null;
        }
        return Of(machine);
    }

    /// <summary>Tells the cache that <paramref name="entry"/> was added to <paramref name="machine"/> by a mod.</summary>
    internal static void Adopt(VendingMachine machine, VendingMachine.VendingMachineItem entry)
    {
        if (machine != null && entry != null)
            VendingOwners[entry] = machine;
    }

    private static void Rescan()
    {
        _scannedAt = Time.realtimeSinceStartup;
        VendingOwners.Clear();
        foreach (var machine in Object.FindObjectsByType<VendingMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (machine.sellableItems == null)
                continue;
            foreach (var entry in machine.sellableItems)
            {
                if (entry != null)
                    VendingOwners[entry] = machine;
            }
        }
    }

    private static ShopKind? KindOf(Component component)
    {
        switch (component)
        {
            case Trade _: return ShopKind.Trader;
            case VendingMachine _: return ShopKind.Vending;
            case MarketShop _: return ShopKind.Market;
            case TrainShop _: return ShopKind.Train;
            case BaseShop _: return ShopKind.Furniture;
            default: return null;
        }
    }

    private static string NameOf(Component component)
    {
        var title = component is Interactable interactable ? interactable.title : null;
        return string.IsNullOrEmpty(title) ? component.name : title;
    }
}
