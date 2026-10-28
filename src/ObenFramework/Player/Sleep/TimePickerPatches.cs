// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>
/// Routes the sleep clock's buttons to <see cref="TimePicker"/> while one is open. With no picker open
/// every patch here returns at its first line.
/// </summary>
[HarmonyPatch]
internal static class TimePickerPatches
{
    // The sleep button confirms the pick instead of putting the player to bed.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitingUI), nameof(WaitingUI.StartSleep))]
    private static bool BeforeStartSleep()
    {
        if (!TimePicker.IsOpen)
            return true;
        Guard(TimePicker.Confirm);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitingUI), nameof(WaitingUI.CloseTimeSetUI))]
    private static void BeforeClose()
    {
        if (TimePicker.IsOpen)
            Guard(TimePicker.Cancel);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(WaitingUI), nameof(WaitingUI.UpdateSleptTime))]
    private static void AfterUpdateText()
    {
        if (TimePicker.IsOpen)
            Guard(TimePicker.Refresh);
    }

    // Passing out with the picker up would leave the slider in the mod's range.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(WaitingController), nameof(WaitingController.PassOut))]
    private static void BeforePassOut()
    {
        if (TimePicker.IsOpen && WaitingUI.instance != null)
            Guard(WaitingUI.instance.CloseTimeSetUI);
    }

    private static void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) { GameEvents.Log.Error("The time picker failed", e); }
    }
}
