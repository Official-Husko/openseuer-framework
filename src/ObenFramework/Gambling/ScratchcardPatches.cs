// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Gambling;

/// <summary>
/// <c>Scratchcard.Use</c> draws the prize as a share of the card's top prize (<c>val1</c>), books it in the
/// gambling statistics (Real Money prizes as ten coins each) and writes it on a receipt spawned a frame later.
/// <see cref="Payouts"/> scales the top prize for the length of the call, so the statistics, the receipt and
/// what the seller pays for it all agree; the card's shared item data gets its own value back afterwards.
/// The prize is read back from the statistics.
/// </summary>
[HarmonyPatch(typeof(Scratchcard), nameof(Scratchcard.Use))]
internal static class ScratchcardPatches
{
    internal struct Draw
    {
        public int WonBefore;
        public float Top;
        public bool Scaled;
    }

    private static void Prefix(ItemMisc item, out Draw __state)
    {
        __state = new Draw { WonBefore = -1 };
        if (item == null)
            return;
        try
        {
            float factor = Payouts.Multiplier(GambleKind.Scratchcard);
            if (factor != 1f)
            {
                __state.Top = item.val1;
                __state.Scaled = true;
                item.val1 = PayoutMath.ScaleTop(item.val1, factor);
            }
            var stats = PlayerStats.instance;
            if (Casino.Scratched.HasSubscribers && stats != null && stats.gambling != null)
                __state.WonBefore = stats.gambling.OCWon;
        }
        catch (Exception e)
        {
            WorldLog.Failed("Scratch card payout hook", e);
        }
    }

    private static void Postfix(ItemMisc item, ItemData itemData, Draw __state)
    {
        var stats = PlayerStats.instance;
        if (__state.WonBefore < 0 || item == null || itemData == null || itemData.item == null || stats == null || stats.gambling == null)
            return;
        try
        {
            int booked = stats.gambling.OCWon - __state.WonBefore;
            bool rm = item.val7;
            var receipt = item.val5 != null ? item.val5.Item : null;
            Casino.Scratched.Raise(new ScratchcardResult(itemData.item, rm ? booked / 10 : booked, rm, itemData.item.Value, receipt));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Scratch card hook", e);
        }
    }

    // Runs after the postfix, and also when the game's method throws.
    private static Exception Finalizer(ItemMisc item, Draw __state, Exception __exception)
    {
        if (__state.Scaled && item != null)
            item.val1 = __state.Top;
        return __exception;
    }
}
