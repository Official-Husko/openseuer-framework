// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;
using Currency = Money.Currency;

namespace ObenFramework.Gambling;

/// <summary>
/// The rat fighting arena: <c>PlaceOCBet</c>/<c>PlaceRMBet</c> (the stake leaves the wallet), <c>StartRound</c>,
/// <c>EndRound</c> (a win on the right rat pays the stake times the number of rats into <c>winAmount</c>,
/// scaled here by <see cref="Payouts"/>) and <c>CollectWinnings</c> (the winnings go to the wallet).
/// </summary>
[HarmonyPatch]
internal static class RatFightPatches
{
    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.PlaceOCBet)), HarmonyPostfix]
    private static void BetCoins(RatFightArena __instance, int amount) => Bet(__instance, amount, Currency.OC);

    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.PlaceRMBet)), HarmonyPostfix]
    private static void BetRealMoney(RatFightArena __instance, int amount) => Bet(__instance, amount, Currency.RM);

    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.StartRound)), HarmonyPrefix]
    private static void BeforeRound(RatFightArena __instance, out bool __state) =>
        __state = __instance.currentState == RatFightArena.State.Betting;

    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.StartRound)), HarmonyPostfix]
    private static void AfterRound(RatFightArena __instance, bool __state)
    {
        if (!__state || __instance.currentState != RatFightArena.State.Fighting || !Contests.Started.HasSubscribers)
            return;
        try { Contests.Started.Raise(Contests.Describe(__instance)); }
        catch (Exception e) { WorldLog.Failed("Rat fight start hook", e); }
    }

    // 0 when the round isn't being fought, 1 for a lost bet, 2 for a bet on the winner.
    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.EndRound)), HarmonyPrefix]
    private static void BeforeEnd(RatFightArena __instance, int winningCombatant, out int __state)
    {
        __state = 0;
        if (__instance.currentState != RatFightArena.State.Fighting)
            return;
        __state = __instance.playerBettingOn == winningCombatant && __instance.betAmount.IsNonZero() ? 2 : 1;
    }

    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.EndRound)), HarmonyPostfix]
    private static void AfterEnd(RatFightArena __instance, int winningCombatant, int __state)
    {
        if (__state == 0)
            return;
        try
        {
            bool won = __state == 2;
            if (won)
                Scale(__instance);
            if (!Contests.Finished.HasSubscribers)
                return;
            var combatants = __instance.combatants;
            string winner = combatants != null && winningCombatant >= 0 && winningCombatant < combatants.Count && combatants[winningCombatant] != null
                ? combatants[winningCombatant].name : null;
            var win = __instance.winAmount;
            Contests.Finished.Raise(new ContestResult(Contests.Describe(__instance), won, winner, won ? win.OC : 0, won ? win.RM : 0f));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Rat fight end hook", e);
        }
    }

    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.CollectWinnings)), HarmonyPrefix]
    private static void BeforeCollect(RatFightArena __instance, out Money.money __state) =>
        __state = __instance.currentState == RatFightArena.State.Ended ? __instance.winAmount : default;

    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.CollectWinnings)), HarmonyPostfix]
    private static void AfterCollect(RatFightArena __instance, Money.money __state)
    {
        if (!__state.IsMoreThanZero() || !Contests.WinningsCollected.HasSubscribers)
            return;
        try
        {
            bool rm = __state.RM > 0f && __state.OC == 0;
            Contests.WinningsCollected.Raise(new RatFightMoney(Contests.Describe(__instance), rm ? (int)__state.RM : __state.OC, rm ? Currency.RM : Currency.OC, -1));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Rat fight collect hook", e);
        }
    }

    private static void Scale(RatFightArena arena)
    {
        float factor = Payouts.Multiplier(GambleKind.RatFight);
        if (factor == 1f)
            return;
        var win = arena.winAmount;
        arena.winAmount = new Money.money(PayoutMath.Scale(win.OC, factor), PayoutMath.ScaleTenths(win.RM, factor));
        // Nothing to collect: back to taking bets, as after a lost round.
        if (!arena.winAmount.IsMoreThanZero())
            arena.currentState = RatFightArena.State.Betting;
    }

    private static void Bet(RatFightArena arena, int amount, Currency currency)
    {
        if (amount <= 0 || !Contests.BetPlaced.HasSubscribers || arena.currentState != RatFightArena.State.Betting)
            return;
        try { Contests.BetPlaced.Raise(new RatFightMoney(Contests.Describe(arena), amount, currency, arena.playerBettingOn)); }
        catch (Exception e) { WorldLog.Failed("Rat fight bet hook", e); }
    }
}
