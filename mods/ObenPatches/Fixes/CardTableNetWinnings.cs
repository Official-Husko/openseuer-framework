using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace ObenPatches.Fixes;

/// <summary>
/// A card table keeps the player's winnings since they sat down: the stake comes off when a game starts, and
/// OnGameFinished was meant to add the payout, but adds the stake back instead. The count was always 0 after a
/// finished game, so the NPCs' "Player Ends On Win" or "Loss" remark when the player gets up never played (only
/// giving up mid-game left a count). The fix adds the payout. The payout factors are the game's own (2, 1.2, 0.8
/// and 0 of the stake for first to fourth place); they live in a local array there, so they are repeated here.
/// </summary>
internal sealed class CardTableNetWinnings : ModFeature
{
    public const string Key = "CardTableNetWinnings";

    public override string Id => Key;
    public override string Name => "Card players comment on your winnings";
    public override string Description => "Getting up from a card table after playing gets the other players' remark on how you did.";
}

[HarmonyPatchCategory(CardTableNetWinnings.Key)]
[HarmonyPatch(typeof(CardGameTable), nameof(CardGameTable.OnGameFinished))]
internal static class CardTableNetWinningsPatch
{
    private static readonly float[] Payout = { 2f, 1.2f, 0.8f, 0f };

    // OnGameFinished then adds the stake; adding the difference first leaves the payout in the count.
    private static void Prefix(int placement, int ___gameStake, ref int ___netWinnings)
    {
        if (placement < 1 || placement > Payout.Length)
            return;
        ___netWinnings += Mathf.RoundToInt(___gameStake * Payout[placement - 1]) - ___gameStake;
    }
}
