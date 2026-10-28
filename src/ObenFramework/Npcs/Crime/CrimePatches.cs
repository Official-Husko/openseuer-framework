// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using NPC;

namespace ObenFramework.Npcs;

/// <summary>
/// The game side of <see cref="Pursuit.Changed"/> (an officer's state starting, an arrest, an officer's
/// activity stopping) and <see cref="Robbers.Robbed"/>. Each returns at once while nobody listens.
/// </summary>
[HarmonyPatch]
internal static class CrimePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Act_Police), nameof(Act_Police.StartState))]
    private static void AfterPoliceState() => Refresh();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Act_Police), nameof(Act_Police.OnPlayerArrested))]
    private static void AfterArrest() => Refresh();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Act_Police), "StopActivity")]
    private static void AfterPoliceStopped() => Refresh();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Act_Robber), nameof(Act_Robber.RobPlayer))]
    private static void AfterRob(Act_Robber __instance)
    {
        if (!Robbers.Robbed.HasSubscribers)
            return;
        try { Robbers.RaiseRobbed(__instance); }
        catch (Exception e) { NpcsLog.Failed("Robbed hook", e); }
    }

    private static void Refresh()
    {
        if (!Pursuit.Changed.HasSubscribers)
            return;
        try { Pursuit.Refresh(); }
        catch (Exception e) { NpcsLog.Failed("Pursuit hook", e); }
    }
}
