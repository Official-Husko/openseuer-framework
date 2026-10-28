// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Gambling;

/// <summary>
/// The card table: <c>StartSelectedGame</c> takes the stake and starts the game (the table's net winnings
/// drop by the stake when it went through), and <c>OnGameFinished</c> pays out by the player's place.
/// </summary>
/// <remarks>
/// <c>OnGameFinished</c> is <c>async void</c>: the method itself only starts its state machine
/// (<c>&lt;OnGameFinished&gt;d__66</c> in 0.5.13), whose first run works out the payout, pays it and only then
/// awaits the table's clean-up. That first run happens inside the call, so a prefix and postfix on the method
/// bracket the payment. Instead of transpiling the state machine, the prefix works the payout out the same way
/// and arms <see cref="PayoutScope"/> to scale exactly that payment.
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
    private static void BeforeStart(int ___netWinnings, out int __state) => __state = ___netWinnings;

    [HarmonyPatch(typeof(CardGameTable), nameof(CardGameTable.StartSelectedGame)), HarmonyPostfix]
    private static void AfterStart(CardGameTable __instance, int ___netWinnings, int __state)
    {
        if (___netWinnings >= __state || !CardGames.Started.HasSubscribers)
            return;
        try { CardGames.Started.Raise(new CardTableInfo(__instance)); }
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
                PayoutScope.ArmMoney(CardTableFields.Currency(__instance), expected, payout);
            else if (CardTableFields.BetItemOf(__instance) is Item item)
                PayoutScope.ArmItems(item, expected, payout);

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
}
