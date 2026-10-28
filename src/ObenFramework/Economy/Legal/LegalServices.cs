// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using LegalAction = LegalServicesController.LegalAction;

namespace ObenFramework.Economy;

/// <summary>
/// The lawyer's services (Samuel Godman in the Bazaar): wiping crimes and unfreezing bank accounts for a
/// fee. Ordering one sends a fee invoice and starts a timer; when it runs out the service takes effect.
/// A fee invoice that expires unpaid turns into debt with the lawyer, which he later bills at twice the
/// amount. This lists the services, orders them the way the dialogue does, reads that debt and reports
/// orders and completions. Call on the main thread.
/// </summary>
public static class LegalServices
{
    private static readonly AccessTools.FieldRef<InvoiceController, ItemReference> InvoiceItem =
        AccessTools.FieldRefAccess<InvoiceController, ItemReference>("invoiceItem");

    /// <summary>A service was ordered (its fee invoice is on its way).</summary>
    public static readonly WorldEvent<LegalActionEvent> Queued = new WorldEvent<LegalActionEvent>(nameof(Queued));

    /// <summary>A service took effect: the crimes are gone or the account is unfrozen.</summary>
    public static readonly WorldEvent<LegalActionEvent> Completed = new WorldEvent<LegalActionEvent>(nameof(Completed));

    /// <summary>True in a world: the lawyer's bookkeeping exists in every gameplay scene.</summary>
    public static bool IsAvailable => LegalServicesController.instance != null;

    /// <summary>Every service with its fee right now and whether it is ordered.</summary>
    public static List<LegalActionInfo> Actions()
    {
        var list = new List<LegalActionInfo>();
        var controller = LegalServicesController.instance;
        if (controller == null)
            return list;
        foreach (var action in controller.legalActions)
        {
            if (action == null || string.IsNullOrEmpty(action.name))
                continue;
            controller.actionQueue.TryGetValue(action.name, out var queued);
            list.Add(new LegalActionInfo(action, queued));
        }
        return list;
    }

    /// <summary>
    /// Fees owed to the lawyer from invoices that expired unpaid, keyed by the invoice name. The lawyer
    /// bills them at twice the amount when the player next asks for a service.
    /// </summary>
    public static Dictionary<string, Money.money> Debt()
    {
        var debts = new Dictionary<string, Money.money>();
        var controller = LegalServicesController.instance;
        if (controller == null)
            return debts;
        foreach (var pair in controller.debt)
        {
            if (pair.Value.IsMoreThanZero())
                debts[pair.Key] = pair.Value;
        }
        return debts;
    }

    /// <summary>True when an unpaid fee under this invoice name is on the books.</summary>
    public static bool HasDebt(string invoiceName)
    {
        var controller = LegalServicesController.instance;
        return controller != null && !string.IsNullOrEmpty(invoiceName) && controller.HasDebt(invoiceName);
    }

    /// <summary>
    /// Orders a service as the dialogue does: the fee invoice is mailed and the timer starts. False when there
    /// is no such service, it is already ordered, or it has nothing to do right now (see <see cref="LegalActionInfo.CanTrigger"/>).
    /// </summary>
    public static bool Queue(string name)
    {
        var controller = LegalServicesController.instance;
        if (controller == null || string.IsNullOrEmpty(name) || SaveGate.IsBusy || !Queueable(controller, name))
            return false;
        controller.QueueAction(name);
        return controller.actionQueue.ContainsKey(name);
    }

    private static bool Queueable(LegalServicesController controller, string name)
    {
        if (TimeOfDayAzure.instance == null || Achievements.instance == null || InvoiceController.instance == null || MailController.instance == null)
            return false;
        LegalAction action = null;
        foreach (var candidate in controller.legalActions)
        {
            if (candidate != null && candidate.name == name)
            {
                action = candidate;
                break;
            }
        }
        if (action == null || controller.actionQueue.ContainsKey(name) || !controller.CanTriggerAction(name))
            return false;
        // A paid service mails its invoice; without the invoice item the game would queue it for free.
        var item = InvoiceItem(InvoiceController.instance);
        return !action.price.IsMoreThanZero() || (item != null && item.Item != null);
    }

    internal static void RaiseQueued(LegalAction action) => Raise(Queued, action);

    internal static void RaiseCompleted(LegalAction action) => Raise(Completed, action);

    private static void Raise(WorldEvent<LegalActionEvent> e, LegalAction action)
    {
        if (action != null && e.HasSubscribers)
            e.Raise(new LegalActionEvent(action.name ?? "", LegalActionInfo.KindOf(action.type), action.actionData ?? ""));
    }
}
