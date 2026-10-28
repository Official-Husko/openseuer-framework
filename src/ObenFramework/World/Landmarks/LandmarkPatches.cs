// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// Reports landmarks added to the map, and keeps mod landmarks out of the game's save: they are taken out
/// of the map lists right before <c>SaveController.SaveGame</c> serializes them and put back after, even
/// when the save throws.
/// </summary>
[HarmonyPatch]
internal static class LandmarkPatches
{
    /// <summary>Mod landmarks are out of the map lists right now; changes to them wait.</summary>
    internal static bool Detached;

    [HarmonyPatch(typeof(MapController), nameof(MapController.AddLandMark), typeof(Landmark), typeof(Vector3), typeof(bool))]
    [HarmonyPostfix]
    private static void Added(Landmark landmark, bool __result)
    {
        if (!__result || Landmarks.Restoring || !Landmarks.Added.HasSubscribers)
            return;
        try { Landmarks.RaiseAdded(landmark); }
        catch (Exception e) { WorldLog.Failed("Landmark added hook", e); }
    }

    [HarmonyPatch(typeof(SaveController), nameof(SaveController.SaveGame))]
    [HarmonyPrefix]
    private static void BeforeSave(out List<DetachedLandmark> __state)
    {
        __state = null;
        try
        {
            __state = Landmarks.Detach();
            Detached = __state != null;
        }
        catch (Exception e) { WorldLog.Failed("Landmark save hook (before)", e); }
    }

    [HarmonyPatch(typeof(SaveController), nameof(SaveController.SaveGame))]
    [HarmonyFinalizer]
    private static Exception AfterSave(Exception __exception, List<DetachedLandmark> __state)
    {
        try { Landmarks.Reattach(__state); }
        catch (Exception e) { WorldLog.Failed("Landmark save hook (after)", e); }
        finally { Detached = false; }
        return __exception;
    }
}
