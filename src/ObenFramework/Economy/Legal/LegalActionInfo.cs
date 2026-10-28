using LegalAction = LegalServicesController.LegalAction;

namespace ObenFramework.Economy;

/// <summary>What a lawyer's service does.</summary>
public enum LegalActionKind
{
    /// <summary>Nothing the framework knows of; the game's own dialogue gives it meaning.</summary>
    None,

    /// <summary>Wipes the player's crimes (all of them, or against one character, see <see cref="LegalActionInfo.Target"/>).</summary>
    ClearCrime,

    /// <summary>Unfreezes the bank account named by <see cref="LegalActionInfo.Target"/>.</summary>
    UnfreezeAccount,
}

/// <summary>A snapshot of one service of the lawyer (the game's <c>LegalServicesController.LegalAction</c>).</summary>
public sealed class LegalActionInfo
{
    internal LegalActionInfo(LegalAction action, LegalAction queued)
    {
        Name = action.name ?? "";
        InvoiceName = action.invoiceName ?? "";
        Kind = KindOf(action.type);
        Target = action.actionData ?? "";
        var price = Price(action);
        PriceCoins = price.OC;
        PriceRealMoney = price.RM;
        PaymentDays = action.paymentTime;
        CanTrigger = Safe(action);
        Queued = queued != null;
        SecondsLeft = queued != null && queued.timer != null ? queued.timer.remainingInSeconds : -1;
    }

    /// <summary>The service's key (<c>ClearCrime_All</c>, <c>UnfreezeAccount_Stalburg Bank</c>); pass it to <see cref="LegalServices.Queue"/>.</summary>
    public string Name { get; }

    /// <summary>Who the fee invoice is from, which also keys unpaid fees in <see cref="LegalServices.Debt"/>.</summary>
    public string InvoiceName { get; }

    public LegalActionKind Kind { get; }

    /// <summary>The crime victim's character id for a targeted <see cref="LegalActionKind.ClearCrime"/>, the bank name for <see cref="LegalActionKind.UnfreezeAccount"/>, or empty.</summary>
    public string Target { get; }

    /// <summary>The fee right now in coins; clearing crimes costs more the more crimes there are.</summary>
    public int PriceCoins { get; }

    /// <summary>The fee right now in Real Money.</summary>
    public float PriceRealMoney { get; }

    /// <summary>Days the fee invoice gives to pay.</summary>
    public int PaymentDays { get; }

    /// <summary>True when the service has something to do (there are crimes to clear, the account is frozen).</summary>
    public bool CanTrigger { get; }

    /// <summary>True while it is ordered and waiting to take effect.</summary>
    public bool Queued { get; }

    /// <summary>Game seconds until a queued service takes effect, or -1.</summary>
    public int SecondsLeft { get; }

    public override string ToString() => $"{Name} ({Kind})";

    internal static LegalActionKind KindOf(LegalAction.ActionType type) => type switch
    {
        LegalAction.ActionType.ClearCrime => LegalActionKind.ClearCrime,
        LegalAction.ActionType.UnfreezeAccount => LegalActionKind.UnfreezeAccount,
        _ => LegalActionKind.None,
    };

    // Both read the crime list or the accounts, which aren't there before a save has loaded.
    private static Money.money Price(LegalAction action)
    {
        try { return action.GetPrice(); }
        catch (System.Exception) { return action.price; }
    }

    private static bool Safe(LegalAction action)
    {
        try { return action.CanTriggerAction(); }
        catch (System.Exception) { return false; }
    }
}

/// <summary>A lawyer's service event, raised by <see cref="LegalServices"/>.</summary>
public readonly struct LegalActionEvent
{
    internal LegalActionEvent(string name, LegalActionKind kind, string target)
    {
        Name = name;
        Kind = kind;
        Target = target;
    }

    public string Name { get; }

    public LegalActionKind Kind { get; }

    public string Target { get; }
}
