using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Economy;

namespace DevBridge.Commands;

/// <summary>Bank branches, the player's accounts and their history.</summary>
internal static class BankCommands
{
    [BridgeCommand("bank.branches", "The bank branches of the loaded scene with services, supply, rates and the player's account there.")]
    private static object Branches(JObject p) => new JArray(Banks.All().Select(Branch).ToArray());

    [BridgeCommand("bank.accounts", "The player's bank accounts (any scene) and whether they hold the debit card.")]
    private static object Accounts(JObject p) => new JObject
    {
        ["debitCard"] = BankAccounts.HasDebitCard,
        ["accounts"] = new JArray(BankAccounts.All().Select(Account).ToArray()),
    };

    [BridgeCommand("bank.ledger", "An account's history, newest first.",
        Params = new[] { "bank: the bank's name", "max?: default 20", "before?: only entries with a smaller id (paging)" })]
    private static object Ledger(JObject p) => new JObject
    {
        ["firstDay"] = BankLedger.FirstDay,
        ["entries"] = new JArray(BankLedger.Entries(p.Str("bank"), p.Int("max", 20), p.Int("before", 0)).Select(e => new JObject
        {
            ["id"] = e.Id,
            ["day"] = e.Day,
            ["kind"] = e.Kind.ToString(),
            ["oc"] = e.Coins,
            ["rm"] = e.RealMoney,
            ["debtOc"] = e.DebtCoins,
            ["debtRm"] = e.DebtRealMoney,
            ["ocAfter"] = e.CoinsAfter,
            ["rmAfter"] = e.RealMoneyAfter,
        }).ToArray()),
    };

    [BridgeCommand("bank.quote", "What exchanging an amount at a branch would take and pay right now. Changes nothing.",
        Params = new[] { "bank: name, GUID or address", "amount", "to: rm (coins into Real Money) or oc" })]
    private static object Quote(JObject p)
    {
        var quote = Banks.Quote(p.Str("bank"), p.Float("amount"), ToRealMoney(p))
            ?? throw new ArgumentException("No such bank branch in this scene, or no money loaded.");
        return new JObject { ["taken"] = quote.Taken, ["credit"] = quote.Credit, ["rate"] = quote.Rate, ["whole"] = quote.Whole };
    }

    [BridgeCommand("bank.exchange", "Exchange a whole amount at a branch's exchange desk.",
        Params = new[] { "bank: name, GUID or address", "amount: whole units", "to: rm (coins into Real Money) or oc" })]
    private static object Exchange(JObject p) =>
        Result(Banks.Exchange(p.Str("bank"), (int)p.Float("amount"), ToRealMoney(p)));

    [BridgeCommand("bank.open", "Open the player's account at a branch's bank.", Params = new[] { "bank" })]
    private static object Open(JObject p) => Result(Banks.OpenAccount(p.Str("bank")));

    [BridgeCommand("bank.deposit", "Deposit from the wallet at a branch.", Params = new[] { "bank", "oc?: coins", "rm?: whole Real Money" })]
    private static object Deposit(JObject p) => Result(Banks.Deposit(p.Str("bank"), p.Int("oc", 0), p.Float("rm", 0f)));

    [BridgeCommand("bank.withdraw", "Withdraw to the wallet at a branch.", Params = new[] { "bank", "oc?: coins", "rm?: Real Money" })]
    private static object Withdraw(JObject p) => Result(Banks.Withdraw(p.Str("bank"), p.Int("oc", 0), p.Float("rm", 0f)));

    [BridgeCommand("bank.loan", "Borrow at a branch in its loan currency, or show the most it would lend without amount.",
        Params = new[] { "bank", "amount?: whole units" })]
    private static object Loan(JObject p)
    {
        int amount = p.Int("amount", 0);
        if (amount <= 0)
            return new JObject { ["maxLoan"] = Banks.MaxLoan(p.Str("bank")) };
        return Result(Banks.TakeLoan(p.Str("bank"), amount));
    }

