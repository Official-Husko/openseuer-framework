// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>Raises <see cref="Hazards.Entered"/> from the two zones' <c>OnTriggerEnter</c>. Both run for every collider that touches a zone, so they return at once while nobody listens.</summary>
[HarmonyPatch]
internal static class HazardPatches
{
    private static bool Listening => Hazards.Entered.HasSubscribers && WorldChanges.Active;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TriggerRadiation), "OnTriggerEnter")]
    private static void BeforeRadiation(bool ___playerIn, out bool __state) => __state = ___playerIn || !Listening;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TriggerRadiation), "OnTriggerEnter")]
    private static void AfterRadiation(TriggerRadiation __instance, bool ___playerIn, bool __state)
    {
        if (__state || !___playerIn)
            return;
        try
        {
            Hazards.RaiseRadiationEntered(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Radiation zone hook", e);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TriggerHurt), "OnTriggerEnter")]
    private static void BeforeDamage(List<Stats> ___currentStats, out int __state) => __state = Listening ? ___currentStats.Count : int.MaxValue;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TriggerHurt), "OnTriggerEnter")]
    private static void AfterDamage(TriggerHurt __instance, List<Stats> ___currentStats, int __state)
    {
        if (___currentStats.Count <= __state)
            return;
        try
        {
            var entered = ___currentStats[___currentStats.Count - 1];
            if (entered != null && entered.type == Stats.Type.Player)
                Hazards.RaiseDamageEntered(__instance);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Damage zone hook", e);
        }
    }
}
