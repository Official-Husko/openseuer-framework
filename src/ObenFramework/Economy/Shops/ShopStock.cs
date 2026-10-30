using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// Puts goods into traders and vending machines, vanilla items and mod items alike. A shop is named by its
/// address (<see cref="ShopInfo.Id"/>), GUID or name; a name applies to every shop that has it. Stock can be
/// added before the shop's scene is loaded: it is placed whenever the shop appears. Everything added here is
/// removed again when the adding mod unloads.
/// </summary>
/// <remarks>
/// Trader stock is put in once per save and topped back up to its amount every time the trader restocks;
/// what the player buys in between stays bought. Stock added with <see cref="AddToTraderOnce(ObenMod, string, int, int)"/>
/// is never topped up. Vending entries are real entries of the machine: they
/// sell for a fixed price, refill with the machine, and their remaining stock is saved with the game. A
/// vending machine that already sells the item keeps its own entry.
/// </remarks>
public static class ShopStock
{
    /// <summary>True when the restock hooks are in place, so added stock is put in and topped up.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Shops");

    private static readonly List<StockRecord> Records = new List<StockRecord>();
    private static ModSaveData<ShopStockSave> _save;

    internal static IReadOnlyList<StockRecord> All => Records;

    internal static ShopStockSave Save => _save != null ? _save.Value : null;

    /// <summary>Everything currently registered, by every mod.</summary>
    public static List<StockEntry> Entries()
    {
        var list = new List<StockEntry>(Records.Count);
        foreach (var record in Records)
            list.Add(new StockEntry(record));
        return list;
    }

    /// <summary>
    /// Keeps a trader stocked with <paramref name="amount"/> of an item. Disposing the result, or unloading
    /// <paramref name="owner"/>, takes back what was put in. Adding the same item to the same shop again replaces the amount.
    /// </summary>
    public static IDisposable AddToTrader(ObenMod owner, string shop, int itemId, int amount) =>
        Add(owner, shop, itemId, vending: false, amount, 0f, Currency.OC);

    /// <inheritdoc cref="AddToTrader(ObenMod, string, int, int)"/>
    public static IDisposable AddToTrader(ObenMod owner, string shop, Item item, int amount) =>
        AddToTrader(owner, shop, IdOf(item), amount);

    /// <summary>
    /// Puts <paramref name="amount"/> of an item into a trader once per save, the first time the trader is
    /// loaded with it registered, and never tops it up: a one-off piece the player can buy and that is then
    /// gone. Disposing the result, or unloading <paramref name="owner"/>, stops it reaching traders not stocked
    /// yet; what was put in stays.
    /// </summary>
    public static IDisposable AddToTraderOnce(ObenMod owner, string shop, int itemId, int amount) =>
        Add(owner, shop, itemId, vending: false, amount, 0f, Currency.OC, once: true);

    /// <inheritdoc cref="AddToTraderOnce(ObenMod, string, int, int)"/>
    public static IDisposable AddToTraderOnce(ObenMod owner, string shop, Item item, int amount) =>
        AddToTraderOnce(owner, shop, IdOf(item), amount);

    /// <summary>
    /// Adds an item to a vending machine with room for <paramref name="stock"/> of it. <paramref name="price"/> is
    /// the price in <paramref name="currency"/>; 0 (or less) uses the game's default of 120% of the item's value.
    /// </summary>
    public static IDisposable AddToVending(ObenMod owner, string shop, int itemId, int stock, float price = 0f, Currency currency = Currency.OC) =>
        Add(owner, shop, itemId, vending: true, stock, price, currency);

    /// <inheritdoc cref="AddToVending(ObenMod, string, int, int, float, Currency)"/>
    public static IDisposable AddToVending(ObenMod owner, string shop, Item item, int stock, float price = 0f, Currency currency = Currency.OC) =>
        AddToVending(owner, shop, IdOf(item), stock, price, currency);

    /// <summary>Removes what <paramref name="owner"/> added for this shop key and item. Returns false when there was nothing.</summary>
    public static bool Remove(ObenMod owner, string shop, int itemId)
    {
        bool removed = false;
        foreach (var record in Records.ToArray())
        {
            if (record.OwnerId == owner.Info.Id && record.Shop == shop && record.ItemId == itemId)
                removed |= Drop(record);
        }
        return removed;
    }

    internal static void Init(ObenMod framework)
    {
        _save = ModSaves.Register<ShopStockSave>(framework, "shopstock");
        GameEvents.WhenWorldReady(framework, _ => PlaceEverywhere());
        framework.Track(Disposable.Create(() =>
        {
            Records.Clear();
            _save = null;
        }));
    }

    private static int IdOf(Item item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));
        return item.ID;
    }

    private static IDisposable Add(ObenMod owner, string shop, int itemId, bool vending, int amount, float price, Currency currency, bool once = false)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(shop))
            throw new ArgumentException("A shop address, GUID or name is required.", nameof(shop));
        if (amount < 1)
            throw new ArgumentOutOfRangeException(nameof(amount), "At least 1.");
        if (float.IsNaN(price) || float.IsInfinity(price))
            throw new ArgumentOutOfRangeException(nameof(price), "Must be a finite number.");
        if (PlayerItems.IsMoney(itemId) || !ItemLookup.Exists(itemId))
            throw new ArgumentException($"Item {itemId} does not exist or is a currency. Register mod items before stocking them.", nameof(itemId));

        // Same mod, shop and item replaces the earlier registration.
        foreach (var old in Records.ToArray())
        {
            if (old.OwnerId == owner.Info.Id && old.Shop == shop && old.ItemId == itemId && old.Vending == vending && old.Once == once)
                Drop(old);
        }

        var record = new StockRecord
        {
            OwnerId = owner.Info.Id, Shop = shop, ItemId = itemId, Vending = vending,
            Amount = amount, Price = price, Currency = currency, Once = once,
        };
        Records.Add(record);
        var handle = Disposable.Create(() => Drop(record));
        owner.Track(handle);

        if (vending)
            ShopStockVending.Place(record);
        else
            ShopStockTraders.Place(record);
        return handle;
    }

    private static bool Drop(StockRecord record)
    {
        if (!Records.Remove(record))
            return false;
        if (record.Vending)
            ShopStockVending.Undo(record);
        else
            ShopStockTraders.Undo(record);
        return true;
    }

    private static void PlaceEverywhere()
    {
        foreach (var record in Records.ToArray())
        {
            if (record.Vending)
                ShopStockVending.Place(record);
            else
                ShopStockTraders.Place(record);
        }
    }
}
