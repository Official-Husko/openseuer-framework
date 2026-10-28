using OS.Items;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>How an invoice was paid.</summary>
public enum InvoicePayer
{
    /// <summary>From the wallet, at a payment machine or by the game's own call.</summary>
    Wallet,

    /// <summary>From a bank account through <see cref="BankAccounts.PayInvoice"/>.</summary>
    BankAccount,
}

/// <summary>A snapshot of one invoice the player has to pay (the game's <c>ItemInvoiceData</c>).</summary>
public sealed class InvoiceInfo
{
    internal InvoiceInfo(ItemInvoiceData data)
    {
        Id = data.invoiceID;
        Debtor = data.debtor ?? "";
        Amount = data.amount;
        Currency = data.currency;
        DueDay = data.dueDate;
        var clock = TimeOfDayAzure.instance;
        DaysLeft = clock != null ? data.DaysRemaining() : 0;
        Expired = clock != null && data.IsExpired();
        Fake = data.nocrime;
        HasPaidAction = data.onPaid != null;
        HasExpiredAction = data.onExpired != null;
    }

    /// <summary>The game's invoice number.</summary>
    public int Id { get; }

    /// <summary>Who sent it, as printed on it (<c>Stalburg Bank: Debt Downpayment</c>).</summary>
    public string Debtor { get; }

    /// <summary>The amount, a whole number in <see cref="Currency"/>.</summary>
    public int Amount { get; }

    public Currency Currency { get; }

    /// <summary>The game day it is due on.</summary>
    public int DueDay { get; }

    /// <summary>Days until it is due (0 is today, negative is overdue).</summary>
    public int DaysLeft { get; }

    /// <summary>Overdue. An expired real invoice turns into an administrative crime at the next day change.</summary>
    public bool Expired { get; }

    /// <summary>A forged invoice (the scam quest): never a crime to ignore.</summary>
    public bool Fake { get; }

    /// <summary>True when paying it does something besides taking money (a loan instalment, a legal fee).</summary>
    public bool HasPaidAction { get; }

    /// <summary>True when letting it expire does something besides the crime (legal fees turn into debt).</summary>
    public bool HasExpiredAction { get; }

    public override string ToString() => $"#{Id} {Debtor}: {Amount} {Currency}";
}

/// <summary>An invoice event, raised by <see cref="Invoices"/>.</summary>
public readonly struct InvoiceEvent
{
    internal InvoiceEvent(InvoiceInfo invoice, InvoicePayer payer)
    {
        Invoice = invoice;
        Payer = payer;
    }

    public InvoiceInfo Invoice { get; }

    /// <summary>For <see cref="Invoices.Paid"/>: where the money came from.</summary>
    public InvoicePayer Payer { get; }
}
