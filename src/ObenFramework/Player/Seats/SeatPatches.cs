// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.Saves;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Raises <see cref="Seats.Sat"/> and <see cref="Seats.Left"/>. Every way of sitting ends in the private
/// Sit(bool, bool) for the player and NPCSit(GameObject, bool) for NPCs; every way of standing up in
/// StopSitEnd and StopNPCSit(GameObject).
/// </summary>
[HarmonyPatch]
internal static class SeatPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractableChair), "Sit", new[] { typeof(bool), typeof(bool) })]
    private static void AfterSit(InteractableChair __instance, bool onLoadGame)
    {
        if (!onLoadGame && !SaveGate.IsLoading)
            Raise(Seats.Sat, __instance, null);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InteractableChair), "StopSitEnd")]
    private static void BeforeStand(InteractableChair __instance, out bool __state) => __state = __instance.PlayerSittingHere;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractableChair), "StopSitEnd")]
    private static void AfterStand(InteractableChair __instance, bool __state)
    {
        if (__state && !SaveGate.IsLoading)
            Raise(Seats.Left, __instance, null);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractableChair), nameof(InteractableChair.NPCSit), new[] { typeof(GameObject), typeof(bool) })]
    private static void AfterNpcSit(InteractableChair __instance, GameObject gameObject, bool instantly)
    {
        if (!instantly && !SaveGate.IsLoading)
            Raise(Seats.Sat, __instance, gameObject);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractableChair), nameof(InteractableChair.StopNPCSit), new[] { typeof(GameObject) })]
    private static void AfterNpcStand(InteractableChair __instance, GameObject gameObject)
    {
        if (!SaveGate.IsLoading)
            Raise(Seats.Left, __instance, gameObject);
    }

    private static void Raise(GameEvent<SeatChange> evt, InteractableChair chair, GameObject npc)
    {
        try
        {
            evt.Raise(new SeatChange(chair, npc), GameEvents.Log);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a seat change failed", e);
        }
    }
}
