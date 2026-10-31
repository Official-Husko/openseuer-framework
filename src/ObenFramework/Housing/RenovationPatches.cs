// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Housing;

/// <summary>Raises <see cref="Renovations.Changed"/> and <see cref="Renovations.Finished"/>.</summary>
[HarmonyPatch]
internal static class RenovationPatches
{
    // Both StartUpdate methods (building and apartment) hand their new progress to the controller here.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.StartUpgrade))]
    private static void Started(TenementUpgradeProgress upgrade, bool silentUpgrade)
    {
        if (!silentUpgrade)
            Raise(upgrade, RenovationState.InProgress, RenovationChangeKind.Started);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementUpgradeProgress), nameof(TenementUpgradeProgress.OnUpgradeDone))]
    private static void WorkDone(TenementUpgradeProgress __instance)
    {
        if (!__instance.silentUpgrade && __instance.upgradeReady)
            Raise(__instance, RenovationState.AwaitingFinalPayment, RenovationChangeKind.WorkDone);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TenementUpgradeProgress), nameof(TenementUpgradeProgress.FinishUpgrade))]
    private static void Finished(TenementUpgradeProgress __instance, bool silentUpgrade)
    {
        if (silentUpgrade || __instance == null || __instance.upgrade == null || !WorldChanges.Active)
            return;
        if (!Renovations.Finished.HasSubscribers && !Renovations.Changed.HasSubscribers)
            return;
        try
        {
            var info = Renovations.Describe(__instance, RenovationState.Installed);
            Renovations.Finished.Raise(info);
            Renovations.Changed.Raise(new RenovationChange(info, RenovationChangeKind.Finished, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Renovation hook", e);
        }
    }

    // A press on the contractor's upgrade screen (a start or a final payment) is the player's.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UpgradeUI), nameof(UpgradeUI.OnPressed))]
    private static void Pressing() => WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(UpgradeUI), nameof(UpgradeUI.OnPressed))]
    private static Exception Pressed(Exception __exception)
    {
        WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
        return __exception;
    }

    private static void Raise(TenementUpgradeProgress progress, RenovationState state, RenovationChangeKind kind)
    {
        if (progress == null || progress.upgrade == null || !Renovations.Changed.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            Renovations.Changed.Raise(new RenovationChange(Renovations.Describe(progress, state), kind, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Renovation hook", e);
        }
    }
}
