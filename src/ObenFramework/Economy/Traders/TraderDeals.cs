// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Saves;
using ObenFramework.World;
using OS.Items;
using UnityEngine;
using Currency = Money.Currency;
using Object = UnityEngine.Object;

namespace ObenFramework.Economy;

/// <summary>
/// Buying from and selling to a trader without the trade window. The goods move first, between the trader's
/// storage and the player's slots, and are counted on both sides; only then does the game's own
/// <c>Trade.Buy</c>/<c>Sell</c> move the money, so price modifiers, trader money, the trade balance and
/// <see cref="Shops.Bought"/>/<see cref="Shops.Sold"/> behave as in the window. When the goods don't add up,
/// every stack is put back and nothing is paid. The game's methods need an <c>ItemData</c>, which only exists
/// while a window shows the slot, so a hidden one stands in for it.
/// </summary>
internal static class TraderDeals
{
    private static bool _running;

    public static TradeResult Run(Trade trade, int itemId, int count, bool buy)
    {
        if (count < 1 || PlayerItems.IsMoney(itemId))
            return TradeResult.BadInput;
        if (_running || SaveGate.IsBusy)
            return TradeResult.Busy;
        if (!PlayerItems.IsReady || !Wallet.CanChange)
            return TradeResult.NotReady;
        var item = ItemLookup.ById(itemId);
        if (item == null || TakeMath.IsLiquid(item.Categories))
            return TradeResult.BadInput;
        if (trade == null)
            return TradeResult.UnknownShop;
        if (!trade.open)
            return TradeResult.Closed;
        if (trade is TradeSpecial)
            return TradeResult.Unsupported;
        var storage = trade.GetComponent<Storage>();
        if (storage == null || storage.Slots == null)
            return TradeResult.NotReady;

        var holder = new GameObject("ObenFramework trade");
        holder.SetActive(false);
        _running = true;
        try
        {
            var slot = holder.AddComponent<Slot>();
            slot.playerSlot = !buy;
            var data = holder.AddComponent<ItemData>();
            data.slotController = new SlotController { slot = slot };
            using (WorldChanges.EnterMod())
            {
                var result = buy ? Buy(trade, storage, data, item, count) : Sell(trade, storage, data, item, count);
                InventoryWatch.Check();
                return result;
            }
        }
        finally
        {
            _running = false;
            Object.Destroy(holder);
        }
    }

    private static TradeResult Buy(Trade trade, Storage storage, ItemData data, Item item, int count)
    {
        var lots = Lots(storage.Slots, item.ID, count, Plain, out int missing);
        if (missing > 0)
            return TradeResult.OutOfStock;
        var currency = trade.purchasePriceMode == Trade.PriceMode.OnlyRM ? Currency.RM : Currency.OC;
        if (!Wallet.CanAfford(currency, Total(trade, data, item, lots, currency)))
            return TradeResult.CannotAfford;

        var shelf = new StackSnapshot(storage.Slots);
        var pack = new StackSnapshot(PlayerSlots.Current());
        int shelfBefore = shelf.Units(item.ID);
        int packBefore = pack.Units(item.ID);
        var result = TradeResult.Failed;
        try
        {
            foreach (var lot in lots)
            {
                if (ItemOperations.AddItemAndReturnRemaining(item, lot.Owner, null, lot.Meta, lot.Amount) > 0)
                    break;
            }
            if (pack.Units(item.ID) - packBefore != count)
                result = TradeResult.NoRoom;
            else
            {
                foreach (var lot in lots)
                    lot.Slot.TakeItem(lot.Amount);
                if (shelfBefore - shelf.Units(item.ID) == count && Pay(trade, data, item, lots, currency, buy: true))
                    return TradeResult.Ok;
            }
        }
        catch (Exception e)
        {
            WorldLog.Failed("Traders.Buy", e);
        }
        shelf.Restore();
        pack.Restore();
        return result;
    }

