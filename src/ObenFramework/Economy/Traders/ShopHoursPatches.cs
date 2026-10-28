using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Economy;

/// <summary>
/// <c>Trade.DeltaSeconds</c>, which runs every game minute per trader and opens or closes it by its hours.
/// The prefix only copies a bool, so the hook costs nothing while no one listens.
/// </summary>
[HarmonyPatch(typeof(Trade), "DeltaSeconds")]
internal static class ShopHoursPatches
{
    private static void Prefix(Trade __instance, out bool __state) => __state = __instance.open;

    private static void Postfix(Trade __instance, bool __state)
    {
        if (__instance.open == __state)
            return;
        try { ShopHours.Changed(__instance, __instance.open); }
        catch (Exception e) { WorldLog.Failed("Shop hours hook", e); }
    }
}
