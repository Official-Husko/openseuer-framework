using System;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// The dialogue money functions (Lua AddMoneyDialogue and RemoveMoneyDialogue, and the
/// RemoveMoneyRM event that takes a float) cast the amount to an int before giving or taking it,
/// so 2.5 RM shows in the notification as "2.50 RM" but only 2 RM changes hands. The fix passes the
/// real amount through for RM. Whole amounts and OC (which is whole anyway) go the game's way.
/// </summary>
internal sealed class DialogueRealMoney : ModFeature
{
    public const string Key = "DialogueRealMoney";

    public override string Id => Key;
    public override string Name => "Dialogue pays the full RM amount";
    public override string Description => "Money given or taken in conversations no longer drops the decimals of RM amounts.";
}

[HarmonyPatchCategory(DialogueRealMoney.Key)]
[HarmonyPatch(typeof(Money))]
internal static class DialogueRealMoneyPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Money.AddMoneyDialogue))]
    private static bool Add(Money __instance, double amount, bool RM)
    {
        if (!HasDecimals(amount, RM))
            return true;
        Notifications.instance.CreateNotification("Money added!", Format(amount));
        __instance.AddMoney((float)amount, Money.Currency.RM);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(Money.RemoveMoneyDialogue))]
    private static bool Remove(Money __instance, double amount, bool RM)
    {
        if (!HasDecimals(amount, RM))
            return true;
        Notifications.instance.CreateNotification("Money removed!", Format(amount));
        __instance.RemoveMoney((float)amount, Money.Currency.RM);
        return false;
    }

    private static bool HasDecimals(double amount, bool rm) =>
        rm && amount != 0.0 && amount != Math.Truncate(amount);

    // The same text the game shows for RM.
    private static string Format(double amount) => amount.ToString("F2") + " RM";
}
