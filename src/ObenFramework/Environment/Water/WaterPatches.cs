using System;
using HarmonyLib;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Env;

/// <summary>Raises <see cref="WorldWater.UnderwaterChanged"/> from the underwater effect's switch.</summary>
[HarmonyPatch]
internal static class WaterPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UnderwaterEffect), nameof(UnderwaterEffect.EnableEffect), new[] { typeof(Color), typeof(float) })]
    private static void BeforeDive(UnderwaterEffect __instance, out bool __state) => __state = __instance.playerIsUnderWater;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnderwaterEffect), nameof(UnderwaterEffect.EnableEffect), new[] { typeof(Color), typeof(float) })]
    private static void AfterDive(UnderwaterEffect __instance, bool __state) => Raise(__state, __instance.playerIsUnderWater);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UnderwaterEffect), nameof(UnderwaterEffect.DisableEffect))]
    private static void BeforeSurface(UnderwaterEffect __instance, out bool __state) => __state = __instance.playerIsUnderWater;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnderwaterEffect), nameof(UnderwaterEffect.DisableEffect))]
    private static void AfterSurface(UnderwaterEffect __instance, bool __state) => Raise(__state, __instance.playerIsUnderWater);

    private static void Raise(bool was, bool now)
    {
        if (was == now || !WorldWater.UnderwaterChanged.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            WorldWater.UnderwaterChanged.Raise(now);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Underwater hook", e);
        }
    }
}
