using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Economy;

namespace DevBridge.Commands;

/// <summary>Traders, opening hours, checkouts, furniture shop money and fuel stations.</summary>
internal static class TraderCommands
{
    [BridgeCommand("traders.list", "The NPC traders of the loaded scene with money, terms and mods' price factors.")]
    private static object TraderList(JObject p) => new JArray(Traders.All().Select(t => new JObject
    {
        ["id"] = t.Shop.Id,
        ["name"] = t.Shop.Name,
        ["open"] = t.Open,
        ["byClock"] = t.OpensByClock,
        ["money"] = t.Money,
        ["sellsFor"] = t.SellsFor.ToString(),
        ["buysFor"] = t.BuysFor.ToString(),
        ["markup"] = t.Markup,
        ["payout"] = t.Payout,
        ["buysStolen"] = t.BuysStolen,
        ["special"] = t.Special,
        ["buyFactor"] = t.BuyFactor,
        ["sellFactor"] = t.SellFactor,
    }).ToArray());

    [BridgeCommand("traders.money", "Set a trader's money, or refill it the game's way.",
        Params = new[] { "shop: address, GUID or name", "amount?: new money", "restock?: true to refill instead" })]
    private static object TraderMoney(JObject p)
    {
        string shop = p.Str("shop");
        bool ok = p.Bool("restock", false)
            ? Traders.RestockMoney(shop)
            : Traders.SetMoney(shop, p.Float("amount"));
        var info = Traders.Get(shop);
        return new JObject { ["ok"] = ok, ["money"] = info?.Money };
    }

    [BridgeCommand("shops.hours", "A trader's opening hours and whether a shop is open now.", Params = new[] { "shop: address, GUID or name" })]
    private static object Hours(JObject p)
    {
        string shop = p.Str("shop");
        var days = ShopHours.Hours(shop);
        return new JObject
        {
            ["open"] = ShopHours.IsOpen(shop),
            ["scheduledOpen"] = ShopHours.IsScheduledOpen(shop),
            ["hours"] = days == null ? null : new JArray(days.Select(d => new JObject
            {
                ["day"] = d.Day.ToString(),
                ["opens"] = d.Opens,
                ["closes"] = d.Closes,
            }).ToArray()),
        };
    }

    [BridgeCommand("markets.list", "The supermarket checkouts of the loaded scene.")]
    private static object Markets(JObject p) => new JArray(MarketShops.All().Select(m => new JObject
    {
        ["id"] = m.Shop.Id,
        ["name"] = m.Name,
        ["locked"] = m.Locked,
        ["canLock"] = m.CanLock,
        ["state"] = m.State.ToString(),
        ["total"] = m.Total,
        ["currency"] = m.Currency.ToString(),
        ["priceFactor"] = m.PriceFactor,
    }).ToArray());

    [BridgeCommand("markets.lock", "Lock or unlock a checkout.", Params = new[] { "shop: address, GUID or name", "locked: true or false" })]
    private static object Lock(JObject p) => new JObject { ["ok"] = MarketShops.SetLocked(p.Str("shop"), p.Bool("locked")) };

    [BridgeCommand("furnitureshops.list", "Furniture and train shops with the money they can pay the player.")]
    private static object FurnitureShopList(JObject p) => new JArray(FurnitureShops.All().Select(f => new JObject
    {
        ["id"] = f.Shop.Id,
        ["name"] = f.Shop.Name,
        ["kind"] = f.Shop.Kind.ToString(),
        ["oc"] = f.Coins,
        ["rm"] = f.RealMoney,
        ["buyPercent"] = f.BuyPercent,
        ["sellPercent"] = f.SellPercent,
        ["restocksDaily"] = f.RestocksDaily,
    }).ToArray());

    [BridgeCommand("fuel.stations", "The fuel stations of the loaded scene with price, range and what is owed.")]
    private static object Fuel(JObject p) => new JArray(FuelStations.All().Select(f => new JObject
    {
        ["id"] = f.Id,
        ["name"] = f.Name,
        ["scene"] = f.Scene,
        ["price"] = f.Price,
        ["min"] = f.MinPrice,
        ["max"] = f.MaxPrice,
        ["currency"] = f.Currency.ToString(),
        ["pumps"] = f.Pumps,
        ["litres"] = f.Litres,
        ["toPayOc"] = f.ToPayCoins,
    }).ToArray());

    [BridgeCommand("fuel.price", "Set a fuel station's price per litre until the game picks its next one.",
        Params = new[] { "station: address, GUID or receipt name", "price" })]
    private static object FuelPrice(JObject p)
    {
        string station = p.Str("station");
        if (!FuelStations.SetPrice(station, (int)p.Float("price")))
            throw new ArgumentException($"No fuel station '{station}' here, or the price was refused.");
        return new JObject { ["price"] = FuelStations.Find(station)?.Price };
    }
}
