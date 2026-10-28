// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.NewGames;

/// <summary>Looks at the tutorial's flags after the game's own Update, while anybody listens to <see cref="TutorialGuide.StepChanged"/>.</summary>
[HarmonyPatch]
internal static class TutorialPatches
{
    private static bool _failed;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(global::Tutorial), "Update")]
    private static void AfterUpdate(global::Tutorial __instance)
    {
        if (_failed || !TutorialGuide.StepChanged.HasSubscribers)
            return;
        try
        {
            TutorialGuide.Observe(__instance);
        }
        catch (Exception e)
        {
            // Once per load of the framework; a broken check every frame would flood the log.
            _failed = true;
            GameEvents.Log.Error("Watching the tutorial failed; TutorialGuide.StepChanged is off until the framework reloads", e);
        }
    }
}
