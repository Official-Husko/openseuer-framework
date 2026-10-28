using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>A completed purchase or sale, raised by <see cref="Shops.Bought"/> and <see cref="Shops.Sold"/>.</summary>
public readonly struct ShopDeal
{
    internal ShopDeal(ShopInfo shop, bool playerBought, Item item, Furniture furniture, int amount, int oc, float rm)
    {
        Shop = shop;
        PlayerBought = playerBought;
        Item = item;
        Furniture = furniture;
        Amount = amount;
        Oc = oc;
        Rm = rm;
    }

    public ShopInfo Shop { get; }

    /// <summary>Which way <see cref="Shop"/>.<see cref="ShopInfo.Kind"/> was used: the buying path (trader, checkout, vending machine, furniture or train shop).</summary>
    public ShopKind Path => Shop.Kind;

    /// <summary>True when the player paid, false when the player was paid.</summary>
    public bool PlayerBought { get; }

    /// <summary>The item, or null for furniture and train deals.</summary>
    public Item Item { get; }

    /// <summary>The furniture, or null for item deals.</summary>
    public Furniture Furniture { get; }

    public int Amount { get; }

    /// <summary>Open Sewer Coin that changed hands. Furniture can cost both currencies at once.</summary>
    public int Oc { get; }

    /// <summary>Real Money that changed hands.</summary>
    public float Rm { get; }

    /// <summary>The currency of the deal; Real Money only when no coins were involved.</summary>
    public Currency Currency => Oc == 0 && Rm > 0f ? Currency.RM : Currency.OC;

    /// <summary>The total in <see cref="Currency"/>.</summary>
    public float Price => Currency == Currency.RM ? Rm : Oc;
}
