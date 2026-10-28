// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Gambling;

/// <summary>
/// The slot machine's steps: <c>ChargeMoney</c> (a play takes the stake from the coins in the machine),
/// <c>SlotMachineHelper.CalculateReward</c> (the only place a win is worked out; scaled by <see cref="Payouts"/>),
/// <c>HandleWinning</c>, <c>HandleDoublingResult</c> (double or nothing) and <c>HandleQuittingProcedure</c>
/// (everything goes to the wallet).
/// </summary>
[HarmonyPatch]
internal static class SlotMachinePatches
{
    [HarmonyPatch(typeof(SlotMachineHelper), nameof(SlotMachineHelper.CalculateReward)), HarmonyPostfix]
    private static void Reward(ref int __result)
    {
        try { __result = Payouts.Apply(GambleKind.SlotMachine, __result); }
        catch (Exception e) { WorldLog.Failed("Slot machine payout hook", e); }
    }

    [HarmonyPatch(typeof(SlotMachineGameplay), "ChargeMoney"), HarmonyPostfix]
    private static void Charged(SlotMachineGameplay __instance, bool __result, int chosenStakes)
    {
        if (!__result || !Casino.Played.HasSubscribers)
            return;
        try { Casino.Raise(Casino.Played, __instance, chosenStakes); }
        catch (Exception e) { WorldLog.Failed("Slot machine play hook", e); }
    }

    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleWinning"), HarmonyPostfix]
    private static void Winning(SlotMachineGameplay __instance, int ____currentRunEarnings)
    {
        if (____currentRunEarnings <= 0 || !Casino.Won.HasSubscribers)
            return;
        try { Casino.Raise(Casino.Won, __instance, ____currentRunEarnings); }
        catch (Exception e) { WorldLog.Failed("Slot machine win hook", e); }
    }

    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleDoublingResult"), HarmonyPrefix]
    private static void BeforeDoubling(int ____currentRunEarnings, out int __state) => __state = ____currentRunEarnings;

    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleDoublingResult"), HarmonyPostfix]
    private static void AfterDoubling(SlotMachineGameplay __instance, int ____currentRunEarnings, int __state)
    {
        if (__state <= 0 || !Casino.Doubled.HasSubscribers)
            return;
        try { Casino.Raise(Casino.Doubled, __instance, ____currentRunEarnings, __state); }
        catch (Exception e) { WorldLog.Failed("Slot machine doubling hook", e); }
    }

    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleQuittingProcedure"), HarmonyPrefix]
    private static void BeforeQuit(int ____moneyInMachine, int ____currentRunEarnings, out int __state) =>
        __state = ____moneyInMachine + ____currentRunEarnings;

    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleQuittingProcedure"), HarmonyPostfix]
    private static void AfterQuit(SlotMachineGameplay __instance, int __state)
    {
        if (__state <= 0 || !Casino.CashedOut.HasSubscribers)
            return;
        try { Casino.Raise(Casino.CashedOut, __instance, __state); }
        catch (Exception e) { WorldLog.Failed("Slot machine cash-out hook", e); }
    }
}
