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
        // Each step on its own, so one failing still lets the session count as a new game and the event fire.
        // The framework's newgame record was reset at the main menu; fill it before anyone hears of the new game.
        DifficultyPreset preset = null;
        Step("reading the difficulty preset", () => preset = DifficultySelector.Chosen);
        Step("storing the new game options", NewGameOptions.CaptureNewGame);
        Step("recording the difficulty preset", () => DifficultyPresets.RecordNewGame(preset));
        NewGameInfo info = null;
        Step("describing the new game", () => info = NewGameInfo.Capture(preset));
        NewGameEvents.Raise(info ?? NewGameInfo.Empty(preset));
    }

    private static void Step(string what, Action action)
    {
        try { action(); }
        catch (Exception e) { GameEvents.Log.Error("New game: " + what + " failed", e); }
    }
}
