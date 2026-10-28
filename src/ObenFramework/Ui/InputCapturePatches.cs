// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Ui;

/// <summary>
/// Turns the game's key reads to "not pressed" for captured actions. Only a read that came back true is
/// looked at, so the cost while nothing is pressed is a single bool check.
/// </summary>
[HarmonyPatch]
internal static class InputCapturePatches
{
    private static bool _failed;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetKeyDown), typeof(string))]
    private static void AfterKeyDown(InputManager __instance, string key, ref bool __result) => Filter(__instance, key, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetKeyDownKeyboardOnly), typeof(string))]
    private static void AfterKeyDownKeyboard(InputManager __instance, string key, ref bool __result) => Filter(__instance, key, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetKeyUp), typeof(string))]
    private static void AfterKeyUp(InputManager __instance, string key, ref bool __result) => Filter(__instance, key, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetKey), typeof(string))]
    private static void AfterKey(InputManager __instance, string key, ref bool __result) => Filter(__instance, key, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InputManager), nameof(InputManager.GetKeyWithHold), typeof(string), typeof(int))]
    private static void AfterKeyWithHold(InputManager __instance, string key, ref bool __result) => Filter(__instance, key, ref __result);

    private static void Filter(InputManager input, string key, ref bool result)
    {
        if (!result || _failed)
            return;
        try
        {
            if (InputCapture.Swallows(input, key))
                result = false;
        }
        catch (Exception e)
        {
            // Once per framework load; a broken check on every key read would flood the log.
            _failed = true;
            GameEvents.Log.Error("Checking input captures failed; captures are off until the framework reloads", e);
        }
    }
}
