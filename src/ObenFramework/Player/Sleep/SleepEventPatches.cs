// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>Hooks <see cref="SleepEvents"/> into the game's roll and its event player.</summary>
[HarmonyPatch]
internal static class SleepEventPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SleepEventController), nameof(SleepEventController.CalculateEvent))]
    private static void AfterRoll(SleepEvent[] sleepingEvents, ref SleepEvent __result)
    {
        if (!SleepEvents.HasHandlers)
            return;
        try
        {
            if (SleepEvents.Cancelled(sleepingEvents, __result))
                __result = null;
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("A sleep event handler failed", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SleepEventController), nameof(SleepEventController.ExecuteEvent))]
    private static void AfterExecute(SleepEventController.SleepEvents Ev)
    {
        if (Ev != SleepEventController.SleepEvents.None)
            SleepEvents.Occurred.Raise(Ev, GameEvents.Log);
    }
}
