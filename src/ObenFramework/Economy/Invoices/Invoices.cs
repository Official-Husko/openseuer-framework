// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;
using OS.Items;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// The invoices the player gets in the mail (rent, loan instalments, fees, fines from mods): list them,
/// send new ones, pay or cancel them, and hear about every one issued, paid, cancelled or expired. An
/// invoice arrives as a paper item and is paid at an invoice payment machine, from a bank account
/// (<see cref="BankAccounts.PayInvoice"/>) or with <see cref="Pay"/>. One that expires unpaid becomes an
/// administrative crime. Invoices are part of the save. Call on the main thread.
/// </summary>
public static class Invoices
{
    /// <summary>Longest due time <see cref="Send"/> accepts, in days.</summary>
    public const int MaxDays = 3650;

    /// <summary>An invoice was created (and its letter posted).</summary>
    public static readonly WorldEvent<InvoiceEvent> Issued = new WorldEvent<InvoiceEvent>(nameof(Issued));

    /// <summary>An invoice was paid, from the wallet or a bank account.</summary>
    public static readonly WorldEvent<InvoiceEvent> Paid = new WorldEvent<InvoiceEvent>(nameof(Paid));

    /// <summary>An invoice was withdrawn without being paid.</summary>
    public static readonly WorldEvent<InvoiceEvent> Cancelled = new WorldEvent<InvoiceEvent>(nameof(Cancelled));

    /// <summary>An invoice ran out unpaid; the game has just turned it into a crime.</summary>
    public static readonly WorldEvent<InvoiceEvent> Expired = new WorldEvent<InvoiceEvent>(nameof(Expired));

    /// <summary>True in a world: the invoice keeper exists in every gameplay scene.</summary>
    public static bool IsAvailable => InvoiceController.instance != null && TimeOfDayAzure.instance != null;

    /// <summary>Every open invoice.</summary>
    public static List<InvoiceInfo> All()
    {
        var list = new List<InvoiceInfo>();
        if (!IsAvailable)
            return list;
        foreach (var invoice in InvoiceController.instance.invoices)
        {
            if (invoice != null)
                list.Add(new InvoiceInfo(invoice));
        }
        return list;
    }

    /// <summary>One invoice by its number, or null when it doesn't exist (any more).</summary>
    public static InvoiceInfo Get(int id)
    {
        var invoice = IsAvailable ? InvoiceController.instance.GetInvoice(id) : null;
        return invoice != null ? new InvoiceInfo(invoice) : null;
    }

    /// <summary>True when the invoice is open, not expired and the wallet covers it.</summary>
    public static bool CanPay(int id) =>
        IsAvailable && Money.instance != null && InvoiceController.instance.CanPayInvoice(id);

    /// <summary>
    /// Sends the player an invoice by mail, as the game's own senders do. Expiring unpaid makes it a crime
    /// worth about the amount. Returns the new invoice, or null outside a world or when the game's invoice
    /// item is missing. Throws for an empty debtor, a non-positive amount or an out-of-range due time.
    /// </summary>
    /// <param name="debtor">Who it is from, as printed on it.</param>
    /// <param name="amount">Whole units of <paramref name="currency"/>.</param>
    /// <param name="days">Days until it is due (the game's own invoices use 7).</param>
    public static InvoiceInfo Send(string debtor, Currency currency, int amount, int days = 7)
    {
        if (string.IsNullOrWhiteSpace(debtor))
            throw new ArgumentException("A debtor is required.", nameof(debtor));
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "An invoice must be for 1 or more.");
        if (days < 0 || days > MaxDays)
            throw new ArgumentOutOfRangeException(nameof(days), "0 to " + MaxDays + " days.");
        if (!IsAvailable || MailController.instance == null || SaveGate.IsBusy)
            return null;

        var controller = InvoiceController.instance;
        int before = controller.invoices.Count;
        var money = currency == Currency.RM ? new Money.money(0, amount) : new Money.money(amount, 0f);
        controller.CreateInvoice(debtor, money, days);
        return controller.invoices.Count > before ? new InvoiceInfo(controller.invoices[controller.invoices.Count - 1]) : null;
    }

    /// <summary>Pays an invoice from the wallet, exactly like the payment machine. False when it doesn't exist or the wallet is short.</summary>
    public static bool Pay(int id)
    {
        if (!IsAvailable || Money.instance == null || SaveGate.IsBusy || !CanPay(id))
            return false;
        return InvoiceController.instance.PayInvoice(id);
    }

    /// <summary>
    /// Withdraws an invoice without payment. Refused (false) for invoices that do something when paid or when
    /// they expire, such as loan instalments and legal fees: cancelling those would skip the game's bookkeeping.
    /// </summary>
    public static bool Cancel(int id)
    {
        if (!IsAvailable || SaveGate.IsBusy)
            return false;
        var controller = InvoiceController.instance;
        var invoice = controller.GetInvoice(id);
        if (invoice == null || invoice.onPaid != null || invoice.onExpired != null)
            return false;
        controller.CancelInvoice(id);
        return controller.GetInvoice(id) == null;
    }

    /// <summary>
    /// Makes one of the game's scene invoice senders (<c>logic_invoice_sender</c>, by address, GUID or path)
    /// send its invoice now. Returns the new invoice, or null when there is no such sender or it is switched off.
    /// </summary>
    public static InvoiceInfo FireSender(string key)
    {
        if (!IsAvailable || MailController.instance == null || SaveGate.IsBusy)
            return null;
        var sender = World.Addressing.ObjectResolver.Find<logic_invoice_sender>(key);
        if (sender == null || sender.disabled)
            return null;
        var controller = InvoiceController.instance;
        int before = controller.invoices.Count;
        sender.SendInvoice();
        return controller.invoices.Count > before ? new InvoiceInfo(controller.invoices[controller.invoices.Count - 1]) : null;
    }

    internal static bool HasSubscribers =>
        Issued.HasSubscribers || Paid.HasSubscribers || Cancelled.HasSubscribers || Expired.HasSubscribers;

    internal static void RaiseIssued(ItemInvoiceData invoice) => Raise(Issued, invoice, InvoicePayer.Wallet);

    internal static void RaisePaid(ItemInvoiceData invoice, InvoicePayer payer) => Raise(Paid, invoice, payer);

    internal static void RaiseCancelled(ItemInvoiceData invoice) => Raise(Cancelled, invoice, InvoicePayer.Wallet);

    internal static void RaiseExpired(ItemInvoiceData invoice) => Raise(Expired, invoice, InvoicePayer.Wallet);

    private static void Raise(WorldEvent<InvoiceEvent> e, ItemInvoiceData invoice, InvoicePayer payer)
    {
        if (invoice != null && e.HasSubscribers)
            e.Raise(new InvoiceEvent(new InvoiceInfo(invoice), payer));
    }
}
