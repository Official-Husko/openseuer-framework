// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Economy;

/// <summary>A snapshot of one furniture or train shop (the game's <c>BaseShop</c>).</summary>
public sealed class FurnitureShopInfo
{
    internal FurnitureShopInfo(ShopInfo shop, BaseShop baseShop)
    {
        Shop = shop;
        Coins = baseShop.currentOcMoney;
        RealMoney = baseShop.currentRmMoney;
        BuyPercent = baseShop.buyPercent;
        SellPercent = baseShop.sellPercent;
        RestocksDaily = baseShop.restockEveryDay;
    }

    public ShopInfo Shop { get; }

    /// <summary>Coins the shop has to buy furniture from the player with.</summary>
    public int Coins { get; }

    /// <summary>Real Money the shop has to buy with (whole numbers).</summary>
    public int RealMoney { get; }

    /// <summary>What the player pays, as a percentage of the furniture's value (110 by default).</summary>
    public int BuyPercent { get; }

    /// <summary>What the player is paid, as a percentage of the value (90 by default).</summary>
    public int SellPercent { get; }

    /// <summary>True when the goods and money renew every day rather than after a random time.</summary>
    public bool RestocksDaily { get; }
}

/// <summary>
/// The money side of furniture and train shops (goods and prices are in <see cref="Shops"/>,
/// <see cref="Prices"/> and <see cref="ShopStock"/>): read and set what they can pay the player, and restock
/// them now. A shop is named by anything <see cref="Shops.Find"/> takes. Call on the main thread.
/// </summary>
public static class FurnitureShops
{
    /// <summary>True when the shop hooks are in place: prices follow <see cref="Prices"/> and restocks raise <see cref="Shops.Restocked"/>.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Shops");

    /// <summary>Every furniture and train shop of the loaded scenes.</summary>
    public static List<FurnitureShopInfo> All()
    {
        var list = new List<FurnitureShopInfo>();
        foreach (var shop in Shops.All())
        {
            if (shop.Component is BaseShop baseShop)
                list.Add(new FurnitureShopInfo(shop, baseShop));
        }
        return list;
    }

    /// <summary>One shop, or null.</summary>
    public static FurnitureShopInfo Get(string key)
    {
        var shop = Find(key, out var baseShop);
        return shop != null ? new FurnitureShopInfo(shop, baseShop) : null;
    }

    /// <summary>Sets the money the shop can pay the player with, in both currencies.</summary>
    public static bool SetMoney(string key, int coins, int realMoney)
    {
        if (coins < 0 || realMoney < 0 || SaveGate.IsBusy || Find(key, out var baseShop) == null)
            return false;
        baseShop.currentOcMoney = coins;
        baseShop.currentRmMoney = realMoney;
        return true;
    }

    /// <summary>Refills the shop's money now, a random amount in its range scaled by the difficulty, as the game does.</summary>
    public static bool RestockMoney(string key) => Run(key, "RestockMoney", s => s.MoneyRestock());

    /// <summary>Renews the shop's goods and money now, as its own restock does (<see cref="Shops.Restocked"/> is raised).</summary>
    public static bool Restock(string key) => Run(key, "Restock", s => s.Restock());

    /// <summary>The pieces a furniture or train shop has on offer now with their prices, or an empty list for no such shop.</summary>
    public static List<FurnitureStockLine> Stock(string key)
    {
        Find(key, out var baseShop);
        return baseShop != null ? FurnitureDeals.Stock(baseShop) : new List<FurnitureStockLine>();
    }

    /// <summary>
    /// The player buys <paramref name="count"/> of a piece (the game's furniture id or its title) from a furniture
    /// shop without its window: paid in both currencies at the shop's prices, the pieces go to the player's
    /// unplaced furniture, the shop's till and stock change as with the window. Raises <see cref="Shops.Bought"/>
    /// once. Train shops and pieces that ask for a picture first are <see cref="TradeResult.Unsupported"/>.
    /// </summary>
    public static TradeResult Buy(string key, string furniture, int count = 1)
    {
        var shop = Find(key, out var baseShop);
        return FurnitureDeals.Buy(shop, baseShop, furniture, count);
    }

    /// <summary>
    /// The player sells <paramref name="count"/> unplaced pieces (by id or title) to a furniture shop without its
    /// window, if the shop takes that piece and can pay; free pieces never sell. Raises <see cref="Shops.Sold"/> once.
    /// </summary>
    public static TradeResult Sell(string key, string furniture, int count = 1)
    {
        var shop = Find(key, out var baseShop);
        return FurnitureDeals.Sell(shop, baseShop, furniture, count);
    }

    private static bool Run(string key, string what, Action<BaseShop> action)
    {
        if (SaveGate.IsBusy || DifficultyController.instance == null || Find(key, out var baseShop) == null)
            return false;
        try
        {
            action(baseShop);
            return true;
        }
        catch (Exception e)
        {
            WorldLog.Failed("FurnitureShops." + what, e);
            return false;
        }
    }

    private static ShopInfo Find(string key, out BaseShop baseShop)
    {
        baseShop = null;
        foreach (var shop in Shops.FindAll(key))
        {
            if (shop.Component is BaseShop found && found != null)
            {
                baseShop = found;
                return shop;
            }
        }
        return null;
    }
}
