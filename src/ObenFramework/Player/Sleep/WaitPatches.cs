// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;

namespace ObenFramework.Player;

/// <summary>
/// What a mod's <see cref="WaitOptions"/> change while its wait runs: the clock's label, no passing out,
/// and frozen needs. Each prefix is one flag check while no mod wait is running.
/// </summary>
[HarmonyPatch]
internal static class WaitPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(WaitingUI), "GetTypeString")]
    private static void ClockLabel(ref string __result)
    {
        string label = ModWait.Label;
        if (label != null && WaitingController.instance != null && WaitingController.instance.IsWaiting)
            __result = label;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitingController), nameof(WaitingController.PassOut))]
    private static bool BeforePassOut(WaitingController.Type type) =>
        !(type == WaitingController.Type.Passout && ModWait.BlockPassOut);

    // Tiredness also drives the wait clock, so with frozen needs the clock is ticked here instead.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerStatsTiredness), nameof(PlayerStatsTiredness.UpdateStats))]
    private static bool BeforeTiredness(int sec)
    {
        if (!ModWait.FreezeNeeds)
            return true;
        var waiting = WaitingController.instance;
        if (waiting != null && waiting.IsWaiting)
        {
            waiting.OnWait(sec);
            waiting.CheckEvent(sec);
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerStatsHungerAndThirst), nameof(PlayerStatsHungerAndThirst.UpdateStats))]
    private static bool BeforeHungerAndThirst() => !ModWait.FreezeNeeds;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerStatsMentalHealth), nameof(PlayerStatsMentalHealth.UpdateStats))]
    private static bool BeforeMentalHealth() => !ModWait.FreezeNeeds;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerStatsHygiene), nameof(PlayerStatsHygiene.UpdateStats))]
    private static bool BeforeHygiene() => !ModWait.FreezeNeeds;
}
