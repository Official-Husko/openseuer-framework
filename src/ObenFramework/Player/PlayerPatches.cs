using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>Raises <see cref="PlayerEvents"/> from the game's damage and death calls.</summary>
[HarmonyPatch]
internal static class PlayerPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PlayerStatsHealth), nameof(PlayerStatsHealth.Damage))]
    private static void BeforeDamage(PlayerStatsHealth __instance, DamageTypeNames reason, out float __state) =>
        __state = reason == DamageTypeNames.Bleed ? __instance.Bleeding : __instance.currentStatus;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerStatsHealth), nameof(PlayerStatsHealth.Damage))]
    private static void AfterDamage(PlayerStatsHealth __instance, DamageTypeNames reason, float __state)
    {
        float now = reason == DamageTypeNames.Bleed ? __instance.Bleeding : __instance.currentStatus;
        float lost = reason == DamageTypeNames.Bleed ? now - __state : __state - now;
        if (lost > 0f)
            PlayerEvents.Damaged.Raise(new PlayerDamage(lost, reason), GameEvents.Log);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Death), nameof(Death.PlayerDeath))]
    private static void BeforeDeath(Death __instance, out bool __state) => __state = !__instance.dead;

    // A finalizer, not a postfix: the death call can throw after it sets `dead` (an unknown damage
    // type has no sound), and the event should still fire. The exception is passed on untouched.
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Death), nameof(Death.PlayerDeath))]
    private static Exception AfterDeath(Exception __exception, Death __instance, DamageTypeNames reason, bool __state)
    {
        if (__state && __instance.dead)
            PlayerEvents.Died.Raise(reason, GameEvents.Log);
        return __exception;
    }
}
