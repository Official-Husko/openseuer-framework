// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using OS.Items;

namespace ObenFramework.Items;

/// <summary>An item that burns as fuel in stoves, generators and heaters.</summary>
public readonly struct FuelInfo
{
    public FuelInfo(int itemId, int burnSeconds, int burntItemId, float burntItemAmount)
    {
        ItemId = itemId;
        BurnSeconds = burnSeconds;
        BurntItemId = burntItemId;
        BurntItemAmount = burntItemAmount;
    }

    public int ItemId { get; }

    /// <summary>How long one unit burns, in game seconds.</summary>
    public int BurnSeconds { get; }

    /// <summary>What is left after burning (ash, an empty can), or -1.</summary>
    public int BurntItemId { get; }

    public float BurntItemAmount { get; }
}

/// <summary>The items that work as fuel, from their fuel meta.</summary>
public static class FuelTypes
{
    public static List<FuelInfo> All()
    {
        var list = new List<FuelInfo>();
        foreach (var item in ItemLookup.All)
        {
            if (Describe(item) is FuelInfo info)
                list.Add(info);
        }
        return list;
    }

    /// <summary>The item's fuel values, or null when it isn't fuel.</summary>
    public static FuelInfo? Get(int itemId) => Describe(ItemLookup.ById(itemId));

    private static FuelInfo? Describe(Item item)
    {
        var fuel = item != null && item.Meta != null ? Item.GetMetaOfType<ItemFueldata>(item.Meta) : null;
        if (fuel == null)
            return null;
        return new FuelInfo(item.ID, fuel.burntime, fuel.burntItem != null ? fuel.burntItem.ID : -1, fuel.burntItemAmount);
    }
}
