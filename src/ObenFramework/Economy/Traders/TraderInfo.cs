using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>A snapshot of one NPC trader (the game's <c>Trade</c>): money, terms and what it deals in.</summary>
public sealed class TraderInfo
{
    internal TraderInfo(ShopInfo shop, Trade trade)
    {
        Shop = shop;
        Open = trade.open;
        OpensByClock = trade.autoOpenClose;
        Money = trade.CurrentMoney;
        MoneyMin = trade.traderMoneyAmountMin;
        MoneyMax = trade.traderMoneyAmountMax;
        SellsFor = trade.purchasePriceMode == Trade.PriceMode.OnlyRM ? Currency.RM : Currency.OC;
        BuysFor = trade.sellPriceMode == Trade.PriceMode.OnlyRM ? Currency.RM : Currency.OC;
        Markup = trade.basePurchasePrice;
        Payout = trade.baseSellPrice;
        BuysStolen = trade.buysStolen;
        StolenFactor = trade.stolenPriceFactor;
        AllowedCategories = (string[])(trade.allowedCategories ?? new string[0]).Clone();
        ForbiddenCategories = (string[])(trade.forbiddenCategories ?? new string[0]).Clone();
        DestroysBought = trade.destroyPurchasedItems;
        Special = trade is TradeSpecial;
        BuyFactor = TraderFactors.For(shop, true);
        SellFactor = TraderFactors.For(shop, false);
    }

    public ShopInfo Shop { get; }

    /// <summary>Open for business right now.</summary>
    public bool Open { get; }

    /// <summary>True when the trader opens and closes by its opening hours (see <see cref="ShopHours"/>).</summary>
    public bool OpensByClock { get; }

    /// <summary>What the trader has left to pay the player with when buying from them (see <see cref="BuysFor"/>).</summary>
    public float Money { get; }

    /// <summary>The range the trader's money is refilled within (before the difficulty's wealth setting).</summary>
    public int MoneyMin { get; }

    public int MoneyMax { get; }

    /// <summary>The currency the player pays in when buying.</summary>
    public Currency SellsFor { get; }

    /// <summary>The currency the player is paid in when selling.</summary>
    public Currency BuysFor { get; }

    /// <summary>The game's factor on an item's value when the player buys (1.2 is 120%).</summary>
    public float Markup { get; }

    /// <summary>The game's factor on an item's value when the player sells (0.9 is 90%).</summary>
    public float Payout { get; }

    public bool BuysStolen { get; }

    /// <summary>Factor on stolen goods' prices.</summary>
    public float StolenFactor { get; }

    /// <summary>Item categories the trader takes; empty means any not forbidden.</summary>
    public string[] AllowedCategories { get; }

    public string[] ForbiddenCategories { get; }

    /// <summary>The trader throws away what it buys instead of reselling it.</summary>
    public bool DestroysBought { get; }

    /// <summary>A scripted trader (<c>TradeSpecial</c>) with its own rules.</summary>
    public bool Special { get; }

    /// <summary>The mods' combined factor on prices the player pays here (see <see cref="Traders.SetPriceFactors"/>).</summary>
    public float BuyFactor { get; }

    /// <summary>The mods' combined factor on prices the player is paid here.</summary>
    public float SellFactor { get; }
}
