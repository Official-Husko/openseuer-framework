using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Economy;

/// <summary>
/// Trader side of <see cref="ShopStock"/>. A trader's goods are the slots of its <c>Storage</c>; stock is
/// written into empty slots with the storage's own private slot method, so stack limits and category filters
/// of the game still apply.
/// </summary>
internal static class ShopStockTraders
{
    private delegate bool AddToSlot(Storage storage, ItemReference item, int slot, int amount, object[] meta, List<int> skins);

    private static readonly AddToSlot AddItemToSlot = (AddToSlot)Delegate.CreateDelegate(
        typeof(AddToSlot), AccessTools.Method(typeof(Storage), "AddItemToStorageSlot"));

    private static bool _fullLogged;

    /// <summary>A trader finished loading its save: put in stock this save doesn't have yet.</summary>
    internal static void Loaded(Storage storage)
    {
        if (ShopStock.All.Count == 0)
            return;
        var trade = storage.GetComponent<Trade>();
        if (trade != null)
            TopUp(trade, restock: false);
    }

    /// <summary>A trader refilled its goods: bring registered stock back up to its amount.</summary>
    internal static void Restocked(Trade trade) => TopUp(trade, restock: true);

    /// <summary>Puts a record's stock into every matching trader that is loaded.</summary>
    internal static void Place(StockRecord record)
    {
        foreach (var trade in Object.FindObjectsByType<Trade>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var shop = ShopIndex.Of(trade);
            if (shop != null && shop.Matches(record.Shop))
                TopUp(trade, record, shop, restock: false);
        }
    }

    /// <summary>Takes back, from loaded traders, up to what the record put in. One-off stock stays where it is.</summary>
    internal static void Undo(StockRecord record)
    {
        if (record.Once)
            return;
        var save = ShopStock.Save;
        foreach (var trade in Object.FindObjectsByType<Trade>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var shop = ShopIndex.Of(trade);
            if (shop == null || !shop.Matches(record.Shop))
                continue;
            if (save != null)
                save.Placed.Remove(record.PlacedKey(shop));
            var storage = trade.GetComponent<Storage>();
            if (storage != null && record.Placed > 0)
                record.Placed -= Take(storage, record.ItemId, record.Placed);
        }
        record.Placed = 0;
    }

    private static void TopUp(Trade trade, bool restock)
    {
        if (ShopStock.All.Count == 0)
            return;
        var shop = ShopIndex.Of(trade);
        if (shop == null)
            return;
        foreach (var record in ShopStock.All)
        {
            if (!record.Vending && shop.Matches(record.Shop))
                TopUp(trade, record, shop, restock);
        }
    }

    private static void TopUp(Trade trade, StockRecord record, ShopInfo shop, bool restock)
    {
        var save = ShopStock.Save;
        if (save == null)
            return;
        string key = record.PlacedKey(shop);
        // Outside of a restock the stock goes in once per save; otherwise every load would refill what was bought.
        // One-off stock never comes back, not even on a restock.
        if ((!restock || record.Once) && save.Placed.Contains(key))
            return;

        var storage = trade.GetComponent<Storage>();
        if (storage == null || storage.storageSlots == null)
            return;
        if (!restock || record.Once)
            save.Placed.Add(key);

        // One-off stock is its own pieces, put in whatever the trader already has of the item.
        int missing = record.Once ? record.Amount : record.Amount - Count(storage, record.ItemId);
        if (missing > 0)
            record.Placed += Fill(storage, record.ItemId, missing);
    }

    private static int Count(Storage storage, int itemId)
    {
        int total = 0;
        foreach (var slot in storage.storageSlots)
        {
            if (slot != null && slot.itemId == itemId)
                total += slot.itemAmount;
        }
        return total;
    }

    private static int Fill(Storage storage, int itemId, int wanted)
    {
        var item = ItemLookup.ById(itemId);
        if (item == null)
            return 0;

        var reference = new ItemReference(item);
        int done = 0;
        for (int i = 0; i < storage.storageSlots.Length && done < wanted; i++)
        {
            var slot = storage.storageSlots[i];
            if (slot == null || slot.itemId != -1)
                continue;
            if (AddItemToSlot(storage, reference, i, wanted - done, null, null))
                done += slot.itemAmount;
        }

        if (done < wanted && !_fullLogged)
        {
            _fullLogged = true;
            GameEvents.Log?.Warn($"{storage.name} has no empty slot left for item {itemId}; the rest of that stock is skipped.");
        }
        return done;
    }

    private static int Take(Storage storage, int itemId, int most)
    {
        int taken = 0;
        foreach (var slot in storage.storageSlots)
        {
            if (slot == null || slot.itemId != itemId || taken >= most)
                continue;
            int part = Math.Min(slot.itemAmount, most - taken);
            slot.itemAmount -= part;
            taken += part;
            if (slot.itemAmount <= 0)
            {
                slot.itemId = -1;
                slot.itemAmount = 0;
            }
        }
        return taken;
    }
}
