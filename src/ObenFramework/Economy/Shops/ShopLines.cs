using System.Collections.Generic;
using ObenFramework.Items;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>One line of a shop's stock: an item, how many are there and what one costs the player.</summary>
public sealed class StockLine
{
    internal StockLine(Item item, int amount, float price, Currency currency)
    {
        Item = item;
        Amount = amount;
        Price = price;
        Currency = currency;
    }

    public Item Item { get; }

    public int Amount { get; internal set; }

    /// <summary>The price of one, modifiers from <see cref="Prices"/> included.</summary>
    public float Price { get; }

    public Currency Currency { get; }
}

/// <summary>Reads the goods of traders and vending machines.</summary>
internal static class ShopLines
{
    internal static List<StockLine> Of(ShopInfo shop)
    {
        var lines = new List<StockLine>();
        if (shop == null || !shop.IsAlive)
            return lines;

        switch (shop.Component)
        {
            case Trade trade:
                FromTrader(shop, trade, lines);
                break;
            case VendingMachine machine:
                FromVending(machine, lines);
                break;
        }
        return lines;
    }

    private static void FromTrader(ShopInfo shop, Trade trade, List<StockLine> lines)
    {
        var storage = trade.GetComponent<Storage>();
        if (storage == null || storage.storageSlots == null)
            return;

        foreach (var slot in storage.storageSlots)
        {
            if (slot == null || slot.itemId <= 0 || slot.itemAmount <= 0)
                continue;
            var item = ItemLookup.ById(slot.itemId);
            if (item == null)
                continue;

            float price = ShopPricing.UnitPrice(shop, item, out var currency) ?? 0f;
            // Slots of the same item and price are one line; a trader often has several stacks.
            var same = lines.Find(l => l.Item == item && l.Price == price && l.Currency == currency);
            if (same != null)
                same.Amount += slot.itemAmount;
            else
                lines.Add(new StockLine(item, slot.itemAmount, price, currency));
        }
    }

    private static void FromVending(VendingMachine machine, List<StockLine> lines)
    {
        if (machine.sellableItems == null)
            return;
        foreach (var entry in machine.sellableItems)
        {
            var item = entry != null && entry.item != null ? entry.item.Item : null;
            if (item != null)
                lines.Add(new StockLine(item, entry.currentStock, entry.GetPrice(), entry.currency));
        }
    }
}
