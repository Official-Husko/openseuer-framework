// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Ui;

/// <summary>Raises <see cref="UiState.PauseOpened"/> and <see cref="UiState.PauseClosed"/> from the pause menu's own open and close.</summary>
[HarmonyPatch]
internal static class UiStatePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PauseMenu), "PauseMenuOpen")]
    private static void AfterOpen()
    {
        try
        {
            var death = Death.instance;
            UiState.RaisePauseOpened(death != null && death.dead);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting the pause menu opening failed", e);
        }
    }

    // The game also calls the close path when the menu wasn't open (loading a save); only report a real close.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PauseMenu), "PauseMenuClose")]
    private static void BeforeClose(PauseMenu __instance, out bool __state) => __state = __instance != null && __instance.PauseIsActive();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PauseMenu), "PauseMenuClose")]
    private static void AfterClose(bool __state)
    {
        if (!__state)
            return;
        try
        {
            UiState.RaisePauseClosed();
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting the pause menu closing failed", e);
        }
    }
}