    private static TradeResult Sell(Trade trade, Storage storage, ItemData data, Item item, int count)
    {
        var held = PlayerSlots.Current(includeEquipment: false).ToArray();
        var lots = Lots(held, item.ID, count, s => Sellable(trade, data, item, s), out int missing);
        if (missing > 0)
        {
            // Enough of them, only not all the trader takes.
            Lots(held, item.ID, count, s => !HoldsLiquid(s), out int lacking);
            return lacking == 0 ? TradeResult.NotAccepted : TradeResult.NotEnoughItems;
        }

        var currency = trade.sellPriceMode == Trade.PriceMode.OnlyRM ? Currency.RM : Currency.OC;
        foreach (var lot in lots)
        {
            Load(data, item, lot.Owner, lot.Meta, lot.Amount);
            if (!trade.CanSell(data, lot.Amount, false, checkMoney: false))
                return TradeResult.NoRoom;
        }
        if (!DifficultyController.InfiniteTraderWealth && trade.CurrentMoney < Total(trade, data, item, lots, currency))
            return TradeResult.ShopCannotAfford;

        var shelf = new StackSnapshot(storage.Slots);
        var pack = new StackSnapshot(PlayerSlots.Current());
        int shelfBefore = shelf.Units(item.ID);
        int packBefore = pack.Units(item.ID);
        var result = TradeResult.Failed;
        try
        {
            bool stored = true;
            foreach (var lot in lots)
            {
                if (!trade.destroyPurchasedItems && storage.AddItemAndReturnRemaining(item, lot.Amount, lot.Owner, lot.Meta) > 0)
                {
                    stored = false;
                    break;
                }
            }
            if (!stored || (!trade.destroyPurchasedItems && shelf.Units(item.ID) - shelfBefore != count))
                result = TradeResult.NoRoom;
            else
            {
                foreach (var lot in lots)
                    lot.Slot.TakeItem(lot.Amount);
                if (packBefore - pack.Units(item.ID) == count && Pay(trade, data, item, lots, currency, buy: false))
                    return TradeResult.Ok;
            }
        }
        catch (Exception e)
        {
            WorldLog.Failed("Traders.Sell", e);
        }
        shelf.Restore();
        pack.Restore();
        return result;
    }

    // What the lots come to: each stack at its own price, as quality and wear change it.
    private static float Total(Trade trade, ItemData data, Item item, List<Lot> lots, Currency currency)
    {
        float total = 0f;
        foreach (var lot in lots)
        {
            Load(data, item, lot.Owner, lot.Meta, lot.Amount);
            total += trade.GetItemStackPrice(data, lot.Amount, currency);
        }
        return total;
    }

    // The money steps, one per stack through the game's own Trade.Buy/Sell. After the checks above they can't
    // fail; if one does anyway, the money moved so far goes back (deal events already raised stay raised).
    private static bool Pay(Trade trade, ItemData data, Item item, List<Lot> lots, Currency currency, bool buy)
    {
        float wallet = Wallet.Get(currency);
        float till = trade.CurrentMoney;
        int balance = trade.cumulativeTradeBalance;
        foreach (var lot in lots)
        {
            Load(data, item, lot.Owner, lot.Meta, lot.Amount);
            if (buy ? trade.Buy(data, lot.Amount) : trade.Sell(data, lot.Amount))
                continue;
            float moved = Wallet.Get(currency) - wallet;
            if (moved > 0f)
                Money.instance.RemoveMoney(moved, currency);
            else if (moved < 0f)
                Money.instance.AddMoney(-moved, currency);
            trade.CurrentMoney = till;
            trade.cumulativeTradeBalance = balance;
            WorldLog.Failed(buy ? "Traders.Buy" : "Traders.Sell", new InvalidOperationException("the game refused a money step after the checks passed"));
            return false;
        }
        return true;
    }

    // The stacks to take from, front to back, each with a copy of its owner and details made before anything moves.
    private static List<Lot> Lots(IList<SlotController> slots, int itemId, int count, Func<ItemStack, bool> accept, out int missing)
    {
        var lots = new List<Lot>();
        missing = count;
        foreach (var slot in slots)
        {
            var stack = slot != null ? slot.itemStack : null;
            if (missing < 1 || stack == null || stack.itemId != itemId || stack.itemAmount < 1 || !accept(stack))
                continue;
            int amount = Math.Min(missing, stack.itemAmount);
            var item = stack.itemReference != null ? stack.itemReference.Item : null;
            lots.Add(new Lot { Slot = slot, Amount = amount, Owner = stack.ownerId, Meta = Item.CloneMeta(stack.Meta, false, amount, item) });
            missing -= amount;
        }
        return lots;
    }

    private static void Load(ItemData data, Item item, int owner, object[] meta, int amount)
    {
        var stack = new ItemStack { itemReference = item.Reference, itemId = item.ID, itemAmount = amount, ownerId = owner };
        stack.Meta = meta;
        data.stack = stack;
    }

    // What the trader sells as its own goods: nobody's, not stolen, no liquid inside.
    private static bool Plain(ItemStack stack) =>
        stack.ownerId <= 0 && stack.GetMetaOfType<ItemStolenData>() == null && !HoldsLiquid(stack);

    private static bool Sellable(Trade trade, ItemData data, Item item, ItemStack stack)
    {
        if (HoldsLiquid(stack))
            return false;
        Load(data, item, stack.ownerId, stack.Meta, 1);
        return !trade.IsStolen(data) && trade.CheckIfAllowed(data);
    }

    private static bool HoldsLiquid(ItemStack stack)
    {
        var liquid = stack.GetMetaOfType<ItemLiquidData>();
        return liquid != null && liquid.liquidItem != null && liquid.liquidItem.ID > 0 && liquid.liquidAmount > 0f;
    }

    private struct Lot
    {
        public SlotController Slot;
        public int Amount;
        public int Owner;
        public object[] Meta;
    }
}
