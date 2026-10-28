// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.NewGames;

/// <summary>Raises <see cref="StartChoices.Chosen"/> when the game adds a pick (<c>StartManager.Next</c>).</summary>
[HarmonyPatch]
internal static class StartChoicesPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartManager), nameof(StartManager.Next))]
    private static void AfterNext(StartSelection.Selection selection)
    {
        if (selection == null)
            return;
        try { StartChoices.Raise(selection); }
        catch (Exception e) { GameEvents.Log.Error("Raising StartChoices.Chosen failed", e); }
    }
}
