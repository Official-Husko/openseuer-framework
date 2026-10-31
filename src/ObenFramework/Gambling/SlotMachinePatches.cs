// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Gambling;

/// <summary>
/// The slot machine's steps: <c>AddMoneyButton</c> (coins paid in at the coin slot), <c>ChargeMoney</c> (a play
/// takes the stake from the coins in the machine), <c>SlotMachineHelper.CalculateReward</c> (the only place a
/// win is worked out; scaled by <see cref="Payouts"/>), <c>HandleWinning</c>, <c>HandleDoublingResult</c> (double
/// or nothing) and <c>HandleQuittingProcedure</c> (everything goes to the wallet).
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

    [HarmonyPatch(typeof(SlotMachineGameplay), nameof(SlotMachineGameplay.AddMoneyButton)), HarmonyPostfix]
    private static void CoinsIn(SlotMachineGameplay __instance, int money)
    {
        if (money <= 0 || !Casino.CoinsInserted.HasSubscribers)
            return;
        try { Casino.Raise(Casino.CoinsInserted, __instance, money); }
        catch (Exception e) { WorldLog.Failed("Slot machine coin hook", e); }
    }

    [HarmonyPatch(typeof(SlotMachineGameplay), "ChargeMoney"), HarmonyPostfix]
    private static void Charged(SlotMachineGameplay __instance, bool __result, int chosenStakes)
    {
        if (!__result || !Casino.Played.HasSubscribers)
            return;
        try { Casino.Raise(Casino.Played, __instance, chosenStakes); }
        catch (Exception e) { WorldLog.Failed("Slot machine play hook", e); }
    }

    // HandleWinning sets _currentRunEarnings to this spin's win; earlier winnings were moved into the machine
    // when the spin started.
    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleWinning"), HarmonyPostfix]
    private static void Winning(SlotMachineGameplay __instance, int ____currentRunEarnings)
    {
        if (____currentRunEarnings <= 0)
            return;
        try { Casino.RaiseWon(__instance, ____currentRunEarnings); }
        catch (Exception e) { WorldLog.Failed("Slot machine win hook", e); }
    }

    // The result is worked out before the call: a lost toss (and a coin on its edge, which the game counts as a
    // loss but leaves the coins in the machine) clears _currentRunEarnings before the method returns.
    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleDoublingResult"), HarmonyPrefix]
    private static void BeforeDoubling(int ____currentRunEarnings, SlotMachineHelper.DoublingValues ____chosenDoublingValue,
        SlotMachineHelper.DoublingValues ____doublingResult, out int[] __state)
    {
        __state = null;
        if (____currentRunEarnings <= 0 || !Casino.Doubled.HasSubscribers)
            return;
        try
        {
            int after = SlotMachineHelper.CalculateWinningsAfterDoubling(____chosenDoublingValue, ____doublingResult, ____currentRunEarnings);
            __state = new[] { ____currentRunEarnings, after };
        }
        catch (Exception e)
        {
            WorldLog.Failed("Slot machine doubling hook", e);
        }
    }

    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleDoublingResult"), HarmonyPostfix]
    private static void AfterDoubling(SlotMachineGameplay __instance, int[] __state)
    {
        if (__state == null)
            return;
        try { Casino.Raise(Casino.Doubled, __instance, __state[1], __state[0]); }
        catch (Exception e) { WorldLog.Failed("Slot machine doubling hook", e); }
    }

    // The machine is described before the cash-out resets it (coins, winnings and stake).
    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleQuittingProcedure"), HarmonyPrefix]
    private static void BeforeQuit(SlotMachineGameplay __instance, int ____moneyInMachine, int ____currentRunEarnings, out CasinoEvent? __state)
    {
        __state = null;
        int amount = ____moneyInMachine + ____currentRunEarnings;
        if (amount <= 0 || !Casino.CashedOut.HasSubscribers)
            return;
        try { __state = new CasinoEvent(CasinoFields.Describe(__instance), amount, 0); }
        catch (Exception e) { WorldLog.Failed("Slot machine cash-out hook", e); }
    }

    [HarmonyPatch(typeof(SlotMachineGameplay), "HandleQuittingProcedure"), HarmonyPostfix]
    private static void AfterQuit(CasinoEvent? __state)
    {
        if (__state == null)
            return;
        try { Casino.CashedOut.Raise(__state.Value); }
        catch (Exception e) { WorldLog.Failed("Slot machine cash-out hook", e); }
    }
}
