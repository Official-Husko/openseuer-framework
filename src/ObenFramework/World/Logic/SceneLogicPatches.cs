// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.World.Logic;

/// <summary>
/// Reports relays that fire. Every relay subclass calls the base <c>triggerOutputs(object)</c>, so one
/// patch sees them all. The prefix decides whether this call will really fire (the method returns early
/// otherwise) and does nothing at all while nobody listens.
/// </summary>
[HarmonyPatch(typeof(Relay), nameof(Relay.triggerOutputs), typeof(object))]
internal static class SceneLogicPatches
{
    [HarmonyPrefix]
    private static void Before(Relay __instance, out bool __state)
    {
        __state = false;
        if (!SceneLogic.Fired.HasSubscribers)
            return;
        try { __state = SceneLogic.CanFire(__instance); }
        catch (Exception e) { WorldLog.Failed("Relay hook (before)", e); }
    }

    [HarmonyPostfix]
    private static void After(Relay __instance, bool __state)
    {
        if (!__state)
            return;
        try { SceneLogic.RaiseFired(__instance); }
        catch (Exception e) { WorldLog.Failed("Relay hook (after)", e); }
    }
}
