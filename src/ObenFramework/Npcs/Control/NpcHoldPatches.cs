// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using NPC;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// The game side of <see cref="NpcClaim.Hold"/>. Each prefix skips the game's method for an NPC a claim
/// holds that part of. Several of these run every frame for every NPC, so each returns at once while no
/// claim holds anything.
/// </summary>
[HarmonyPatch]
internal static class NpcHoldPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FadeGameObject), "Cull")]
    private static bool BeforeCull(FadeGameObject __instance) =>
        NpcControl.HeldCount == 0 || !Held(__instance, NpcHolds.Visible);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NPC_FOV), nameof(NPC_FOV.CheckIfTargetIsDetected))]
    private static bool BeforeSight(NPC_FOV __instance, ref bool __result)
    {
        if (NpcControl.HeldCount == 0 || !Held(__instance, NpcHolds.Sight))
            return true;
        __result = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InteractableTalk), "CanTalk")]
    private static bool BeforeCanTalk(InteractableTalk __instance, ref bool __result)
    {
        if (NpcControl.HeldCount == 0 || !Held(__instance, NpcHolds.Talk))
            return true;
        __result = false;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BarkStarter), nameof(BarkStarter.TryBark), typeof(Transform), typeof(Transform))]
    private static bool BeforeBark(BarkStarter __instance) =>
        NpcControl.HeldCount == 0 || !Held(__instance, NpcHolds.Barks);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DialogueSystemTrigger), nameof(DialogueSystemTrigger.Fire))]
    private static bool BeforeFire(DialogueSystemTrigger __instance) =>
        NpcControl.HeldCount == 0 || string.IsNullOrEmpty(__instance.barkConversation) || !Held(__instance, NpcHolds.Barks);

    private static bool Held(Component part, NpcHolds hold)
    {
        try
        {
            var npc = part.GetComponentInParent<NPCInfo>();
            return npc != null && NpcControl.Holds(npc, hold);
        }
        catch (Exception e)
        {
            NpcsLog.Failed("NPC hold hook", e);
            return false;
        }
    }
}
