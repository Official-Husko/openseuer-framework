// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using NPC;

namespace ObenFramework.Npcs;

/// <summary>
/// Raises <see cref="Npcs.Spawned"/> and <see cref="Npcs.Despawned"/> from the game's scene NPC list, the one
/// place every spawn (<c>Character.Spawn</c>, <c>NPCInfo.Start</c>) and every removal (<c>NPCInfo.OnDestroy</c>)
/// goes through. The prefixes only note whether the NPC was already listed, so a repeated add or remove is ignored.
/// </summary>
[HarmonyPatch]
internal static class NpcPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NPCManager), nameof(NPCManager.AddNpcToList))]
    private static void BeforeAdd(NPCManager __instance, NPCInfo npc, out bool __state) =>
        __state = npc != null && !__instance.GetAllNPCs().Contains(npc);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NPCManager), nameof(NPCManager.AddNpcToList))]
    private static void AfterAdd(NPCInfo npc, bool __state)
    {
        if (!__state || !Npcs.Spawned.HasSubscribers || npc.character == null)
            return;
        try { Npcs.Spawned.Raise(new NpcRecord(Npcs.DataOf(npc.character.ID), npc)); }
        catch (Exception e) { NpcsLog.Failed("NPC spawned hook", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NPCManager), nameof(NPCManager.RemoveNpcFromList))]
    private static void BeforeRemove(NPCManager __instance, NPCInfo npc, out bool __state)
    {
        __state = npc != null && __instance.GetAllNPCs().Contains(npc);
        if (!__state)
            return;
        try
        {
            NpcControl.Forget(npc);
            if (Npcs.Despawned.HasSubscribers && npc.character != null)
                Npcs.Despawned.Raise(new NpcRecord(Npcs.DataOf(npc.character.ID), npc));
        }
        catch (Exception e)
        {
            NpcsLog.Failed("NPC despawned hook", e);
        }
    }
}
