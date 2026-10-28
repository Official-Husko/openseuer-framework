// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using NPC;

namespace ObenFramework.Npcs;

/// <summary>
/// Raises <see cref="Npcs.Died"/> from <c>NPCInfo.ChangeState</c> (every death goes through it, <c>Kill</c>
/// included) and <see cref="Npcs.ActivityChanged"/> from the setter of <c>NPCDataState.CurrentActivity</c>.
/// The prefixes only note the value before; the activity hook returns at once while nobody listens.
/// </summary>
[HarmonyPatch]
internal static class NpcStatePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NPCInfo), nameof(NPCInfo.ChangeState))]
    private static void BeforeState(NPCInfo __instance, out bool __state) =>
        __state = __instance.IsDead();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NPCInfo), nameof(NPCInfo.ChangeState))]
    private static void AfterState(NPCInfo __instance, bool __state)
    {
        if (__state || !__instance.IsDead() || __instance.character == null)
            return;
        try
        {
            NpcControl.Forget(__instance);
            if (Npcs.Died.HasSubscribers)
                Npcs.Died.Raise(new NpcRecord(Npcs.DataOf(__instance.character.ID), __instance));
        }
        catch (Exception e)
        {
            NpcsLog.Failed("NPC died hook", e);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NPCDataState), nameof(NPCDataState.CurrentActivity), MethodType.Setter)]
    private static void BeforeActivity(NPCDataState __instance, out Activity __state) =>
        __state = Npcs.ActivityChanged.HasSubscribers ? __instance.CurrentActivity : null;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NPCDataState), nameof(NPCDataState.CurrentActivity), MethodType.Setter)]
    private static void AfterActivity(NPCDataState __instance, Activity __state)
    {
        if (!Npcs.ActivityChanged.HasSubscribers)
            return;
        var now = __instance.CurrentActivity;
        if (now == __state)
            return;
        try
        {
            int id = Npcs.IdOfState(__instance);
            if (id != 0)
                Npcs.ActivityChanged.Raise(new NpcActivityChange(id, NpcNames.Of(__state), NpcNames.Of(now)));
        }
        catch (Exception e)
        {
            NpcsLog.Failed("NPC activity hook", e);
        }
    }
}
