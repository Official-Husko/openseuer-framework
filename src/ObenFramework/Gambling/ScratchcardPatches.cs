// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Gambling;

/// <summary>
/// <c>Scratchcard.Use</c> draws the prize and books it in the gambling statistics (Real Money prizes as ten
/// coins each), then spawns a receipt a frame later. The prize is read back from the statistics.
/// </summary>
[HarmonyPatch(typeof(Scratchcard), nameof(Scratchcard.Use))]
internal static class ScratchcardPatches
{
    private static void Prefix(out int __state)
    {
        __state = -1;
        var stats = PlayerStats.instance;
        if (Casino.Scratched.HasSubscribers && stats != null && stats.gambling != null)
            __state = stats.gambling.OCWon;
    }

    private static void Postfix(ItemMisc item, ItemData itemData, int __state)
    {
        var stats = PlayerStats.instance;
        if (__state < 0 || item == null || itemData == null || itemData.item == null || stats == null || stats.gambling == null)
            return;
        try
        {
            int booked = stats.gambling.OCWon - __state;
            bool rm = item.val7;
            Casino.Scratched.Raise(new ScratchcardResult(itemData.item, rm ? booked / 10 : booked, rm, itemData.item.Value));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Scratch card hook", e);
        }
    }
}
