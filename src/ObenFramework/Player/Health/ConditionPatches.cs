// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;
using PlayerStatsConditions;

namespace ObenFramework.Player;

/// <summary>Reports every gain, loss and strengthening of a condition the game makes.</summary>
[HarmonyPatch]
internal static class ConditionPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BaseHealthCondition), nameof(BaseHealthCondition.ApplyCondition))]
    private static void BeforeApply(BaseHealthCondition __instance, out float __state) => __state = Before(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BaseHealthCondition), nameof(BaseHealthCondition.ApplyCondition))]
    private static void AfterApply(BaseHealthCondition __instance, float __state) => After(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BaseHealthCondition), nameof(BaseHealthCondition.RemoveCondition))]
    private static void BeforeRemove(BaseHealthCondition __instance, out float __state) => __state = Before(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BaseHealthCondition), nameof(BaseHealthCondition.RemoveCondition))]
    private static void AfterRemove(BaseHealthCondition __instance, float __state) => After(__instance, __state);

    private static float Before(BaseHealthCondition condition)
    {
        try
        {
            return PlayerConditions.Level(condition);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reading a condition failed", e);
            return float.NaN;
        }
    }

    private static void After(BaseHealthCondition condition, float before)
    {
        try
        {
            PlayerConditions.Raise(condition, before);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a condition change failed", e);
        }
    }
}
