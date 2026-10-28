using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Economy;
using ObenFramework.Gambling;

namespace DevBridge.Commands;

/// <summary>The economy and gambling events, recorded for checking them in the running game, and trader price factors.</summary>
internal static class EconomyEventCommands
{
    private static readonly EventRecorder MoneyRecorder = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        Wallet.Changed.Subscribe(owner, e => recorder.Add("WalletChanged", new JObject { ["currency"] = e.Currency.ToString(), ["before"] = e.Before, ["after"] = e.After }));
        BankLedger.Added.Subscribe(owner, e => recorder.Add("Ledger", new JObject
        {
            ["bank"] = e.Bank, ["kind"] = e.Kind.ToString(), ["oc"] = e.Coins, ["rm"] = e.RealMoney, ["debtOc"] = e.DebtCoins, ["debtRm"] = e.DebtRealMoney,
        }));
        Invoices.Issued.Subscribe(owner, e => recorder.Add("InvoiceIssued", Invoice(e)));
        Invoices.Paid.Subscribe(owner, e => recorder.Add("InvoicePaid", Invoice(e)));
        Invoices.Cancelled.Subscribe(owner, e => recorder.Add("InvoiceCancelled", Invoice(e)));
        Invoices.Expired.Subscribe(owner, e => recorder.Add("InvoiceExpired", Invoice(e)));
        LegalServices.Queued.Subscribe(owner, e => recorder.Add("LegalQueued", new JObject { ["name"] = e.Name, ["kind"] = e.Kind.ToString() }));
        LegalServices.Completed.Subscribe(owner, e => recorder.Add("LegalCompleted", new JObject { ["name"] = e.Name, ["kind"] = e.Kind.ToString() }));
        MarketShops.Paid.Subscribe(owner, e => recorder.Add("CheckoutPaid", new JObject { ["shop"] = e.Shop.Name, ["total"] = e.Total, ["currency"] = e.Currency.ToString() }));
        ShopHours.Opened.Subscribe(owner, s => recorder.Add("ShopOpened", new JObject { ["shop"] = s.Name }));
        ShopHours.Closed.Subscribe(owner, s => recorder.Add("ShopClosed", new JObject { ["shop"] = s.Name }));
        FuelStations.PriceChanged.Subscribe(owner, s => recorder.Add("FuelPrice", new JObject { ["station"] = s.Name, ["price"] = s.Price }));
        FuelStations.FuelBought.Subscribe(owner, f => recorder.Add("FuelBought", new JObject { ["station"] = f.Station.Name, ["litres"] = f.Litres, ["cost"] = f.Cost }));
        PlayerShops.Sold.Subscribe(owner, s => recorder.Add("PlayerShopSold", new JObject
        {
            ["shop"] = s.Shop, ["item"] = s.Item.ID, ["amount"] = s.Amount, ["total"] = s.Total, ["currency"] = s.Currency.ToString(), ["stolen"] = s.Stolen,
        }));
    });

    private static readonly EventRecorder GamblingRecorder = new EventRecorder(recorder =>
    {
        var owner = DevBridgeMod.Instance;
        Casino.Played.Subscribe(owner, e => recorder.Add("Played", Machine(e)));
        Casino.Won.Subscribe(owner, e => recorder.Add("Won", Machine(e)));
        Casino.Doubled.Subscribe(owner, e => recorder.Add("Doubled", Machine(e)));
        Casino.CashedOut.Subscribe(owner, e => recorder.Add("CashedOut", Machine(e)));
        Casino.Scratched.Subscribe(owner, s => recorder.Add("Scratched", new JObject { ["card"] = s.Card.ID, ["prize"] = s.Prize, ["rm"] = s.RealMoney }));
        CardGames.Started.Subscribe(owner, t => recorder.Add("CardGameStarted", new JObject { ["kind"] = t.Kind.ToString(), ["stake"] = t.Stake }));
        CardGames.Ended.Subscribe(owner, r => recorder.Add("CardGameEnded", new JObject
        {
            ["kind"] = r.Kind.ToString(), ["placement"] = r.Placement, ["stake"] = r.Stake, ["payout"] = r.Payout,
        }));
        Contests.Started.Subscribe(owner, c => recorder.Add("ContestStarted", new JObject { ["kind"] = c.Kind.ToString(), ["name"] = c.Name, ["opponent"] = c.Opponent }));
        Contests.Finished.Subscribe(owner, r => recorder.Add("ContestFinished", new JObject
        {
            ["kind"] = r.Contest.Kind.ToString(), ["won"] = r.PlayerWon, ["winner"] = r.Winner, ["winOc"] = r.WinCoins, ["winRm"] = r.WinRealMoney,
        }));
        Contests.BetPlaced.Subscribe(owner, b => recorder.Add("RatBet", new JObject { ["amount"] = b.Amount, ["currency"] = b.Currency.ToString(), ["fighter"] = b.Fighter }));
        Contests.WinningsCollected.Subscribe(owner, b => recorder.Add("RatWinnings", new JObject { ["amount"] = b.Amount, ["currency"] = b.Currency.ToString() }));
    });

    [BridgeCommand("economy.events", "Money, bank ledger, invoice, legal, checkout, opening hours, fuel and player shop events since recording started.",
        Params = new[] { "clear?: forget what was returned, default true" })]
    private static object MoneyEvents(JObject p) => MoneyRecorder.Read(p.Bool("clear", true));

    [BridgeCommand("gambling.events", "Casino, card game and contest events since recording started.",
        Params = new[] { "clear?: forget what was returned, default true" })]
    private static object GamblingEvents(JObject p) => GamblingRecorder.Read(p.Bool("clear", true));

    [BridgeCommand("traders.factor", "Put the bridge's own price factors on a trader (or all of them); buy=1 and sell=1 removes them.",
        Params = new[] { "shop?: address, GUID or name; leave out for every trader", "buy: factor on what the player pays", "sell: factor on what the player is paid" })]
    private static object Factor(JObject p)
    {
        string shop = p.Str("shop", null);
        float buy = p.Float("buy");
        float sell = p.Float("sell");
        if (buy == 1f && sell == 1f)
            Traders.ClearPriceFactors(DevBridgeMod.Instance, shop);
        else
            Traders.SetPriceFactors(DevBridgeMod.Instance, shop, buy, sell);
        var info = shop != null ? Traders.Get(shop) : null;
        return new JObject { ["buyFactor"] = info?.BuyFactor, ["sellFactor"] = info?.SellFactor };
    }

    private static JObject Invoice(InvoiceEvent e) => new JObject
    {
        ["id"] = e.Invoice.Id, ["debtor"] = e.Invoice.Debtor, ["amount"] = e.Invoice.Amount, ["currency"] = e.Invoice.Currency.ToString(), ["payer"] = e.Payer.ToString(),
    };

    private static JObject Machine(CasinoEvent e) => new JObject
    {
        ["kind"] = e.Machine.Kind.ToString(), ["machine"] = e.Machine.Id, ["amount"] = e.Amount, ["before"] = e.Before,
    };
}
