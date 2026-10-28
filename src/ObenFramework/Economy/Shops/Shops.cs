using System.Collections.Generic;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Economy;

/// <summary>
/// The shops of the loaded scene and what happens at them. A shop is a trader, a supermarket checkout, a
/// furniture or train shop or a vending machine; each has a stable address (<see cref="ShopInfo.Id"/>) built
/// from the game's GUID. Prices can be changed with <see cref="Prices"/>, stock added with
/// <see cref="ShopStock"/>. Call on the main thread; the scans are for commands and setup, not per frame.
/// </summary>
public static class Shops
{
    /// <summary>The player bought something. Raised after the game finished the purchase, whichever way it was made.</summary>
    public static readonly WorldEvent<ShopDeal> Bought = new WorldEvent<ShopDeal>(nameof(Bought));

    /// <summary>The player sold something to a trader or furniture shop.</summary>
    public static readonly WorldEvent<ShopDeal> Sold = new WorldEvent<ShopDeal>(nameof(Sold));

    /// <summary>
    /// A shop restocked its goods: a trader's storage refilled, a vending machine refilled, a furniture or
    /// train shop drew new stock. Stock added through <see cref="ShopStock"/> is topped up just before this fires.
    /// </summary>
    public static readonly WorldEvent<ShopInfo> Restocked = new WorldEvent<ShopInfo>(nameof(Restocked));

    /// <summary>Every shop in the loaded scenes, including ones in objects that are switched off right now.</summary>
    public static List<ShopInfo> All()
    {
        var shops = new List<ShopInfo>();
        Collect<Trade>(shops);
        Collect<VendingMachine>(shops);
        Collect<MarketShop>(shops);
        Collect<BaseShop>(shops);
        return shops;
    }

    /// <summary>The shop a game component is, or null when it isn't one.</summary>
    public static ShopInfo Of(Component component) => ShopIndex.Of(component);

    /// <summary>
    /// The shop named by <paramref name="key"/>: its address text, GUID, hierarchy path, <c>#instanceId</c>, or
    /// its name (the first match when several share it). Null when there is no such shop.
    /// </summary>
    public static ShopInfo Find(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        var byAddress = ShopIndex.Of(ObjectResolver.Find<Component>(key));
        if (byAddress != null)
            return byAddress;

        foreach (var shop in All())
        {
            if (shop.Matches(key))
                return shop;
        }
        return null;
    }

    /// <summary>Every shop <paramref name="key"/> names; a name can match several, an address or GUID one.</summary>
    public static List<ShopInfo> FindAll(string key)
    {
        var found = new List<ShopInfo>();
        foreach (var shop in All())
        {
            if (shop.Matches(key))
                found.Add(shop);
        }
        if (found.Count == 0)
        {
            var single = Find(key);
            if (single != null)
                found.Add(single);
        }
        return found;
    }

    /// <summary>The trader whose trade window is open right now, or null.</summary>
    public static ShopInfo Open
    {
        get
        {
            var panel = TradePanel.instance;
            return panel != null && panel.activeTrade != null ? ShopIndex.Of(panel.activeTrade) : null;
        }
    }

    /// <summary>What a trader or vending machine has on offer now, with current prices. Other kinds have no fixed stock and give an empty list.</summary>
    public static List<StockLine> Stock(ShopInfo shop) => ShopLines.Of(shop);

    private static void Collect<T>(List<ShopInfo> into) where T : Component
    {
        foreach (var component in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var info = ShopIndex.Of(component);
            if (info != null)
                into.Add(info);
        }
    }

    internal static void Reset() => ShopIndex.Reset();
}
