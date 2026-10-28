// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>Raises <see cref="Jobs.Worked"/> with what <c>InteractableWaitWork.WaitDone</c> paid.</summary>
[HarmonyPatch(typeof(InteractableWaitWork), nameof(InteractableWaitWork.WaitDone))]
internal static class JobPatches
{
    internal struct Purse
    {
        public bool Known;
        public int Coins;
        public float RealMoney;
    }

    [HarmonyPrefix]
    private static void Before(out Purse __state)
    {
        var money = Money.instance;
        __state = money != null
            ? new Purse { Known = true, Coins = money.OCMoneyAmount, RealMoney = money.RMoneyAmount }
            : default;
    }

    [HarmonyPostfix]
    private static void After(InteractableWaitWork __instance, int timeWaited, Purse __state)
    {
        var money = Money.instance;
        if (!__state.Known || money == null || timeWaited <= 0)
            return;
        try
        {
            int coins = money.OCMoneyAmount - __state.Coins;
            float real = (float)Math.Round(money.RMoneyAmount - __state.RealMoney, 1);
            Jobs.Worked.Raise(new WorkedShift(__instance, timeWaited, coins, real), GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a shift failed", e);
        }
    }
}
