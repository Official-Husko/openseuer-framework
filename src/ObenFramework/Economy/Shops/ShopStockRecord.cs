using System.Collections.Generic;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>What a mod asked <see cref="ShopStock"/> to keep in a shop. Read-only view for listing.</summary>
public sealed class StockEntry
{
    internal StockEntry(StockRecord record)
    {
        Owner = record.OwnerId;
        Shop = record.Shop;
        ItemId = record.ItemId;
        Vending = record.Vending;
        Amount = record.Amount;
        Price = record.Price;
        Currency = record.Currency;
        Once = record.Once;
    }

    /// <summary>Id of the mod that added it.</summary>
    public string Owner { get; }

    /// <summary>The shop key the mod gave: an address, GUID or name.</summary>
    public string Shop { get; }

    public int ItemId { get; }

    /// <summary>True for a vending machine entry, false for trader stock.</summary>
    public bool Vending { get; }

    /// <summary>Trader: the amount topped up to. Vending machine: the stock it refills to.</summary>
    public int Amount { get; }

    /// <summary>Vending machine price, or 0 when the item uses the game's default price.</summary>
    public float Price { get; }

    public Currency Currency { get; }

    /// <summary>Trader stock put in once per save and never topped up.</summary>
    public bool Once { get; }
}

/// <summary>One registration. The same record serves trader and vending stock.</summary>
internal sealed class StockRecord
{
    internal string OwnerId;
    internal string Shop;
    internal int ItemId;
    internal bool Vending;
    internal int Amount;
    internal float Price;
    internal Currency Currency;
    internal bool Once;

    /// <summary>Trader stock this record put into storages since it was added, so removing it takes back that much at most.</summary>
    internal int Placed;

    internal readonly List<Placement> Placements = new List<Placement>();
    internal bool Warned;

    internal string PlacedKey(ShopInfo shop) => OwnerId + "|" + shop.Id + "|" + ItemId + (Once ? "|once" : "");
}

/// <summary>An entry this record added to one vending machine.</summary>
internal sealed class Placement
{
    internal readonly VendingMachine Machine;
    internal readonly VendingMachine.VendingMachineItem Entry;

    internal Placement(VendingMachine machine, VendingMachine.VendingMachineItem entry)
    {
        Machine = machine;
        Entry = entry;
    }
}

/// <summary>Per-save state of <see cref="ShopStock"/>, kept by <c>ModSaves</c> next to the game's save.</summary>
internal sealed class ShopStockSave
{
    /// <summary>Trader stock already put in, so loading doesn't refill what the player bought.</summary>
    public List<string> Placed = new List<string>();

    /// <summary>Stock of mod entries in vending machines, keyed by machine address and item id.</summary>
    public Dictionary<string, int> VendingStock = new Dictionary<string, int>();
}
