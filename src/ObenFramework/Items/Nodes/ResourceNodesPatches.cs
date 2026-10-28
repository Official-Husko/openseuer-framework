// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>Reports hits on resource nodes, comparing the stock before and after the game's damage call.</summary>
[HarmonyPatch]
internal static class ResourceNodesPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WorkableResourceSource), nameof(WorkableResourceSource.Damage))]
    private static void Hitting(int ___currentStock, out int __state) => __state = ___currentStock;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WorkableResourceSource), nameof(WorkableResourceSource.Damage))]
    private static void Hitted(WorkableResourceSource __instance, DamageReceiver.DamageType type, int ___currentStock, int __state)
    {
        if (__state < 1 || (!ResourceNodes.Hit.HasSubscribers && !ResourceNodes.Depleted.HasSubscribers) || !WorldChanges.Active)
            return;
        try
        {
            if (!__instance.CheckDamageType(type))
                return;
            string id = ObjectIds.IdOf(__instance);
            ResourceNodes.Hit.Raise(new ResourceHit(id, ___currentStock, ___currentStock < __state));
            if (___currentStock < 1)
                ResourceNodes.Depleted.Raise(id);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Resource node hit hook", e);
        }
    }
}
