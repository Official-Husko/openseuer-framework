// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// The game side of <see cref="Surveillance"/> and <see cref="Janitors.Acted"/>. The trespass hooks run every
/// physics step while something stands in an area, so they return at once while nobody listens.
/// </summary>
[HarmonyPatch]
internal static class WatchPatches
{
    // Above zero while JanitorController.TriggerJanitorAction runs: actions inside it are fresh, others are replays.
    private static int _triggering;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Witness), nameof(Witness.Witnessed))]
    private static void AfterWitnessed(Witness __instance, bool __result, bool invoke, WitnessType type)
    {
        if (!__result || !invoke || type != WitnessType.Crime || !Surveillance.PlayerSpotted.HasSubscribers)
            return;
        try { Surveillance.Spot(__instance); }
        catch (Exception e) { NpcsLog.Failed("Witness hook", e); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrespassingArea), "OnTriggerStay")]
    private static void AfterStay(TrespassingArea __instance, Collider other)
    {
        if (!Surveillance.WatchesTrespass || !other.CompareTag("Player"))
            return;
        try { Surveillance.Stay(__instance); }
        catch (Exception e) { NpcsLog.Failed("Trespass hook", e); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TrespassingArea), "OnTriggerExit")]
    private static void AfterExit(TrespassingArea __instance, Collider other)
    {
        if (!Surveillance.WatchesTrespass || !other.CompareTag("Player"))
            return;
        try { Surveillance.Exit(__instance); }
        catch (Exception e) { NpcsLog.Failed("Trespass hook", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(JanitorController), nameof(JanitorController.TriggerJanitorAction))]
    private static void BeforeTrigger() => _triggering++;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(JanitorController), nameof(JanitorController.TriggerJanitorAction))]
    private static void AfterTrigger()
    {
        if (_triggering > 0)
            _triggering--;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StorageJanitorAction), nameof(StorageJanitorAction.TriggerAction))]
    private static void AfterStorage(StorageJanitorAction __instance)
    {
        if (!Janitors.Acted.HasSubscribers)
            return;
        try { Janitors.RaiseStorage(__instance, _triggering == 0); }
        catch (Exception e) { NpcsLog.Failed("Janitor hook", e); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RelayJanitor), nameof(RelayJanitor.TriggerAction))]
    private static void AfterRelay(RelayJanitor __instance)
    {
        if (!Janitors.Acted.HasSubscribers)
            return;
        try { Janitors.RaiseRelay(__instance, _triggering == 0); }
        catch (Exception e) { NpcsLog.Failed("Janitor hook", e); }
    }
}
