// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.World;
using UnityEngine;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

public static partial class Banks
{
    /// <summary>
    /// The most the player could borrow at this branch now, in its <see cref="BankInfo.LoanCurrency"/> (the
    /// game folds the other currency in). Null without an account: the game's own calculation would open one.
    /// </summary>
    public static int? MaxLoan(string bankKey)
    {
        var bank = Resolve(bankKey);
        if (bank == null || Money.instance == null || Money.instance.FindBankAccount(bank.BankName) == null)
            return null;
        try
        {
            var max = bank.CalculateMaximumLoan();
            return bank.LoanCurrency == Currency.RM ? (int)max.RM : max.OC;
        }
        catch (Exception e)
        {
            // The tenement financing part needs the tenement controller of the start area.
            WorldLog.Failed("Banks.MaxLoan", e);
            return null;
        }
    }

    /// <summary>
    /// Borrows a whole amount in the branch's loan currency, as the clerk does: the debt grows by it plus the
    /// bank's terms, and the money goes to the wallet (or into the account at banks that pay loans that way).
    /// </summary>
    public static BankResult TakeLoan(string bankKey, int amount)
    {
        var bank = Resolve(bankKey);
        var refusal = Check(bank, needAccount: false);
        if (refusal != BankResult.Ok)
            return refusal;
        if (amount <= 0)
            return BankResult.Refused;

        var account = Money.instance.FindBankAccount(bank.BankName);
        if (account == null && !bank.TakeLoanDirectly)
            return BankResult.NoAccount;
        if (account != null && account.Frozen)
            return BankResult.Frozen;

        // A bank that lends without an account opens one for the loan, like the clerk's dialogue does.
        if (account == null)
        {
            var opened = OpenAccount(bankKey);
            if (opened != BankResult.Ok)
                return opened;
        }
        int? max = MaxLoan(bankKey);
        if (max == null || amount > max.Value)
            return BankResult.Refused;
        bool rm = bank.LoanCurrency == Currency.RM;
        if (bank.TakeLoanDirectly && !WalletFits(rm ? 0 : amount, rm ? amount : 0f))
            return BankResult.Refused;

        return Guard("TakeLoan", () =>
        {
            if (rm)
                bank.TakeRMLoan(amount);
            else
                bank.TakeOCLoan(amount);
            return BankResult.Ok;
        });
    }

    /// <summary>
    /// Pays debt back from the wallet in the branch's loan currency, as at the desk. A Real Money debt with
    /// hundredths left over after the whole part is cleared with the next tenth up, so it can be paid off fully.
    /// </summary>
    public static BankResult RepayLoan(string bankKey, int amount)
    {
        var bank = Resolve(bankKey);
        var refusal = Check(bank, needAccount: true);
        if (refusal != BankResult.Ok)
            return refusal;
        if (amount <= 0)
            return BankResult.Refused;

        var account = bank.GetPlayerAccount();
        bool rm = bank.LoanCurrency == Currency.RM;
        float owed = rm ? account.CurrentDebt.RM : account.CurrentDebt.OC;
        if (owed <= 0f)
            return BankResult.Refused;
        float wallet = rm ? Wallet.RealMoney : Wallet.Coins;
        int whole = Mathf.Min(amount, (int)owed);
        if (whole > (int)wallet)
            return BankResult.Insufficient;

        return Guard("RepayLoan", () =>
        {
            if (whole > 0)
                bank.PayLoan(whole);
            if (rm && amount > whole)
                PayRemainder(account);
            return BankResult.Ok;
        });
    }

    // PayLoan only takes whole Real Money, which leaves the hundredths interest adds.
    private static void PayRemainder(Money.BankAccountData account)
    {
        float rest = account.CurrentDebt.RM;
        if (rest <= 0f || rest >= 1f)
            return;
        float pay = AccountMath.WalletTenths(rest);
        if (!Wallet.CanAfford(Currency.RM, pay))
            return;
        Money.instance.RemoveMoney(pay, Currency.RM);
        account.CurrentDebt.RM = 0f;
        BankLedger.Reconcile(LedgerKind.Repayment);
    }
}
