using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Economy;

/// <summary>
/// Vending machine side of <see cref="ShopStock"/>. A machine sells whatever is in its
/// <c>sellableItems</c> array, so a registered entry is appended there. The game saves that array by
/// position and would read an extra entry back as a blank one, so mod entries are taken out while the game
/// saves or loads the machine and put back afterwards; their remaining stock goes into the framework's own
/// save data instead.
/// </summary>
internal static class ShopStockVending
{
    /// <summary>Appends a record's entry to every matching machine that is loaded.</summary>
    internal static void Place(StockRecord record)
    {
        record.Placements.RemoveAll(p => p.Machine == null);
        foreach (var machine in Object.FindObjectsByType<VendingMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var shop = ShopIndex.Of(machine);
            if (shop != null && shop.Matches(record.Shop))
                Place(machine, record, restoreStock: false);
        }
    }

    /// <summary>The machine started or loaded: add every matching entry, with the stock the save remembers.</summary>
    internal static void Attach(VendingMachine machine, bool restoreStock)
    {
        if (ShopStock.All.Count == 0)
            return;
        var shop = ShopIndex.Of(machine);
        if (shop == null)
            return;
        foreach (var record in ShopStock.All)
        {
            if (record.Vending && shop.Matches(record.Shop))
                Place(machine, record, restoreStock);
        }
    }

    /// <summary>Takes the mod entries out of a machine so the game sees its own array. Returns true when any were removed.</summary>
    internal static bool Detach(VendingMachine machine, bool rememberStock)
    {
        if (machine.sellableItems == null)
            return false;

        bool any = false;
        var shop = ShopIndex.Of(machine);
        foreach (var record in ShopStock.All)
        {
            var placement = record.Placements.Find(p => p.Machine == machine);
            if (placement == null || Array.IndexOf(machine.sellableItems, placement.Entry) < 0)
                continue;
            if (rememberStock && shop != null && ShopStock.Save != null)
                ShopStock.Save.VendingStock[StockKey(shop, record.ItemId)] = placement.Entry.currentStock;
            Remove(machine, placement.Entry);
            any = true;
        }
        return any;
    }

    internal static void Undo(StockRecord record)
    {
        foreach (var placement in record.Placements)
        {
            if (placement.Machine != null)
                Remove(placement.Machine, placement.Entry);
        }
        record.Placements.Clear();
    }

    private static void Place(VendingMachine machine, StockRecord record, bool restoreStock)
    {
        var shop = ShopIndex.Of(machine);
        var existing = record.Placements.Find(p => p.Machine == machine);
        if (existing != null)
        {
            if (machine.sellableItems == null || Array.IndexOf(machine.sellableItems, existing.Entry) < 0)
                Append(machine, existing.Entry);
            if (restoreStock)
                RestoreStock(shop, record, existing.Entry);
            return;
        }

        if (machine.sellableItems != null && SellsAlready(machine, record.ItemId))
        {
            if (!record.Warned)
            {
                record.Warned = true;
                GameEvents.Log?.Warn($"{record.OwnerId}: {machine.name} already sells item {record.ItemId}, its own entry is kept.");
            }
            return;
        }

        var item = ItemLookup.ById(record.ItemId);
        if (item == null)
            return;

        var entry = new VendingMachine.VendingMachineItem
        {
            item = new ItemReference(item),
            stock = record.Amount,
            currentStock = record.Amount,
            currency = record.Currency,
        };
        if (record.Price > 0f)
            entry.absolutePrice = record.Price;
        Append(machine, entry);
        record.Placements.Add(new Placement(machine, entry));
        ShopIndex.Adopt(machine, entry);
        RestoreStock(shop, record, entry);
    }

    private static void RestoreStock(ShopInfo shop, StockRecord record, VendingMachine.VendingMachineItem entry)
    {
        var save = ShopStock.Save;
        if (shop == null || save == null || !save.VendingStock.TryGetValue(StockKey(shop, record.ItemId), out int stock))
            return;
        entry.currentStock = Mathf.Clamp(stock, 0, entry.stock);
    }

    private static string StockKey(ShopInfo shop, int itemId) => shop.Id + "|" + itemId;

    private static bool SellsAlready(VendingMachine machine, int itemId)
    {
        foreach (var entry in machine.sellableItems)
        {
            if (entry != null && entry.item != null && entry.item.ID == itemId)
                return true;
        }
        return false;
    }

    private static void Append(VendingMachine machine, VendingMachine.VendingMachineItem entry)
    {
        int length = machine.sellableItems != null ? machine.sellableItems.Length : 0;
        var grown = new VendingMachine.VendingMachineItem[length + 1];
        if (length > 0)
            Array.Copy(machine.sellableItems, grown, length);
        grown[length] = entry;
        machine.sellableItems = grown;
    }

    private static void Remove(VendingMachine machine, VendingMachine.VendingMachineItem entry)
    {
        if (machine.sellableItems == null)
            return;
        var kept = new List<VendingMachine.VendingMachineItem>(machine.sellableItems);
        if (kept.Remove(entry))
            machine.sellableItems = kept.ToArray();
    }
}
