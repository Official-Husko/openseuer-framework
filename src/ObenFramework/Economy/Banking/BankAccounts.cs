// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// The player's bank accounts, by bank name, from anywhere: no branch needs to be loaded. This is what
/// online banking, card payments and mail-order services build on: read balances, freeze and unfreeze,
/// charge and credit an account, pay invoices and debt from it, send cash by post. Every change is written
/// to the <see cref="BankLedger"/>. A frozen account refuses everything but crediting. Call on the main thread.
/// </summary>
public static partial class BankAccounts
{
    /// <summary>The game's id of the debit card task item the bank hands out.</summary>
    public const string DebitCardId = "debit_card";

    /// <summary>Every account the player has. Empty without a loaded save.</summary>
    public static List<BankAccountInfo> All()
    {
        var list = new List<BankAccountInfo>();
        var money = Money.instance;
        if (money == null || money.bankAccounts == null)
            return list;
        foreach (var data in money.bankAccounts)
        {
            if (data != null && !string.IsNullOrEmpty(data.Bankname))
                list.Add(new BankAccountInfo(data));
        }
        return list;
    }

    /// <summary>The player's account at <paramref name="bank"/> (the bank's name, such as <c>Stalburg Bank</c>), or null.</summary>
    public static BankAccountInfo Get(string bank)
    {
        var data = Find(bank);
        return data != null ? new BankAccountInfo(data) : null;
    }

    /// <summary>True when the player holds the bank's debit card (a task item).</summary>
    public static bool HasDebitCard
    {
        get
        {
            var tasks = TaskItemsManager.instance;
            return tasks != null && tasks.PlayerHasTaskItemId(DebitCardId);
        }
    }

    /// <summary>Freezes or unfreezes an account, the same flag the bank's fraud check and the lawyer use.</summary>
    public static BankResult SetFrozen(string bank, bool frozen)
    {
        var refusal = Check(bank, out var account, allowFrozen: true);
        if (refusal != BankResult.Ok)
            return refusal;
        account.Frozen = frozen;
        BankLedger.Reconcile(LedgerKind.Other);
        return BankResult.Ok;
    }

    /// <summary>
    /// Takes an amount out of the account for something bought (online shops, card terminals). Coins are
    /// whole numbers, Real Money is kept to two decimals. Refused when the account is frozen or short.
    /// </summary>
    public static BankResult Charge(string bank, Currency currency, float amount) =>
        Debit(bank, currency, amount, LedgerKind.Purchase);

    /// <summary>Puts an amount into the account (a refund, wages, a payout). Works on frozen accounts too, like the game's own credits.</summary>
    public static BankResult Credit(string bank, Currency currency, float amount)
    {
        var refusal = Check(bank, out var account, allowFrozen: true);
        if (refusal != BankResult.Ok)
            return refusal;
        bool rm = currency == Currency.RM;
        if (!AccountMath.TryNormalize(rm, amount, out float value) || (!rm && !AccountMath.CoinsFit(account.OC, value)))
            return BankResult.Refused;

        if (rm)
            account.RM = AccountMath.CreditRm(account.RM, value);
        else
            account.OC += (int)value;
        BankLedger.Reconcile(LedgerKind.Refund);
        return BankResult.Ok;
    }

    internal static BankResult Debit(string bank, Currency currency, float amount, LedgerKind kind)
    {
        var refusal = Check(bank, out var account, allowFrozen: false);
        if (refusal != BankResult.Ok)
            return refusal;
        bool rm = currency == Currency.RM;
        if (!AccountMath.TryNormalize(rm, amount, out float value))
            return BankResult.Refused;
        if (!AccountMath.Covers(rm, account.OC, account.RM, value))
            return BankResult.Insufficient;

        TakeFrom(account, rm, value);
        BankLedger.Reconcile(kind);
        return BankResult.Ok;
    }

    internal static Money.BankAccountData Find(string bank)
    {
        var money = Money.instance;
        return money != null && !string.IsNullOrEmpty(bank) ? money.FindBankAccount(bank) : null;
    }

    private static void TakeFrom(Money.BankAccountData account, bool rm, float value)
    {
        if (rm)
            account.RM = AccountMath.DebitRm(account.RM, value);
        else
            account.OC -= (int)value;
    }

    private static void Restore(Money.BankAccountData account, int coins, float rm)
    {
        account.OC = coins;
        account.RM = rm;
    }

    private static BankResult Check(string bank, out Money.BankAccountData account, bool allowFrozen)
    {
        account = null;
        if (Money.instance == null || SaveGate.IsBusy)
            return BankResult.NotReady;
        account = Find(bank);
        if (account == null)
            return BankResult.NoAccount;
        return account.Frozen && !allowFrozen ? BankResult.Frozen : BankResult.Ok;
    }

    private static BankResult Guard(string what, Func<BankResult> call)
    {
        try
        {
            return call();
        }
        catch (Exception e)
        {
            WorldLog.Failed("BankAccounts." + what, e);
            return BankResult.Failed;
        }
    }
}
