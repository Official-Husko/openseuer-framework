using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Economy;

namespace DevBridge.Commands;

/// <summary>Invoices and the lawyer's services.</summary>
internal static class InvoiceCommands
{
    [BridgeCommand("invoices.list", "The player's open invoices.")]
    private static object List(JObject p) => new JArray(Invoices.All().Select(Invoice).ToArray());

    [BridgeCommand("invoices.send", "Mail the player an invoice.",
        Params = new[] { "debtor: who it is from", "currency: oc or rm", "amount: whole units", "days?: until due, default 7" })]
    private static object Send(JObject p)
    {
        var invoice = Invoices.Send(p.Str("debtor"), MoneyCommands.ParseCurrency(p.Str("currency")), (int)p.Float("amount"), p.Int("days", 7))
            ?? throw new InvalidOperationException("Invoices can only be sent while a world is loaded.");
        return Invoice(invoice);
    }

    [BridgeCommand("invoices.pay", "Pay an invoice from the wallet, or from a bank account.",
        Params = new[] { "id: invoice number", "bank?: pay from this bank's account instead" })]
    private static object Pay(JObject p)
    {
        int id = p.Int("id", -1);
        string bank = p.Str("bank", null);
        if (!string.IsNullOrEmpty(bank))
            return BankCommands.Result(BankAccounts.PayInvoice(bank, id));
        return new JObject { ["paid"] = Invoices.Pay(id), ["oc"] = Wallet.Coins, ["rm"] = Wallet.RealMoney };
    }

    [BridgeCommand("invoices.cancel", "Withdraw a plain invoice (not loan instalments or legal fees).", Params = new[] { "id: invoice number" })]
    private static object Cancel(JObject p) => new JObject { ["cancelled"] = Invoices.Cancel(p.Int("id", -1)) };

    [BridgeCommand("legal.list", "The lawyer's services with their fee now, and fees owed to him.")]
    private static object Legal(JObject p) => new JObject
    {
        ["actions"] = new JArray(LegalServices.Actions().Select(a => new JObject
        {
            ["name"] = a.Name,
            ["kind"] = a.Kind.ToString(),
            ["target"] = a.Target,
            ["invoice"] = a.InvoiceName,
            ["priceOc"] = a.PriceCoins,
            ["priceRm"] = a.PriceRealMoney,
            ["canTrigger"] = a.CanTrigger,
            ["queued"] = a.Queued,
            ["secondsLeft"] = a.SecondsLeft,
        }).ToArray()),
        ["debt"] = new JObject(LegalServices.Debt().Select(d => new JProperty(d.Key, new JObject { ["oc"] = d.Value.OC, ["rm"] = d.Value.RM }))),
    };

    [BridgeCommand("legal.queue", "Order one of the lawyer's services (the fee invoice is mailed).", Params = new[] { "name: from legal.list" })]
    private static object Queue(JObject p) => new JObject { ["queued"] = LegalServices.Queue(p.Str("name")) };

    private static JObject Invoice(InvoiceInfo i) => new JObject
    {
        ["id"] = i.Id,
        ["debtor"] = i.Debtor,
        ["amount"] = i.Amount,
        ["currency"] = i.Currency.ToString(),
        ["dueDay"] = i.DueDay,
        ["daysLeft"] = i.DaysLeft,
        ["expired"] = i.Expired,
        ["fake"] = i.Fake,
        ["paidAction"] = i.HasPaidAction,
    };
}
