using System;
using HarmonyLib;

namespace ObenUIKit.NewGameScreen;

/// <summary>Puts the "Mod options" button on the New Game screen's difficulty page when the page shows.</summary>
[HarmonyPatch]
internal static class NewGameScreenPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DifficultyUI), "OnEnable")]
    private static void AfterShow(DifficultyUI __instance)
    {
        try
        {
            NewGameButton.Show(__instance);
        }
        catch (Exception e)
        {
            UIKitMod.Instance?.Log.Error("Adding the Mod options button to the New Game screen failed", e);
        }
    }
}
