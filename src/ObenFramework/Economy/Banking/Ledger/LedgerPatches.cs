// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace ObenFramework.Economy;

/// <summary>
/// Every game method that changes the player's bank accounts. A prefix first writes down whatever changed
/// unseen (as <see cref="LedgerKind.Other"/>), the postfix then records the method's own change under its
/// reason. <see cref="BankLedger.Reconcile"/> catches its own errors and allocates nothing when nothing moved.
/// </summary>
[HarmonyPatch]
internal static class LedgerPatches
{
    private static void Flush() => BankLedger.Reconcile(LedgerKind.Other);

    [HarmonyPatch(typeof(Bank), nameof(Bank.DepositWithdraw)), HarmonyPrefix]
    private static void BeforeDeposit() => Flush();

    [HarmonyPatch(typeof(Bank), nameof(Bank.DepositWithdraw)), HarmonyPostfix]
    private static void AfterDeposit() => BankLedger.Reconcile(LedgerKind.Deposit);

    [HarmonyPatch(typeof(Bank), nameof(Bank.TakeLoan)), HarmonyPrefix]
    private static void BeforeLoan() => Flush();

    [HarmonyPatch(typeof(Bank), nameof(Bank.TakeLoan)), HarmonyPostfix]
    private static void AfterLoan() => BankLedger.Reconcile(LedgerKind.Loan);

    [HarmonyPatch(typeof(Bank), nameof(Bank.PayLoan)), HarmonyPrefix]
    private static void BeforeRepay() => Flush();

    [HarmonyPatch(typeof(Bank), nameof(Bank.PayLoan)), HarmonyPostfix]
    private static void AfterRepay() => BankLedger.Reconcile(LedgerKind.Repayment);

    [HarmonyPatch(typeof(Bank), nameof(Bank.CreateAccount)), HarmonyPrefix]
    private static void BeforeOpen() => Flush();

    [HarmonyPatch(typeof(Bank), nameof(Bank.CreateAccount)), HarmonyPostfix]
    private static void AfterOpen() => BankLedger.Reconcile(LedgerKind.Opened);

    [HarmonyPatch(typeof(Bank), nameof(Bank.CreateAccountWithMoney)), HarmonyPrefix]
    private static void BeforeOpenWithMoney() => Flush();

    [HarmonyPatch(typeof(Bank), nameof(Bank.CreateAccountWithMoney)), HarmonyPostfix]
    private static void AfterOpenWithMoney() => BankLedger.Reconcile(LedgerKind.Opened);

    [HarmonyPatch(typeof(Bank), nameof(Bank.AddRMToAccount)), HarmonyPrefix]
    private static void BeforeCreditRm() => Flush();

    [HarmonyPatch(typeof(Bank), nameof(Bank.AddRMToAccount)), HarmonyPostfix]
    private static void AfterCreditRm() => BankLedger.Reconcile(LedgerKind.Credit);

    [HarmonyPatch(typeof(Bank), nameof(Bank.AddOCToAccount)), HarmonyPrefix]
    private static void BeforeCreditCoins() => Flush();

    [HarmonyPatch(typeof(Bank), nameof(Bank.AddOCToAccount)), HarmonyPostfix]
    private static void AfterCreditCoins() => BankLedger.Reconcile(LedgerKind.Credit);

    [HarmonyPatch(typeof(Bank), nameof(Bank.SetAccountFrozen)), HarmonyPrefix]
    private static void BeforeFreeze() => Flush();

    // Track tells frozen and unfrozen apart by the flag itself.
    [HarmonyPatch(typeof(Bank), nameof(Bank.SetAccountFrozen)), HarmonyPostfix]
    private static void AfterFreeze() => BankLedger.Reconcile(LedgerKind.Other);

    [HarmonyPatch(typeof(Money), nameof(Money.DebtDownpaymentCallback)), HarmonyPrefix]
    private static void BeforeDebtPayment() => Flush();

    [HarmonyPatch(typeof(Money), nameof(Money.DebtDownpaymentCallback)), HarmonyPostfix]
    private static void AfterDebtPayment() => BankLedger.Reconcile(LedgerKind.DebtPayment);

    [HarmonyPatch(typeof(FakeInvoiceReceiver), "HandleMail"), HarmonyPrefix]
    private static void BeforeScam() => Flush();

    [HarmonyPatch(typeof(FakeInvoiceReceiver), "HandleMail"), HarmonyPostfix]
    private static void AfterScam() => BankLedger.Reconcile(LedgerKind.ScamPayout);

    [HarmonyPatch(typeof(LegalServicesController.LegalAction), nameof(LegalServicesController.LegalAction.TriggerAction)), HarmonyPrefix]
    private static void BeforeLegal() => Flush();

    [HarmonyPatch(typeof(LegalServicesController.LegalAction), nameof(LegalServicesController.LegalAction.TriggerAction)), HarmonyPostfix]
    private static void AfterLegal() => BankLedger.Reconcile(LedgerKind.Legal);

    [HarmonyPatch(typeof(PlayerSubscription), nameof(PlayerSubscription.HandleBankPayment)), HarmonyPrefix]
    private static void BeforeSubscription() => Flush();

    [HarmonyPatch(typeof(PlayerSubscription), nameof(PlayerSubscription.HandleBankPayment)), HarmonyPostfix]
    private static void AfterSubscription() => BankLedger.Reconcile(LedgerKind.Subscription);
}

/// <summary>
/// The daily step of <c>Money.CurrentTime</c> (it runs every game minute and does its work when the day
/// changed): weekly interest, applied by the BankInterest fix of Unofficial ObenPatches in a prefix of its own.
/// This prefix runs first, so whatever was there before the interest is written down as it was.
/// </summary>
[HarmonyPatch(typeof(Money), "CurrentTime")]
internal static class LedgerInterestPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(TimeAndDay Today, TimeAndDay ___lastCheck, out bool __state)
    {
        __state = Today != null && ___lastCheck != null && Today.currentDay > ___lastCheck.currentDay;
        if (__state)
            BankLedger.Reconcile(LedgerKind.Other);
    }

    private static void Postfix(bool __state)
    {
        if (__state)
            BankLedger.Reconcile(LedgerKind.Interest);
    }
}
