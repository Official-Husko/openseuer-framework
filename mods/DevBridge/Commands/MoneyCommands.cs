using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Economy;

namespace DevBridge.Commands;

/// <summary>Read and change the player's money.</summary>
internal static class MoneyCommands
{
    [BridgeCommand("money.get", "The player's Open Sewer Coin and Real Money.")]
    private static object Get(JObject p) => Balance();

    [BridgeCommand("money.add", "Add money, or remove it with a negative amount (never below zero).",
        Params = new[] { "currency: oc or rm", "amount: Real Money counts tenths" })]
    private static object Add(JObject p)
    {
        var currency = ParseCurrency(p.Str("currency"));
        if (!Wallet.Add(currency, p.Float("amount")))
            throw new InvalidOperationException("Money can only be changed while a world is loaded.");
        return Balance();
    }

    private static JObject Balance() => new JObject { ["oc"] = Wallet.Coins, ["rm"] = Wallet.RealMoney };

    private static Money.Currency ParseCurrency(string text)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "oc": case "coin": case "coins": return Money.Currency.OC;
            case "rm": case "real": case "realmoney": return Money.Currency.RM;
            default: throw new ArgumentException("currency must be 'oc' or 'rm'.");
        }
    }
}
