// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player.Survival;

/// <summary>
/// Gives <see cref="BreakdownHooks"/> its hook. The game's Depression methods are plain static helpers
/// that other code could call too, so they are only intercepted while the mental health update that
/// triggers a breakdown is running.
/// </summary>
[HarmonyPatch]
internal static class BreakdownPatches
{
    private static bool _inBreakdown;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerStatsMentalHealth), nameof(PlayerStatsMentalHealth.UpdateStats))]
    private static void EnterUpdate() => _inBreakdown = true;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(PlayerStatsMentalHealth), nameof(PlayerStatsMentalHealth.UpdateStats))]
    private static void ExitUpdate() => _inBreakdown = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Depression), nameof(Depression.Suicide))]
    private static bool BeforeSuicide() => Allowed(BreakdownKind.Suicide);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Depression), nameof(Depression.HurtYourself))]
    private static bool BeforeHurt() => Allowed(BreakdownKind.SelfHarm);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Depression), nameof(Depression.UseRandomMushroomsFromInventory))]
    private static bool BeforeMushrooms(ref bool __result) => AllowedOrFalse(BreakdownKind.Mushrooms, ref __result);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Depression), nameof(Depression.DrinkRandomAlcoholFromInventory))]
    private static bool BeforeAlcohol(ref bool __result) => AllowedOrFalse(BreakdownKind.Alcohol, ref __result);

    // For methods that report whether they did anything: a cancelled act reports false.
    private static bool AllowedOrFalse(BreakdownKind kind, ref bool result)
    {
        if (Allowed(kind))
            return true;
        result = false;
        return false;
    }

    private static bool Allowed(BreakdownKind kind)
    {
        if (!_inBreakdown)
            return true;
        try
        {
            return BreakdownHooks.Allow(kind);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error($"Checking a {kind} breakdown failed", e);
            return true;
        }
    }
}
