using System;
using HarmonyLib;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>
/// Every balance change ends in <c>Money.AddMoney(float, Currency)</c> or <c>RemoveMoney(float, Currency)</c>
/// (the other overloads call these). The prefix notes the balance only while <see cref="Wallet.Changed"/> has
/// subscribers; the postfix reports the difference.
/// </summary>
[HarmonyPatch]
internal static class WalletPatches
{
    // NaN marks "nobody listened when the call started".
    private const float NotWatched = float.NaN;

    [HarmonyPatch(typeof(Money), nameof(Money.AddMoney), new[] { typeof(float), typeof(Currency) })]
    [HarmonyPrefix]
    private static void BeforeAdd(Money __instance, Currency currency, out float __state) =>
        __state = Wallet.Changed.HasSubscribers ? Read(__instance, currency) : NotWatched;

    [HarmonyPatch(typeof(Money), nameof(Money.AddMoney), new[] { typeof(float), typeof(Currency) })]
    [HarmonyPostfix]
    private static void AfterAdd(Money __instance, Currency currency, float __state) => Report(__instance, currency, __state);

    [HarmonyPatch(typeof(Money), nameof(Money.RemoveMoney), new[] { typeof(float), typeof(Currency) })]
    [HarmonyPrefix]
    private static void BeforeRemove(Money __instance, Currency type, out float __state) =>
        __state = Wallet.Changed.HasSubscribers ? Read(__instance, type) : NotWatched;

    [HarmonyPatch(typeof(Money), nameof(Money.RemoveMoney), new[] { typeof(float), typeof(Currency) })]
    [HarmonyPostfix]
    private static void AfterRemove(Money __instance, Currency type, float __state) => Report(__instance, type, __state);

    private static float Read(Money money, Currency currency) =>
        currency == Currency.RM ? money.RMoneyAmount : money.OCMoneyAmount;

    private static void Report(Money money, Currency currency, float before)
    {
        if (float.IsNaN(before))
            return;
        try
        {
            float after = Read(money, currency);
            if (after != before)
                Wallet.Changed.Raise(new MoneyChange(currency, before, after));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Wallet change hook", e);
        }
    }
}
