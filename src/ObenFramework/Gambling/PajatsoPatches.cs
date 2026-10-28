// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Gambling;

/// <summary>
/// The pajatso: <c>OnLaunchCoin</c> (one coin played), <c>CoinGateHit</c> (the coin went through a winning
/// gate: the game drops that many coins from the gutter onto the win tray, or none when the gutter runs dry)
/// and <c>OnCollectWins</c> (the tray goes to the wallet). The payout is scaled after the gate paid, so the
/// physical coins in the gutter don't decide whether a scaled win can be paid.
/// </summary>
[HarmonyPatch]
internal static class PajatsoPatches
{
    [HarmonyPatch(typeof(PajatsoGameplay), "OnLaunchCoin"), HarmonyPostfix]
    private static void Launched(PajatsoGameplay __instance)
    {
        if (!Casino.Played.HasSubscribers)
            return;
        try { Casino.Raise(Casino.Played, __instance, CasinoFields.PajatsoStake); }
        catch (Exception e) { WorldLog.Failed("Pajatso play hook", e); }
    }

    [HarmonyPatch(typeof(PajatsoGameplay), nameof(PajatsoGameplay.CoinGateHit)), HarmonyPrefix]
    private static void BeforeGate(int ___coinsWon, out int __state) => __state = ___coinsWon;

    [HarmonyPatch(typeof(PajatsoGameplay), nameof(PajatsoGameplay.CoinGateHit)), HarmonyPostfix]
    private static void AfterGate(PajatsoGameplay __instance, int amount, ref int ___coinsWon, Button ___CollectWinsButton, int __state)
    {
        // The gate paid only if the tray grew by the gate's amount.
        if (amount <= 0 || ___coinsWon - __state != amount)
            return;
        try
        {
            int paid = Payouts.Apply(GambleKind.Pajatso, amount);
            if (paid != amount)
            {
                ___coinsWon = __state + paid;
                if (___CollectWinsButton != null)
                    ___CollectWinsButton.useMessage = ___coinsWon > 0 ? $"Collect {___coinsWon} OC" : "Empty";
            }
            if (paid > 0)
                Casino.Raise(Casino.Won, __instance, paid);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Pajatso win hook", e);
        }
    }

    [HarmonyPatch(typeof(PajatsoGameplay), nameof(PajatsoGameplay.OnCollectWins)), HarmonyPrefix]
    private static void BeforeCollect(int ___coinsWon, out int __state) => __state = ___coinsWon;

    [HarmonyPatch(typeof(PajatsoGameplay), nameof(PajatsoGameplay.OnCollectWins)), HarmonyPostfix]
    private static void AfterCollect(PajatsoGameplay __instance, int __state)
    {
        if (__state <= 0 || !Casino.CashedOut.HasSubscribers)
            return;
        try { Casino.Raise(Casino.CashedOut, __instance, __state); }
        catch (Exception e) { WorldLog.Failed("Pajatso collect hook", e); }
    }
}
