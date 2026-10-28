// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Economy;

/// <summary>
/// The invoice keeper's methods: <c>CreateInvoiceItem</c> (every new invoice), <c>PayInvoice</c> (the
/// payment machine), <c>CancelInvoice</c> and <c>CheckDueDates</c> (runs every game minute; expires
/// overdue invoices when the day changed). Each handler returns at once while no one listens.
/// </summary>
[HarmonyPatch]
internal static class InvoicePatches
{
    // The invoices open before a due-date check, reused so the daily check allocates nothing.
    private static readonly List<ItemInvoiceData> BeforeCheck = new List<ItemInvoiceData>();

    [HarmonyPatch(typeof(InvoiceController), nameof(InvoiceController.CreateInvoiceItem)), HarmonyPostfix]
    private static void Created(InvoiceController __instance)
    {
        if (!Invoices.Issued.HasSubscribers)
            return;
        try
        {
            int count = __instance.invoices.Count;
            if (count > 0)
                Invoices.RaiseIssued(__instance.invoices[count - 1]);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Invoice issued hook", e);
        }
    }

    [HarmonyPatch(typeof(InvoiceController), nameof(InvoiceController.PayInvoice)), HarmonyPrefix]
    private static void BeforePay(InvoiceController __instance, int ID, out ItemInvoiceData __state) =>
        __state = Invoices.Paid.HasSubscribers ? __instance.GetInvoice(ID) : null;

    [HarmonyPatch(typeof(InvoiceController), nameof(InvoiceController.PayInvoice)), HarmonyPostfix]
    private static void AfterPay(bool __result, ItemInvoiceData __state)
    {
        if (!__result || __state == null)
            return;
        try { Invoices.RaisePaid(__state, InvoicePayer.Wallet); }
        catch (Exception e) { WorldLog.Failed("Invoice paid hook", e); }
    }

    [HarmonyPatch(typeof(InvoiceController), nameof(InvoiceController.CancelInvoice)), HarmonyPrefix]
    private static void BeforeCancel(InvoiceController __instance, int ID, out ItemInvoiceData __state) =>
        __state = Invoices.Cancelled.HasSubscribers ? __instance.GetInvoice(ID) : null;

    [HarmonyPatch(typeof(InvoiceController), nameof(InvoiceController.CancelInvoice)), HarmonyPostfix]
    private static void AfterCancel(InvoiceController __instance, ItemInvoiceData __state)
    {
        if (__state == null || __instance.invoices.Contains(__state))
            return;
        try { Invoices.RaiseCancelled(__state); }
        catch (Exception e) { WorldLog.Failed("Invoice cancelled hook", e); }
    }

    [HarmonyPatch(typeof(InvoiceController), nameof(InvoiceController.CheckDueDates)), HarmonyPrefix]
    private static void BeforeDueCheck(InvoiceController __instance, TimeAndDay date, TimeAndDay ___lastChecked, out bool __state)
    {
        __state = false;
        if (!Invoices.Expired.HasSubscribers || date == null || ___lastChecked == null || date.currentDay <= ___lastChecked.currentDay)
            return;
        BeforeCheck.Clear();
        BeforeCheck.AddRange(__instance.invoices);
        __state = true;
    }

    [HarmonyPatch(typeof(InvoiceController), nameof(InvoiceController.CheckDueDates)), HarmonyPostfix]
    private static void AfterDueCheck(InvoiceController __instance, bool __state)
    {
        if (!__state)
            return;
        try
        {
            foreach (var invoice in BeforeCheck)
            {
                if (invoice != null && !__instance.invoices.Contains(invoice))
                    Invoices.RaiseExpired(invoice);
            }
        }
        catch (Exception e)
        {
            WorldLog.Failed("Invoice expired hook", e);
        }
        finally
        {
            BeforeCheck.Clear();
        }
    }
}
