// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Economy;

/// <summary>
/// Prices and till sums of player-run shops, without Unity types. The default price follows the game's
/// own (unused) player shop: the item's sale value times the shop's markup, rounded to whole coins, and a
/// tenth of that in Real Money for a Real Money shop.
/// </summary>
internal static class PlayerShopMath
{
    public const float DefaultMarkup = 1.2f;
    public const float MaxMarkup = 10f;

    /// <summary>Largest amount a till holds per currency.</summary>
    public const float MaxTill = 100000000f;

    /// <summary>True for a markup the shops accept: above 0, at most <see cref="MaxMarkup"/>.</summary>
    public static bool ValidMarkup(float markup) => markup > 0f && markup <= MaxMarkup;

    /// <summary>
    /// The price of one unit in the shop's currency: a fixed price when the shop set one, else the sale value
    /// times the markup (coins rounded to whole, Real Money a tenth of the coin price, in tenths). 0 means not for sale.
    /// </summary>
    public static float UnitPrice(int saleValue, float markup, bool realMoney, IReadOnlyDictionary<int, float> fixedPrices, int itemId)
    {
        if (fixedPrices != null && fixedPrices.TryGetValue(itemId, out float fixedPrice))
            return Settle(fixedPrice, realMoney);
        double coins = Math.Round((double)saleValue * markup);
        if (double.IsNaN(coins) || coins <= 0d)
            return 0f;
        if (coins > MaxTill)
            coins = MaxTill;
        return realMoney ? (float)Math.Round(coins * 0.1, 1) : (float)coins;
    }

    /// <summary>A price as the shop's currency holds it: whole coins, Real Money in tenths, never negative.</summary>
    public static float Settle(float price, bool realMoney)
    {
        if (float.IsNaN(price) || price <= 0f)
            return 0f;
        if (price > MaxTill)
            price = MaxTill;
        return realMoney ? (float)Math.Round(price, 1) : (float)Math.Round(price);
    }

    /// <summary>The total for <paramref name="count"/> units.</summary>
    public static float Total(float unitPrice, int count, bool realMoney) =>
        realMoney ? (float)Math.Round(unitPrice * count, 1) : unitPrice * count;

    /// <summary>True when a sale of <paramref name="count"/> from a stack can go ahead.</summary>
    public static bool CanSell(int stackItemId, int expectedItemId, int stackAmount, int count, float unitPrice) =>
        stackItemId == expectedItemId && count > 0 && stackAmount >= count && unitPrice > 0f;

    /// <summary>Adds to a till, capped at <see cref="MaxTill"/>. False when the amount is not usable.</summary>
    public static bool Credit(ref int coins, ref float realMoney, bool rm, float amount)
    {
        if (float.IsNaN(amount) || amount <= 0f)
            return false;
        if (rm)
            realMoney = (float)Math.Round(Math.Min((double)realMoney + amount, MaxTill), 1);
        else
            coins = (int)Math.Min((double)coins + Math.Floor(amount), MaxTill);
        return true;
    }

    /// <summary>Takes from a till. False, changing nothing, when the till holds less.</summary>
    public static bool Debit(ref int coins, ref float realMoney, bool rm, float amount)
    {
        if (float.IsNaN(amount) || amount <= 0f)
            return false;
        if (rm)
        {
            if (realMoney + 0.0001f < amount)
                return false;
            realMoney = Math.Max(0f, (float)Math.Round(realMoney - amount, 1));
            return true;
        }
        if (coins < amount)
            return false;
        coins -= (int)Math.Floor(amount);
        return true;
    }
}
