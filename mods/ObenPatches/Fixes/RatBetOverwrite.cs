using System.Runtime.CompilerServices;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// The rat fight bookie keeps offering bets during the betting time, and the bet slider allows up to the maximum
/// minus what is already staked, but RatFightArena.PlaceOCBet/PlaceRMBet set the stake to the new amount and charge
/// the wallet again: bets of 30 and 20 on Green cost 50 and only 20 rode. Picking a rat also moves an earlier stake
/// onto it, even when the slider is closed without betting. The fix adds a second bet on the same rat to the stake;
/// a bet on another rat gives the earlier stake back and starts over, and a stake stays on the rat it was placed on.
/// </summary>
internal sealed class RatBetOverwrite : ModFeature
{
    public const string Key = "RatBetOverwrite";

    public override string Id => Key;
    public override string Name => "Rat fight bets add up";
    public override string Description => "A second bet on the same rat in a round adds to your stake instead of replacing it; betting on another rat gives the first stake back.";
}

[HarmonyPatchCategory(RatBetOverwrite.Key)]
[HarmonyPatch]
internal static class RatBetOverwritePatch
{
    // The rat each arena's stake was placed on.
    private static readonly ConditionalWeakTable<RatFightArena, StrongBox<int>> StakeRat =
        new ConditionalWeakTable<RatFightArena, StrongBox<int>>();

    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.PlaceOCBet)), HarmonyPrefix]
    private static bool PlaceOc(RatFightArena __instance, int amount) => Place(__instance, amount, Money.Currency.OC);

    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.PlaceRMBet)), HarmonyPrefix]
    private static bool PlaceRm(RatFightArena __instance, int amount) => Place(__instance, amount, Money.Currency.RM);

    // Ahead of everyone else's EndRound prefix, so the payout and anything reporting it see the stake's rat.
    [HarmonyPatch(typeof(RatFightArena), nameof(RatFightArena.EndRound)), HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void BackOnStakeRat(RatFightArena __instance)
    {
        if (__instance.betAmount.IsNonZero() && StakeRat.TryGetValue(__instance, out var rat) && rat.Value >= 0)
            __instance.playerBettingOn = rat.Value;
    }

    private static bool Place(RatFightArena arena, int amount, Money.Currency currency)
    {
        if (arena.currentState != RatFightArena.State.Betting)
            return true;
        float staked = currency == Money.Currency.RM ? arena.betAmount.RM : arena.betAmount.OC;
        int rat = arena.playerBettingOn;
        var box = StakeRat.GetValue(arena, _ => new StrongBox<int>(-1));
        if (staked <= 0f)
        {
            box.Value = rat;
            return true;
        }

        float stake = amount;
        if (box.Value == rat || box.Value < 0)
            stake += staked;
        else
            Money.instance.AddMoney(staked, currency);
        if (currency == Money.Currency.RM)
            arena.betAmount.RM = stake;
        else
            arena.betAmount.OC = (int)stake;
        Money.instance.RemoveMoney(amount, currency);
        box.Value = rat;
        return false;
    }
}
