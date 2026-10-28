// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Gambling;

/// <summary>
/// <c>ContestController.StartContest</c> and <c>StopContest</c>: the drinking and sauna contests. The stop
/// prefix describes the contest while the controller still knows it; the stop clears it.
/// </summary>
[HarmonyPatch]
internal static class ContestPatches
{
    [HarmonyPatch(typeof(ContestController), nameof(ContestController.StartContest)), HarmonyPostfix]
    private static void Started(ContestController.IContestParticipant participant, ContestController.IContest contest)
    {
        if (contest == null || !Contests.Started.HasSubscribers)
            return;
        try { Contests.Started.Raise(Contests.Describe(contest, participant)); }
        catch (Exception e) { WorldLog.Failed("Contest start hook", e); }
    }

    [HarmonyPatch(typeof(ContestController), nameof(ContestController.StopContest)), HarmonyPrefix]
    private static void BeforeStop(ContestController __instance, ContestController.IContestParticipant ___activeParticipant, out ContestInfo __state)
    {
        __state = null;
        if (!Contests.Finished.HasSubscribers || __instance.ActiveContest == null)
            return;
        try { __state = Contests.Describe(__instance.ActiveContest, ___activeParticipant); }
        catch (Exception e) { WorldLog.Failed("Contest stop hook (before)", e); }
    }

    [HarmonyPatch(typeof(ContestController), nameof(ContestController.StopContest)), HarmonyPostfix]
    private static void AfterStop(bool playerWon, ContestInfo __state)
    {
        if (__state == null)
            return;
        try { Contests.Finished.Raise(new ContestResult(__state, playerWon, null, 0, 0f)); }
        catch (Exception e) { WorldLog.Failed("Contest stop hook", e); }
    }
}
