// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>A snapshot of one supermarket checkout (the game's <c>MarketShop</c>).</summary>
public sealed class CheckoutInfo
{
    internal CheckoutInfo(ShopInfo shop, MarketShop market)
    {
        Shop = shop;
        Name = string.IsNullOrEmpty(market.marketName) ? shop.Name : market.marketName;
        Locked = market.marketLock != null && market.marketLock.Locked;
        CanLock = market.marketLock != null;
        State = market.state;
        Total = market.GetCurrentPrice();
        Currency = market.currency;
        PriceFactor = market.priceFactor;
        Owner = market.owner != null ? market.owner.ID : -1;
    }

    public ShopInfo Shop { get; }

    /// <summary>The market's name (the shop the checkout belongs to).</summary>
    public string Name { get; }

    /// <summary>Locked checkouts take no goods (closed shop, quest).</summary>
    public bool Locked { get; }

    /// <summary>False for a checkout without a lock.</summary>
    public bool CanLock { get; }

    /// <summary>Waiting for goods, scanning, or waiting for payment.</summary>
    public MarketShop.State State { get; }

    /// <summary>The scanned total so far, rounded as it is charged.</summary>
    public int Total { get; }

    public Currency Currency { get; }

    /// <summary>The game's factor on the goods' value (1.1 is 110%).</summary>
    public float PriceFactor { get; }

    /// <summary>The character id the goods on the shelves belong to.</summary>
    public int Owner { get; }
}

/// <summary>A payment at a checkout, raised by <see cref="MarketShops.Paid"/>.</summary>
public readonly struct CheckoutPayment
{
    internal CheckoutPayment(ShopInfo shop, int total, Currency currency)
    {
        Shop = shop;
        Total = total;
        Currency = currency;
    }

    public ShopInfo Shop { get; }

    /// <summary>What was charged for everything on the conveyor.</summary>
    public int Total { get; }

    public Currency Currency { get; }
}

/// <summary>
/// The supermarket checkouts of the loaded scene: their state and running total, locking and unlocking them,
/// and one <see cref="Paid"/> event per payment (<see cref="Shops.Bought"/> has the goods line by line).
/// Call on the main thread.
/// </summary>
public static class MarketShops
{
    /// <summary>The player paid at a checkout.</summary>
    public static readonly WorldEvent<CheckoutPayment> Paid = new WorldEvent<CheckoutPayment>(nameof(Paid));

    /// <summary>Every checkout of the loaded scenes. A scan; not for every frame.</summary>
    public static List<CheckoutInfo> All()
    {
        var list = new List<CheckoutInfo>();
        foreach (var shop in Shops.All())
        {
            if (shop.Component is MarketShop market)
                list.Add(new CheckoutInfo(shop, market));
        }
        return list;
    }

    /// <summary>One checkout, or null when <paramref name="key"/> names none.</summary>
    public static CheckoutInfo Get(string key)
    {
        var shop = Find(key, out var market);
        return shop != null ? new CheckoutInfo(shop, market) : null;
    }

    /// <summary>Locks or unlocks a checkout with its own lock, as the game's scripts do. False for one without a lock.</summary>
    public static bool SetLocked(string key, bool locked)
    {
        if (SaveGate.IsBusy || Find(key, out var market) == null || market.marketLock == null)
            return false;
        if (locked)
            market.Lock();
        else
            market.Unlock();
        return market.marketLock.Locked == locked;
    }

    private static ShopInfo Find(string key, out MarketShop market)
    {
        market = null;
        foreach (var shop in Shops.FindAll(key))
        {
            if (shop.Component is MarketShop found && found != null)
            {
                market = found;
                return shop;
            }
        }
        return null;
    }
}
