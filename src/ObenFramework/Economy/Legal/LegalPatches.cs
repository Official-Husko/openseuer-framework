// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;
using LegalAction = LegalServicesController.LegalAction;

namespace ObenFramework.Economy;

/// <summary>
/// <c>LegalServicesController.QueueAction</c> (a service ordered, by dialogue or by
/// <see cref="LegalServices.Queue"/>) and <c>LegalAction.TriggerAction</c> (its timer ran out and it took effect).
/// </summary>
[HarmonyPatch]
internal static class LegalPatches
{
    [HarmonyPatch(typeof(LegalServicesController), nameof(LegalServicesController.QueueAction)), HarmonyPrefix]
    private static void BeforeQueue(LegalServicesController __instance, string name, out bool __state) =>
        __state = LegalServices.Queued.HasSubscribers && name != null && !__instance.actionQueue.ContainsKey(name);

    [HarmonyPatch(typeof(LegalServicesController), nameof(LegalServicesController.QueueAction)), HarmonyPostfix]
    private static void AfterQueue(LegalServicesController __instance, string name, bool __state)
    {
        if (!__state)
            return;
        try
        {
            if (__instance.actionQueue.TryGetValue(name, out var queued))
                LegalServices.RaiseQueued(queued);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Legal service queued hook", e);
        }
    }

    [HarmonyPatch(typeof(LegalAction), nameof(LegalAction.TriggerAction)), HarmonyPostfix]
    private static void Triggered(LegalAction __instance)
    {
        try { LegalServices.RaiseCompleted(__instance); }
        catch (Exception e) { WorldLog.Failed("Legal service completed hook", e); }
    }
}
