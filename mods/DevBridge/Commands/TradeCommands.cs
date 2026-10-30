using System;
using System.Collections.Generic;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Economy;

namespace DevBridge.Commands;

/// <summary>Buying and selling at traders and furniture shops without their windows.</summary>
internal static class TradeCommands
{
    private static readonly Dictionary<string, IDisposable> Stocked = new Dictionary<string, IDisposable>();

    [BridgeCommand("shops.addstock", "Stock a trader or vending machine with an item (owned by the bridge), or take the bridge's stock back.",
        Params = new[]
        {
            "shop: address, GUID or name", "item: item id", "amount?: default 1", "vending?: true for a vending machine",
            "once?: true for one-off trader stock that is never topped up", "price?: vending price", "clear?: true removes it",
        })]
    private static object AddStock(JObject p)
    {
        string shop = p.Str("shop");
        int item = (int)p.Float("item");
        bool vending = p.Bool("vending", false);
        bool once = p.Bool("once", false);
        string key = shop + "|" + item + "|" + vending + "|" + once;
        if (Stocked.TryGetValue(key, out var old))
        {
            old.Dispose();
            Stocked.Remove(key);
        }
        if (!p.Bool("clear", false))
        {
            int amount = p.Int("amount", 1);
            Stocked[key] = vending
                ? ShopStock.AddToVending(DevBridgeMod.Instance, shop, item, amount, p.Float("price", 0f))
                : once
                    ? ShopStock.AddToTraderOnce(DevBridgeMod.Instance, shop, item, amount)
                    : ShopStock.AddToTrader(DevBridgeMod.Instance, shop, item, amount);
        }
        return new JArray(ShopStock.Entries().Select(e => new JObject
        {
            ["owner"] = e.Owner,
            ["shop"] = e.Shop,
            ["item"] = e.ItemId,
            ["vending"] = e.Vending,
            ["once"] = e.Once,
            ["amount"] = e.Amount,
        }).ToArray());
    }

    [BridgeCommand("traders.buy", "The player buys items from a trader without the trade window.",
        Params = new[] { "shop: address, GUID or name", "item: item id", "count?: default 1" })]
    private static object Buy(JObject p) => Result(Traders.Buy(p.Str("shop"), (int)p.Float("item"), p.Int("count", 1)));

    [BridgeCommand("traders.sell", "The player sells items to a trader without the trade window.",
        Params = new[] { "shop: address, GUID or name", "item: item id", "count?: default 1" })]
    private static object Sell(JObject p) => Result(Traders.Sell(p.Str("shop"), (int)p.Float("item"), p.Int("count", 1)));

    [BridgeCommand("furnitureshops.stock", "What a furniture or train shop has on offer, with prices.", Params = new[] { "shop: address, GUID or name" })]
    private static object Stock(JObject p) => new JArray(FurnitureShops.Stock(p.Str("shop")).Select(l => new JObject
    {
        ["id"] = l.Id,
        ["title"] = l.Title,
        ["amount"] = l.Amount,
        ["oc"] = l.Coins,
        ["rm"] = l.RealMoney,
        ["needsPicture"] = l.NeedsPicture,
    }).ToArray());

    [BridgeCommand("furnitureshops.buy", "The player buys furniture from a furniture shop without its window.",
        Params = new[] { "shop: address, GUID or name", "furniture: the game's furniture id or title", "count?: default 1" })]
    private static object BuyFurniture(JObject p) => Result(FurnitureShops.Buy(p.Str("shop"), p.Str("furniture"), p.Int("count", 1)));

    [BridgeCommand("furnitureshops.sell", "The player sells unplaced furniture to a furniture shop without its window.",
        Params = new[] { "shop: address, GUID or name", "furniture: the game's furniture id or title", "count?: default 1" })]
    private static object SellFurniture(JObject p) => Result(FurnitureShops.Sell(p.Str("shop"), p.Str("furniture"), p.Int("count", 1)));

    private static JObject Result(TradeResult result) => new JObject
    {
        ["result"] = result.ToString(),
        ["oc"] = Wallet.Coins,
        ["rm"] = Wallet.RealMoney,
    };
}
