using System.Collections.Generic;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>A snapshot of one player-run shop. Take a new one after a change.</summary>
public sealed class PlayerShopInfo
{
    internal PlayerShopInfo(PlayerShopRecord record, int shelvesLoaded)
    {
        Id = record.Id;
        Name = record.Name;
        Area = record.Area;
        Containers = record.Containers.ToArray();
        Open = record.Open;
        Markup = record.Markup;
        Currency = record.RealMoney ? Currency.RM : Currency.OC;
        TillCoins = record.TillCoins;
        TillRealMoney = record.TillRealMoney;
        FixedPrices = new Dictionary<int, float>(record.Prices);
        ShelvesLoaded = shelvesLoaded;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>GUID of the building area whose containers are shelves, or null.</summary>
    public string Area { get; }

    /// <summary>Container ids added as shelves one by one.</summary>
    public string[] Containers { get; }

    /// <summary>A closed shop sells nothing.</summary>
    public bool Open { get; }

    /// <summary>The factor on an item's sale value for items without a fixed price.</summary>
    public float Markup { get; }

    /// <summary>What the shop sells for. The till keeps both currencies.</summary>
    public Currency Currency { get; }

    /// <summary>Coins taken and not yet collected.</summary>
    public int TillCoins { get; }

    /// <summary>Real Money taken and not yet collected.</summary>
    public float TillRealMoney { get; }

    /// <summary>Fixed prices by item id, in <see cref="Currency"/>.</summary>
    public IReadOnlyDictionary<int, float> FixedPrices { get; }

    /// <summary>How many of the shop's shelves are in the loaded scenes. 0 means it can't sell right now.</summary>
    public int ShelvesLoaded { get; }

    public override string ToString() => $"{Name} ({Id}){(Open ? "" : " closed")}";
}

/// <summary>One stack on a player-run shop's shelves.</summary>
public sealed class PlayerShopLine
{
    internal PlayerShopLine(string container, int slot, Item item, int amount, float price, bool stolen)
    {
        Container = container;
        Slot = slot;
        Item = item;
        Amount = amount;
        Price = price;
        Stolen = stolen;
    }

    /// <summary>The shelf's container id; pass it with <see cref="Slot"/> to <see cref="PlayerShops.Sell"/>.</summary>
    public string Container { get; }

    public int Slot { get; }

    public Item Item { get; }

    public int Amount { get; }

    /// <summary>The price of one in the shop's currency; 0 means not for sale.</summary>
    public float Price { get; }

    /// <summary>The stack belongs to a character (stolen goods).</summary>
    public bool Stolen { get; }
}

/// <summary>A sale at a player-run shop, raised by <see cref="PlayerShops.Sold"/>.</summary>
public readonly struct PlayerShopSale
{
    internal PlayerShopSale(string shop, Item item, int amount, float total, Currency currency, bool stolen, bool byWorker)
    {
        Shop = shop;
        Item = item;
        Amount = amount;
        Total = total;
        Currency = currency;
        Stolen = stolen;
        ByWorker = byWorker;
    }

    public string Shop { get; }

    public Item Item { get; }

    public int Amount { get; }

    /// <summary>What went into the till.</summary>
    public float Total { get; }

    public Currency Currency { get; }

    /// <summary>The goods were stolen.</summary>
    public bool Stolen { get; }

    /// <summary>The caller said a worker made the sale rather than the player.</summary>
    public bool ByWorker { get; }
}

/// <summary>How a <see cref="PlayerShops.Sell"/> went.</summary>
public enum ShopSaleResult
{
    Ok,
    /// <summary>No world, or a save is being written or read.</summary>
    NotReady,
    NoShop,
    Closed,
    /// <summary>The container is not one of the shop's loaded shelves.</summary>
    NotAShelf,
    /// <summary>The slot doesn't hold that many of that item.</summary>
    NoItem,
    /// <summary>The item's price is 0.</summary>
    NotForSale,
    Failed,
}