    [BridgeCommand("bank.repay", "Pay loan debt back from the wallet at a branch.", Params = new[] { "bank", "amount: whole units" })]
    private static object Repay(JObject p) => Result(Banks.RepayLoan(p.Str("bank"), (int)p.Float("amount")));

    [BridgeCommand("bank.charge", "Take an amount out of an account (online purchase) or, with credit=true, put it in.",
        Params = new[] { "bank: the bank's name", "currency: oc or rm", "amount", "credit?: true to pay in" })]
    private static object Charge(JObject p)
    {
        var currency = MoneyCommands.ParseCurrency(p.Str("currency"));
        float amount = p.Float("amount");
        return Result(p.Bool("credit", false)
            ? BankAccounts.Credit(p.Str("bank"), currency, amount)
            : BankAccounts.Charge(p.Str("bank"), currency, amount));
    }

    [BridgeCommand("bank.transfer", "Move money between the wallet and an account, with no branch involved.",
        Params = new[] { "bank: the bank's name", "currency: oc or rm", "amount", "to: account or wallet" })]
    private static object Transfer(JObject p)
    {
        string bank = p.Str("bank");
        var currency = MoneyCommands.ParseCurrency(p.Str("currency"));
        float amount = p.Float("amount");
        switch (p.Str("to").Trim().ToLowerInvariant())
        {
            case "account": return Result(BankAccounts.DepositFromWallet(bank, currency, amount));
            case "wallet": return Result(BankAccounts.WithdrawToWallet(bank, currency, amount));
            default: throw new ArgumentException("to must be 'account' or 'wallet'.");
        }
    }

    [BridgeCommand("bank.freeze", "Freeze or unfreeze an account.", Params = new[] { "bank: the bank's name", "frozen: true or false" })]
    private static object Freeze(JObject p) => Result(BankAccounts.SetFrozen(p.Str("bank"), p.Bool("frozen")));

    [BridgeCommand("bank.post", "Take cash out of an account and post it to the mailbox.",
        Params = new[] { "bank: the bank's name", "currency: oc or rm", "amount", "fee?: kept by the bank", "delay?: game minutes" })]
    private static object Post(JObject p) => Result(BankAccounts.WithdrawByPost(p.Str("bank"),
        MoneyCommands.ParseCurrency(p.Str("currency")), p.Float("amount"), p.Float("fee", 0f), p.Int("delay", 0)));

    internal static JObject Result(BankResult result) => new JObject
    {
        ["result"] = result.ToString(),
        ["oc"] = Wallet.Coins,
        ["rm"] = Wallet.RealMoney,
    };

    internal static JObject Account(BankAccountInfo a) => a == null ? null : new JObject
    {
        ["bank"] = a.Bank,
        ["oc"] = a.Coins,
        ["rm"] = a.RealMoney,
        ["frozen"] = a.Frozen,
        ["interest"] = a.Interest,
        ["debtOc"] = a.DebtCoins,
        ["debtRm"] = a.DebtRealMoney,
        ["debtInterest"] = a.DebtInterest,
    };

    private static JObject Branch(BankInfo b) => new JObject
    {
        ["id"] = b.Id,
        ["name"] = b.Name,
        ["scene"] = b.Scene,
        ["services"] = b.Services.ToString(),
        ["currencies"] = b.Currencies.ToString(),
        ["supplyOc"] = b.CoinSupply,
        ["supplyRm"] = b.RealMoneySupply,
        ["ocToRm"] = b.CoinToRealMoney,
        ["rmToOc"] = b.RealMoneyToCoins,
        ["corruption"] = b.Corruption,
        ["loanCurrency"] = b.LoanCurrency.ToString(),
        ["paymentMonths"] = b.PaymentMonths,
        ["account"] = Account(b.Account),
    };

    private static bool ToRealMoney(JObject p)
    {
        switch (p.Str("to").Trim().ToLowerInvariant())
        {
            case "rm": return true;
            case "oc": return false;
            default: throw new ArgumentException("to must be 'rm' or 'oc'.");
        }
    }
}
