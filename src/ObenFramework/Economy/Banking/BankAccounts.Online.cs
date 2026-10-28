// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

public static partial class BankAccounts
{
    /// <summary>
    /// Pays an invoice from the account instead of the wallet. Does what the game's own payment does: runs the
    /// invoice's paid action (a loan instalment lowers the debt, a legal fee clears), removes it, and raises
    /// <see cref="Invoices.Paid"/>. Refused for an unknown or expired invoice.
    /// </summary>
    public static BankResult PayInvoice(string bank, int invoiceId)
    {
        var controller = InvoiceController.instance;
        if (controller == null || TimeOfDayAzure.instance == null)
            return BankResult.NotReady;
        var invoice = controller.GetInvoice(invoiceId);
        if (invoice == null || invoice.IsExpired() || invoice.amount <= 0)
            return BankResult.Refused;

        var refusal = Check(bank, out var account, allowFrozen: false);
        if (refusal != BankResult.Ok)
            return refusal;
        bool rm = invoice.currency == Currency.RM;
        if (!AccountMath.Covers(rm, account.OC, account.RM, invoice.amount))
            return BankResult.Insufficient;

        int coins = account.OC;
        float realMoney = account.RM;
        TakeFrom(account, rm, invoice.amount);
        try
        {
            invoice.onPaid?.Invoke(invoice);
            controller.invoices.Remove(invoice);
        }
        catch (Exception e)
        {
            if (controller.invoices.Contains(invoice))
                Restore(account, coins, realMoney);
            BankLedger.Reconcile(LedgerKind.Invoice);
            World.WorldLog.Failed("BankAccounts.PayInvoice", e);
            return BankResult.Failed;
        }
        BankLedger.Reconcile(LedgerKind.Invoice);

        if (invoice.nocrime && Achievements.instance != null)
            Guard("PayInvoice achievement", () => { Achievements.instance.TriggerAchievement("PAY_FAKE_INVOICE", 1); return BankResult.Ok; });
        Invoices.RaisePaid(invoice, InvoicePayer.BankAccount);
        return BankResult.Ok;
    }

    /// <summary>Pays loan debt in one currency from the account's balance. At most what is owed is taken.</summary>
    public static BankResult RepayDebt(string bank, Currency currency, float amount)
    {
        var refusal = Check(bank, out var account, allowFrozen: false);
        if (refusal != BankResult.Ok)
            return refusal;
        bool rm = currency == Currency.RM;
        if (!AccountMath.TryNormalize(rm, amount, out float value))
            return BankResult.Refused;

        float owed = rm ? account.CurrentDebt.RM : account.CurrentDebt.OC;
        float pay = Math.Min(value, owed);
        if (pay <= 0f)
            return BankResult.Refused;
        if (!AccountMath.Covers(rm, account.OC, account.RM, pay))
            return BankResult.Insufficient;

        TakeFrom(account, rm, pay);
        if (rm)
            account.CurrentDebt.RM = AccountMath.DebitRm(account.CurrentDebt.RM, pay);
        else
            account.CurrentDebt.OC -= (int)pay;
        BankLedger.Reconcile(LedgerKind.Repayment);
        return BankResult.Ok;
    }

    /// <summary>
    /// Takes cash out of the account and posts it to the player's mailbox: <paramref name="amount"/> leaves the
    /// account, <paramref name="fee"/> of it is kept by the bank, the rest arrives by letter (Real Money in
    /// tenths, as the wallet keeps it). Nothing is taken if the letter can't be sent.
    /// </summary>
    /// <param name="delayMinutes">Game minutes until the letter arrives; 0 delivers at once.</param>
    public static BankResult WithdrawByPost(string bank, Currency currency, float amount, float fee = 0f, int delayMinutes = 0)
    {
        if (float.IsNaN(fee) || fee < 0f || delayMinutes < 0)
            return BankResult.Refused;
        var refusal = Check(bank, out var account, allowFrozen: false);
        if (refusal != BankResult.Ok)
            return refusal;
        bool rm = currency == Currency.RM;
        if (!AccountMath.TryNormalize(rm, amount, out float value))
            return BankResult.Refused;
        float sent = rm ? Money.RoundRM(value - fee) : (float)Math.Floor(value - fee);
        if (sent <= 0f)
            return BankResult.TooSmall;
        if (!AccountMath.Covers(rm, account.OC, account.RM, value))
            return BankResult.Insufficient;
        if (!PlayerMail.IsAvailable)
            return BankResult.NotReady;

        int coins = account.OC;
        float realMoney = account.RM;
        TakeFrom(account, rm, value);
        bool posted;
        try
        {
            posted = PlayerMail.Send("Cash from " + bank, null, null, MailKind.Letter, delayMinutes,
                rm ? 0 : (int)sent, rm ? sent : 0f);
        }
        catch (Exception e)
        {
            World.WorldLog.Failed("BankAccounts.WithdrawByPost", e);
            posted = false;
        }
        if (!posted)
        {
            Restore(account, coins, realMoney);
            return BankResult.Failed;
        }
        BankLedger.Reconcile(LedgerKind.CashByPost);
        return BankResult.Ok;
    }
}
