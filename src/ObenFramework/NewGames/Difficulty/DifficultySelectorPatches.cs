// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.NewGames;

/// <summary>
/// The New Game screen's difficulty page (<c>DifficultyUI</c>, only in the main menu) and its arrows.
/// The list is extended before the page draws, so the neighbouring names already include the presets.
/// </summary>
[HarmonyPatch]
internal static class DifficultySelectorPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DifficultyUI), "OnEnable")]
    private static void BeforeShow() => Extend();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DifficultyUI), "Start")]
    private static void BeforeStart() => Extend();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DifficultyController), nameof(DifficultyController.ChangeDifficulty), typeof(bool))]
    private static void AfterMove(DifficultyController __instance)
    {
        try { DifficultySelector.Moved(__instance); }
        catch (Exception e) { GameDifficulty.Failed("following the difficulty selector", e); }
    }

    private static void Extend()
    {
        try { DifficultySelector.Extend(); }
        catch (Exception e) { GameDifficulty.Failed("adding the presets to the difficulty selector", e); }
    }
}
