using System;
using HarmonyLib;

namespace ObenUIKit.NewGameScreen;

/// <summary>
/// Puts the new game option rows on the New Game screen's difficulty page when it shows, and keeps them right
/// when the page redraws for another level (<c>UpdateDifficulty</c>, which the page runs on showing and on
/// every change of level).
/// </summary>
[HarmonyPatch]
internal static class NewGameScreenPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DifficultyUI), "OnEnable")]
    private static void AfterShow(DifficultyUI __instance)
    {
        try { NewGameRows.Show(__instance); }
        catch (Exception e) { UIKitMod.Instance?.Log.Error("Adding the new game options to the New Game screen failed", e); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DifficultyUI), "UpdateDifficulty")]
    private static void AfterRedraw(DifficultyUI __instance)
    {
        try { NewGameRows.Changed(__instance); }
        catch (Exception e) { UIKitMod.Instance?.Log.Error("Showing the new game options for the difficulty level failed", e); }
    }
}
