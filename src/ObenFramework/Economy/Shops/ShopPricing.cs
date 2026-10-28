using System;
using UnityEngine;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// Applies <see cref="Prices"/> to a price the game computed and keeps the result a price the game could
/// have produced: whole coins, Real Money in tenths (or whole where that path rounds to whole), never
/// below the smallest charge, never overflowing.
/// </summary>
internal static class ShopPricing
{
    private const float MaxPrice = 100000000f;

    /// <param name="original">The game's own unit price, in <paramref name="currency"/>.</param>
    /// <param name="minimum">The smallest price the path can charge: 1 OC, 0.1 RM at traders, 0 where the game allows free.</param>
    /// <param name="wholeRealMoney">True where the path rounds Real Money to whole numbers (vending, furniture, checkout).</param>
    internal static float Apply(ShopInfo shop, Item item, Furniture furniture, bool playerBuys, Currency currency,
        float original, float minimum, bool wholeRealMoney)
    {
        if (shop == null || !Prices.Active)
            return original;
        // A furniture price of 0 means "not charged in this currency"; modifiers must not invent one.
        if (furniture != null && original <= 0f)
            return original;

        float modified = Prices.Run(shop, item, furniture, playerBuys, currency, original);
        if (modified == original)
            return original;
        return Settle(modified, currency, minimum, wholeRealMoney);
    }

    internal static float Settle(float value, Currency currency, float minimum, bool wholeRealMoney)
    {
        value = Mathf.Clamp(value, minimum, MaxPrice);
        if (currency == Currency.RM && !wholeRealMoney)
            return Money.RoundRM(value);
        return (float)Math.Round(value);
    }

    /// <summary>The smallest charge at a trader: 1 OC, or one tenth of a Real Money.</summary>
    internal static float TraderMinimum(Currency currency) => currency == Currency.RM ? 0.1f : 1f;

    /// <summary>
    /// What one <paramref name="item"/> costs the player at a shop that sells it, or null when it doesn't.
    /// Traders sell from storage, so the price is worked out the way the game does; vending machines and
    /// checkouts already carry the modifiers because their own price method is hooked.
    /// </summary>
    internal static float? UnitPrice(ShopInfo shop, Item item, out Currency currency)
    {
        currency = Currency.OC;
        if (shop == null || item == null || !shop.IsAlive)
            return null;

        switch (shop.Component)
        {
            case Trade trade:
                currency = trade.purchasePriceMode == Trade.PriceMode.OnlyRM ? Currency.RM : Currency.OC;
                float basePrice = Trade.GetItemPrice(item, 0, null, currency, trade.basePurchasePrice, trade.stolenPriceFactor);
                return Apply(shop, item, null, true, currency, basePrice, TraderMinimum(currency), false);
            case VendingMachine machine:
                foreach (var entry in machine.sellableItems)
                {
                    if (entry == null || entry.item == null || entry.item.ID != item.ID)
                        continue;
                    currency = entry.currency;
                    return entry.GetPrice();
                }
                return null;
            case MarketShop market:
                currency = market.currency;
                return market.GetItemSalePrice(item);
            default:
                return null;
        }
    }
}
