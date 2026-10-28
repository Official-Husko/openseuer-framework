using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Economy;

namespace DevBridge.Commands;

/// <summary>Player-run shops.</summary>
internal static class PlayerShopCommands
{
    [BridgeCommand("playershops.list", "The save's player-run shops with their till and shelves.")]
    private static object List(JObject p) => new JArray(PlayerShops.All().Select(Shop).ToArray());

    [BridgeCommand("playershops.create", "Start a player-run shop (closed, empty till).",
        Params = new[] { "id", "name?", "currency?: oc (default) or rm" })]
    private static object Create(JObject p)
    {
        string currency = p.Str("currency", "oc");
        if (!PlayerShops.Create(p.Str("id"), p.Str("name", null), MoneyCommands.ParseCurrency(currency)))
            throw new InvalidOperationException("Not created: the id is taken or no save is loaded.");
        return Shop(PlayerShops.Get(p.Str("id")));
    }

    [BridgeCommand("playershops.set", "Change a player-run shop. Give only what changes.",
        Params = new[]
        {
            "id", "open?: true or false", "markup?: factor on the sale value", "area?: building area GUID ('' clears)",
            "addContainer?: container id", "removeContainer?: container id", "item?: item id for price", "price?: fixed price, -1 clears",
        })]
    private static object Set(JObject p)
    {
        string id = p.Str("id");
        var shop = PlayerShops.Get(id) ?? throw new ArgumentException($"No player shop '{id}'.");
        if (p["open"] != null)
            Check(PlayerShops.SetOpen(id, p.Bool("open")), "open");
        if (p["markup"] != null)
            Check(PlayerShops.SetMarkup(id, p.Float("markup")), "markup");
        if (p["area"] != null)
            Check(PlayerShops.SetArea(id, p.Str("area")), "area");
        if (p["addContainer"] != null)
            Check(PlayerShops.AddContainer(id, p.Str("addContainer")), "addContainer");
        if (p["removeContainer"] != null)
            Check(PlayerShops.RemoveContainer(id, p.Str("removeContainer")), "removeContainer");
        if (p["item"] != null)
        {
            int item = p.Int("item", -1);
            float price = p.Float("price", -1f);
            Check(price < 0f ? PlayerShops.ClearPrice(id, item) : PlayerShops.SetPrice(id, item, price), "price");
        }
        return Shop(PlayerShops.Get(id) ?? shop);
    }

    [BridgeCommand("playershops.stock", "What is on a player-run shop's loaded shelves, with prices.", Params = new[] { "id" })]
    private static object Stock(JObject p) => new JArray(PlayerShops.Stock(p.Str("id")).Select(l => new JObject
    {
        ["container"] = l.Container,
        ["slot"] = l.Slot,
        ["item"] = l.Item.ID,
        ["title"] = l.Item.Title,
        ["amount"] = l.Amount,
        ["price"] = l.Price,
        ["stolen"] = l.Stolen,
    }).ToArray());

    [BridgeCommand("playershops.sell", "Sell goods from one shelf slot into the till, as a customer would buy them.",
        Params = new[] { "id", "container: from playershops.stock", "slot", "item: item id", "count?: default 1" })]
    private static object Sell(JObject p)
    {
        var result = PlayerShops.Sell(p.Str("id"), p.Str("container"), p.Int("slot", -1), p.Int("item", -1), p.Int("count", 1));
        var shop = PlayerShops.Get(p.Str("id"));
        return new JObject { ["result"] = result.ToString(), ["tillOc"] = shop?.TillCoins, ["tillRm"] = shop?.TillRealMoney };
    }

    [BridgeCommand("playershops.collect", "Move a player-run shop's till to the wallet.", Params = new[] { "id" })]
    private static object Collect(JObject p) => new JObject
    {
        ["collected"] = PlayerShops.CollectEarnings(p.Str("id")),
        ["oc"] = Wallet.Coins,
        ["rm"] = Wallet.RealMoney,
    };

    [BridgeCommand("playershops.delete", "Close a player-run shop for good.", Params = new[] { "id", "force?: also with money in the till" })]
    private static object Delete(JObject p) => new JObject { ["deleted"] = PlayerShops.Delete(p.Str("id"), p.Bool("force", false)) };

    private static JObject Shop(PlayerShopInfo s) => s == null ? null : new JObject
    {
        ["id"] = s.Id,
        ["name"] = s.Name,
        ["open"] = s.Open,
        ["currency"] = s.Currency.ToString(),
        ["markup"] = s.Markup,
        ["area"] = s.Area,
        ["containers"] = new JArray(s.Containers.Cast<object>().ToArray()),
        ["shelvesLoaded"] = s.ShelvesLoaded,
        ["tillOc"] = s.TillCoins,
        ["tillRm"] = s.TillRealMoney,
        ["fixedPrices"] = new JObject(s.FixedPrices.Select(kv => new JProperty(kv.Key.ToString(), kv.Value))),
    };

    private static void Check(bool ok, string what)
    {
        if (!ok)
            throw new ArgumentException($"'{what}' was refused (out of range, or no save loaded).");
    }
}
