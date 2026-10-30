// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Economy;

/// <summary>
/// The NPC traders of the loaded scene beyond what <see cref="Shops"/> lists: their money and terms, setting
/// and refilling their money, and per-mod price factors on what they charge and pay. Opening hours are in
/// <see cref="ShopHours"/>. A trader is named by anything <see cref="Shops.Find"/> takes. Call on the main thread.
/// </summary>
public static class Traders
{
    /// <summary>True when the price hooks are in place, so price factors take effect and trades from code raise the deal events.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Shops");

    private static readonly AccessTools.FieldRef<Trade, FurnitureShop> Till = AccessTools.FieldRefAccess<Trade, FurnitureShop>("furniture");

    /// <summary>Every trader of the loaded scenes. A scan; not for every frame.</summary>
    public static List<TraderInfo> All()
    {
        var list = new List<TraderInfo>();
        foreach (var shop in Shops.All())
        {
            if (shop.Component is Trade trade)
                list.Add(new TraderInfo(shop, trade));
        }
        return list;
    }

    /// <summary>One trader, or null when <paramref name="key"/> names no trader.</summary>
    public static TraderInfo Get(string key)
    {
        var shop = Find(key, out var trade);
        return shop != null ? new TraderInfo(shop, trade) : null;
    }

    /// <summary>
    /// Sets how much money the trader has to buy with (whole coins, or Real Money in tenths). No effect, and
    /// false, while the difficulty gives traders infinite wealth.
    /// </summary>
    public static bool SetMoney(string key, float amount)
    {
        if (float.IsNaN(amount) || amount < 0f || amount > AccountMath.MaxAmount || SaveGate.IsBusy || DifficultyController.InfiniteTraderWealth)
            return false;
        if (Find(key, out var trade) == null)
            return false;
        bool rm = trade.sellPriceMode == Trade.PriceMode.OnlyRM;
        trade.CurrentMoney = rm ? Money.RoundRM(amount) : Mathf.Round(amount);
        return true;
    }

    /// <summary>
    /// Refills the trader's money now, as the game does on its own schedule (a random amount in the trader's
    /// range, scaled by the difficulty). A trader that pays from a furniture shop's till refills that till.
    /// </summary>
    public static bool RestockMoney(string key)
    {
        if (SaveGate.IsBusy || DifficultyController.instance == null || Find(key, out var trade) == null)
            return false;
        try
        {
            // The game's own refill writes the trader's private money, which such a trader never reads.
            var till = TillShop(trade);
            if (till != null)
                till.MoneyRestock();
            else
                trade.MoneyRestock();
            return true;
        }
        catch (Exception e)
        {
            WorldLog.Failed("Traders.RestockMoney", e);
            return false;
        }
    }

    /// <summary>
    /// The player buys <paramref name="count"/> of an item from a trader without the trade window, in the
    /// trader's currency and at the prices the window shows: stacks are taken in the order of the trader's
    /// slots, each at its own price (quality and wear change it; mods' factors and <see cref="Prices"/>
    /// modifiers apply). Only the trader's own goods are taken: nobody's, not stolen, nothing poured in. All or
    /// nothing: the goods must fit the player's inventory, grid, equipment slots or bag, as in the window.
    /// Raises <see cref="Shops.Bought"/> for each stack bought from. Works with the window open too.
    /// </summary>
    public static TradeResult Buy(string key, int itemId, int count)
    {
        Find(key, out var trade);
        return TraderDeals.Run(trade, itemId, count, buy: true);
    }

    /// <summary>
    /// The player sells <paramref name="count"/> of an item to a trader without the trade window, from the
    /// inventory grid and bag (never what's worn or held in the equipment slots), stacks the trader won't take
    /// left alone. The trader pays what the window would, and refuses as it would: stolen goods it doesn't
    /// buy, categories it doesn't deal in, a full storage, not enough money. All or nothing; raises
    /// <see cref="Shops.Sold"/> for each stack sold from. Liquids and filled containers are refused on both sides.
    /// </summary>
    public static TradeResult Sell(string key, int itemId, int count)
    {
        Find(key, out var trade);
        return TraderDeals.Run(trade, itemId, count, buy: false);
    }

    /// <summary>
    /// Scales one trader's prices for as long as <paramref name="owner"/> is loaded: <paramref name="buy"/> on
    /// what the player pays, <paramref name="sell"/> on what the player is paid (1 leaves it, 0.5 halves, at
    /// most 10). <paramref name="key"/> is a trader's name (every trader of that name, also ones in scenes loaded
    /// later), its address, GUID or path (that one trader), or null for every trader. Factors of different mods multiply; the
    /// same mod and trader again replaces the earlier pair. They apply before <see cref="Prices"/> modifiers
    /// and are not saved: set them in <c>OnLoad</c>. Throws for a factor out of range.
    /// </summary>
    public static void SetPriceFactors(ObenMod owner, string key, float buy, float sell)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (!ValidFactor(buy) || !ValidFactor(sell))
            throw new ArgumentOutOfRangeException(nameof(buy), $"Price factors must be above 0 and at most {TraderFactors.Max}.");
        TraderFactors.Set(owner, IdOf(key), buy, sell);
    }

    /// <summary>Takes this mod's factors on a trader (or, with null, on every trader) away. False when it had none.</summary>
    public static bool ClearPriceFactors(ObenMod owner, string key) =>
        owner != null && TraderFactors.Clear(owner, IdOf(key));

    // A name covers every trader of that name, loaded now or later. An address, GUID or path names one
    // trader and is kept as its address, so it survives the scene being reloaded; an address of a trader
    // that isn't loaded is kept as given.
    private static string IdOf(string key)
    {
        if (key == null)
            return TraderFactors.AllTraders;
        var shop = Find(key, out _);
        if (shop != null && !string.Equals(shop.Name, key, StringComparison.OrdinalIgnoreCase))
            return shop.Id;
        if (shop == null && ObjectAddress.TryParse(key, out _))
            return key;
        return TraderFactors.NameKey(key);
    }

    private static ShopInfo Find(string key, out Trade trade)
    {
        trade = null;
        foreach (var shop in Shops.FindAll(key))
        {
            if (shop.Component is Trade found && found != null)
            {
                trade = found;
                return shop;
            }
        }
        return null;
    }

    // The furniture shop whose money the trader pays with (the game's "same money as furniture shop" link), or null.
    private static FurnitureShop TillShop(Trade trade)
    {
        var shop = Till(trade);
        return shop != null ? shop : null;
    }

    private static bool ValidFactor(float factor) => !float.IsNaN(factor) && factor > 0f && factor <= TraderFactors.Max;
}
