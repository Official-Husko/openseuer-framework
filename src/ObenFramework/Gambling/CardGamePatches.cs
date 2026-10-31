// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Gambling;

/// <summary>
/// The card table: <c>StartSelectedGame</c> takes the stake (the table's net winnings drop by it when it went
/// through) and starts the game a second later, which then deals (<c>CardGame.ChangeGameState</c> from
/// NotStarted to Dealing, also at tables that play for free); <c>OnGameFinished</c> pays out by the player's
/// place, and <c>LeaveTable</c> during a game gives it up.
/// </summary>
/// <remarks>
/// <c>OnGameFinished</c> is <c>async void</c>: the method itself only starts its state machine
/// (<c>&lt;OnGameFinished&gt;d__66</c> in 0.5.13), whose first run works out the payout, books it, pays it and
/// only then awaits the table's clean-up. That first run happens inside the call, so a prefix and postfix on
/// the method bracket the payment. Instead of transpiling the state machine, the prefix works the payout out
/// the same way and arms <see cref="PayoutScope"/> to scale exactly that booking and payment.
/// </remarks>
[HarmonyPatch]
internal static class CardGamePatches
{
    internal struct Finish
    {
        public CardTableInfo Table;
        public GambleKind Kind;
        public int Stake;
        public int Payout;
    }

    [HarmonyPatch(typeof(CardGameTable), nameof(CardGameTable.StartSelectedGame)), HarmonyPrefix]
    private static void BeforeStake(int ___netWinnings, out int __state) => __state = ___netWinnings;

    [HarmonyPatch(typeof(CardGameTable), nameof(CardGameTable.StartSelectedGame)), HarmonyPostfix]
    private static void AfterStake(CardGameTable __instance, int ___netWinnings, int __state)
    {
        if (___netWinnings < __state)
            CardTableFields.AddNet(__instance, ___netWinnings - __state);
    }

    [HarmonyPatch(typeof(CardGame), nameof(CardGame.ChangeGameState)), HarmonyPrefix]
    private static void BeforeState(CardGame __instance, out GameState __state) => __state = __instance.gameState;

    [HarmonyPatch(typeof(CardGame), nameof(CardGame.ChangeGameState)), HarmonyPostfix]
    private static void AfterState(CardGame __instance, GameState newGameState, GameState __state)
    {
        if (__state != GameState.NotStarted || newGameState != GameState.Dealing || __instance.cardGameTable == null || !CardGames.Started.HasSubscribers)
            return;
        try { CardGames.Started.Raise(new CardTableInfo(__instance.cardGameTable)); }
        catch (Exception e) { WorldLog.Failed("Card game start hook", e); }
    }

    [HarmonyPatch(typeof(CardGameTable), nameof(CardGameTable.OnGameFinished)), HarmonyPrefix]
    private static void BeforeFinish(CardGameTable __instance, int placement, out Finish __state)
    {
        __state = default;
        try
        {
            var game = CardTableFields.Game(__instance);
            int stake = CardTableFields.Stake(__instance);
            int expected = PayoutMath.CardPayout(stake, placement);
            if (expected < 0)
                return;
            var kind = game != null ? CardTableFields.KindOf(game) : GambleKind.All;
            int payout = Payouts.Apply(kind, expected);

            if (!CardTableFields.ItemBet(__instance))
            {
                var currency = CardTableFields.Currency(__instance);
                int coins = currency == Money.Currency.RM ? 10 : 1;
                PayoutScope.ArmMoney(currency, expected, payout);
                PayoutScope.ArmStats(expected * coins, payout * coins);
            }
            else if (CardTableFields.BetItemOf(__instance) is Item item)
            {
                PayoutScope.ArmItems(item, expected, payout);
                PayoutScope.ArmStats(item.Value * expected, item.Value * payout);
            }
            CardTableFields.AddNet(__instance, payout);

            if (CardGames.Ended.HasSubscribers)
                __state = new Finish { Table = new CardTableInfo(__instance), Kind = kind, Stake = stake, Payout = payout };
        }
        catch (Exception e)
        {
            PayoutScope.Fail(e);
        }
    }

    [HarmonyPatch(typeof(CardGameTable), nameof(CardGameTable.OnGameFinished)), HarmonyPostfix]
    private static void AfterFinish(int placement, Finish __state)
    {
        PayoutScope.Disarm();
        if (__state.Table == null)
            return;
        try { CardGames.Ended.Raise(new CardGameResult(__state.Table, __state.Kind, placement, __state.Stake, __state.Payout)); }
        catch (Exception e) { WorldLog.Failed("Card game end hook", e); }
    }

    // The game's own test for giving up (WouldBeGivingUp), read before the clean-up ends the game.
    [HarmonyPatch(typeof(CardGameTable), nameof(CardGameTable.LeaveTable)), HarmonyPrefix]
    private static void Leaving(CardGameTable __instance)
    {
        try
        {
            var game = CardTableFields.Game(__instance);
            bool givingUp = game != null && game.gameState != GameState.NotStarted && game.gameState != GameState.GameEnded;
            if (givingUp && CardGames.GaveUp.HasSubscribers)
                CardGames.GaveUp.Raise(new CardTableInfo(__instance));
            CardTableFields.ResetNet(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Card game leave hook", e);
        }
    }
}
