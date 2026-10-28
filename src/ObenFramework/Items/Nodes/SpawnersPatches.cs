// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>Reports spawners that spawned: every spawn, by timer or on request, goes through <c>TrySpawn</c>.</summary>
[HarmonyPatch]
internal static class SpawnersPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Spawner), nameof(Spawner.TrySpawn))]
    private static void Spawned(Spawner __instance, bool __result)
    {
        if (!__result || !Spawners.Spawned.HasSubscribers || !WorldChanges.Active)
            return;
        try { Spawners.Spawned.Raise(Spawners.Describe(__instance)); }
        catch (Exception e) { ItemsLog.Failed("Spawner hook", e); }
    }
}
