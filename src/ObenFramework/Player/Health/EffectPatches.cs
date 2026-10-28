// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>
/// Tells <see cref="PlayerEffects"/> when the game switches an effect. The game calls these on the change
/// itself, not every frame. Leaving the wasteland is a coroutine, so its end is reported when it finishes.
/// </summary>
[HarmonyPatch]
internal static class EffectPatches
{
    // One patch method per target: Harmony merges several [HarmonyPatch] attributes on one method into one.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DrunkEffect), "StartDrunkEffect")]
    private static void AfterDrunkStart() => Report(PlayerEffect.Drunk);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DrunkEffect), "StopDrunkEffect")]
    private static void AfterDrunkStop() => Report(PlayerEffect.Drunk);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MushroomEffect), "StartMushroomEffect")]
    private static void AfterMushroomStart() => Report(PlayerEffect.Mushroom);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MushroomEffect), "StopMushroomEffect")]
    private static void AfterMushroomStop() => Report(PlayerEffect.Mushroom);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OrangeMushroomEffect), "StartMushroomEffect")]
    private static void AfterOrangeStart() => Report(PlayerEffect.OrangeMushroom);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(OrangeMushroomEffect), "StopMushroomEffect")]
    private static void AfterOrangeStop() => Report(PlayerEffect.OrangeMushroom);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SMVEffects), nameof(SMVEffects.WastelandActive))]
    private static void AfterWastelandStart() => Report(PlayerEffect.Wasteland);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SMVEffects), "StopEffect")]
    private static void AfterWastelandStop(ref IEnumerator __result) => __result = ReportWhenDone(__result);

    private static IEnumerator ReportWhenDone(IEnumerator inner)
    {
        while (inner.MoveNext())
            yield return inner.Current;
        Report(PlayerEffect.Wasteland);
    }

    private static void Report(PlayerEffect effect)
    {
        try
        {
            PlayerEffects.Report(effect);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error($"Reporting the {effect} effect failed", e);
        }
    }
}
