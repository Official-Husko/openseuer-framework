// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

public static partial class PlayerShops
{
    /// <summary>The price of one <paramref name="item"/> at the shop, in its currency, or null for an unknown shop. 0 means not for sale.</summary>
    public static float? PriceOf(string id, Item item, object[] meta = null)
    {
        var record = Record(id);
        if (record == null || item == null)
            return null;
        return UnitPrice(record, item, meta);
    }

    /// <summary>What is on the shop's loaded shelves, stack by stack, with prices.</summary>
    public static List<PlayerShopLine> Stock(string id)
    {
        var lines = new List<PlayerShopLine>();
        var record = Record(id);
        if (record == null)
            return lines;
        foreach (var shelf in PlayerShopShelves.Of(record))
        {
            string container = Containers.IdOf(shelf);
            for (int i = 0; i < shelf.Slots.Length; i++)
            {
                var stack = shelf.Slots[i] != null ? shelf.Slots[i].itemStack : null;
                if (stack == null || stack.itemId <= 1 || stack.itemAmount <= 0)
                    continue;
                var item = ItemLookup.ById(stack.itemId);
                if (item != null)
                    lines.Add(new PlayerShopLine(container, i, item, stack.itemAmount, UnitPrice(record, item, stack.Meta), stack.ownerId > 0));
            }
        }
        return lines;
    }

    /// <summary>The shop's loaded shelves.</summary>
    public static List<Storage> Shelves(string id) => PlayerShopShelves.Of(Record(id));

    /// <summary>
    /// Sells <paramref name="count"/> of the item in one slot of one shelf: the goods leave the shelf and their
    /// price goes into the till, and <see cref="Sold"/> is raised. Nothing changes unless the shop is open, the
    /// slot holds that many of <paramref name="itemId"/> and the item has a price.
    /// </summary>
    /// <param name="byWorker">Passed on in the event, for mods that tell player and worker sales apart.</param>
    public static ShopSaleResult Sell(string id, string containerId, int slot, int itemId, int count, bool byWorker = false)
    {
        if (!Writable())
            return ShopSaleResult.NotReady;
        var record = Record(id);
        if (record == null)
            return ShopSaleResult.NoShop;
        if (!record.Open)
            return ShopSaleResult.Closed;
        var shelf = PlayerShopShelves.Find(record, containerId);
        if (shelf == null)
            return ShopSaleResult.NotAShelf;
        var slotController = slot >= 0 && slot < shelf.Slots.Length ? shelf.Slots[slot] : null;
        var stack = slotController != null ? slotController.itemStack : null;
        var item = stack != null ? ItemLookup.ById(stack.itemId) : null;
        if (stack == null || item == null || stack.itemId != itemId || count <= 0 || stack.itemAmount < count)
            return ShopSaleResult.NoItem;

        float unit = UnitPrice(record, item, stack.Meta);
        if (!PlayerShopMath.CanSell(stack.itemId, itemId, stack.itemAmount, count, unit))
            return ShopSaleResult.NotForSale;
        float total = PlayerShopMath.Total(unit, count, record.RealMoney);
        bool stolen = stack.ownerId > 0;

        try
        {
            int before = stack.itemAmount;
            using (WorldChanges.Enter(ChangeSource.Mod))
                slotController.TakeItem(count);
            if (before - stack.itemAmount != count)
                return ShopSaleResult.Failed;
        }
        catch (Exception e)
        {
            WorldLog.Failed("PlayerShops.Sell", e);
            return ShopSaleResult.Failed;
        }
        PlayerShopMath.Credit(ref record.TillCoins, ref record.TillRealMoney, record.RealMoney, total);
        if (Sold.HasSubscribers)
            Sold.Raise(new PlayerShopSale(record.Id, item, count, total, record.RealMoney ? Currency.RM : Currency.OC, stolen, byWorker));
        return ShopSaleResult.Ok;
    }

    /// <summary>Moves everything in the till to the wallet, with the game's "money added" note. False when the till is empty or no world is loaded.</summary>
    public static bool CollectEarnings(string id)
    {
        var record = Writable() ? Record(id) : null;
        if (record == null || !Wallet.CanChange || (record.TillCoins <= 0 && record.TillRealMoney <= 0f))
            return false;
        if (!Fits(record))
            return false;
        var amount = new Money.money(record.TillCoins, record.TillRealMoney);
        record.TillCoins = 0;
        record.TillRealMoney = 0f;
        Money.instance.AddMoney(amount, notify: true);
        return true;
    }

    /// <summary>Puts money into the till (a tip, a delivery paid on account).</summary>
    public static bool AddEarnings(string id, Currency currency, float amount)
    {
        var record = Writable() ? Record(id) : null;
        return record != null && PlayerShopMath.Credit(ref record.TillCoins, ref record.TillRealMoney, currency == Currency.RM, amount);
    }

    /// <summary>Takes money out of the till (wages, a theft). False, taking nothing, when the till holds less.</summary>
    public static bool TakeEarnings(string id, Currency currency, float amount)
    {
        var record = Writable() ? Record(id) : null;
        return record != null && PlayerShopMath.Debit(ref record.TillCoins, ref record.TillRealMoney, currency == Currency.RM, amount);
    }

    private static float UnitPrice(PlayerShopRecord record, Item item, object[] meta) =>
        PlayerShopMath.UnitPrice(item.GetItemSalePrice(meta), record.Markup, record.RealMoney, record.Prices, item.ID);

    private static bool Fits(PlayerShopRecord record) =>
        (long)Wallet.Coins + record.TillCoins <= int.MaxValue &&
        (long)Wallet.ToTenths(Wallet.RealMoney) + Wallet.ToTenths(record.TillRealMoney) <= int.MaxValue;
}
