using System.Collections.Generic;
using HarmonyLib;
using NPC;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// NPC_FOV.CheckIfNPCsAreDetected is meant to report which other NPCs this one can see (robbers use
/// it to notice police), but its loop reads the NPC's own head every time instead of the head of
/// the NPC it's looking at. So a robber either "sees" every NPC in the level or none of them. The
/// fix checks each NPC's head, and skips the NPC itself.
/// </summary>
internal sealed class RobbersSeePolice : ModFeature
{
    public const string Key = "RobbersSeePolice";

    public override string Id => Key;
    public override string Name => "Robbers notice police they can actually see";
    public override string Description => "NPC sight checks look at the other NPCs instead of the NPC's own head.";
}

[HarmonyPatchCategory(RobbersSeePolice.Key)]
[HarmonyPatch(typeof(NPC_FOV), nameof(NPC_FOV.CheckIfNPCsAreDetected))]
internal static class RobbersSeePolicePatch
{
    private static bool Prefix(NPC_FOV __instance, NPCInfo ___npcInfo, ref List<NPCReference> npcs, bool ignoreCooldown, ref bool __result)
    {
        npcs = new List<NPCReference>();
        var all = NPCManager.instance.GetAllNPCs();
        for (int i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == null || other == ___npcInfo || other.npcController == null)
                continue;
            var sight = other.GetComponent<NPC_FOV>();
            if (sight == null || sight.headPosition == null)
                continue;
            if (__instance.CheckIfTargetIsDetected(sight.headPosition.position, ignoreCooldown))
                npcs.Add(new NPCReference(other.npcController.ID));
        }
        __result = npcs.Count > 0;
        return false;
    }
}
