// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player.Survival;

/// <summary>Lets <see cref="DamageHooks"/> change or cancel the game's damage call before it runs.</summary>
[HarmonyPatch]
internal static class DamagePatches
{
    // Zero makes the game's own "damage == 0, return" check skip the whole call, and PlayerEvents.Damaged
    // (which compares health before and after) then reports nothing, as it should for a cancelled hit.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(PlayerStatsHealth), nameof(PlayerStatsHealth.Damage))]
    private static void BeforeDamage(ref float damage, DamageTypeNames reason, bool silent)
    {
        try
        {
            damage = DamageHooks.Dispatch(damage, reason, silent);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Damage interception failed", e);
        }
    }
}
