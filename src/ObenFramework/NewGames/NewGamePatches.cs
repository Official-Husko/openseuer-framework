// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.NewGames;

/// <summary>
/// The game copies the New Game screen's choices into <c>PlayerIdentityMainMenu</c> in
/// <c>NewGame.SaveCharacter</c>, which only runs when the player confirms a new character.
/// </summary>
[HarmonyPatch]
internal static class NewGamePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(global::NewGame), nameof(global::NewGame.SaveCharacter))]
    private static void AfterSaveCharacter()
    {
        try
        {
            // The framework's newgame record was reset at the main menu; fill it before anyone hears of the new game.
            var preset = DifficultySelector.Chosen;
            NewGameOptions.CaptureNewGame();
            DifficultyPresets.RecordNewGame(preset);
            NewGameEvents.Raise(NewGameInfo.Capture(preset));
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Raising NewGameEvents.Started failed", e);
        }
    }
}
