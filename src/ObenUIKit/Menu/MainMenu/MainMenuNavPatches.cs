// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenUIKit.Menu;

/// <summary>
/// Teaches the main menu's keyboard and pad navigation about the mod buttons. The game's grid code answers
/// "which button is above, below, left, right" for its six buttons and returns the current one for anything
/// else; these postfixes answer for moves out of the bottom row and between mod buttons, and leave every
/// other answer alone. With no mod buttons they change nothing.
/// </summary>
[HarmonyPatch]
internal static class MainMenuNavPatches
{
    private static bool _failed;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuNavigationHelper), "GetButtonBelow")]
    private static void AfterBelow(MainMenuNavigationHelper __instance, ref UnityEngine.UI.Button __result) =>
        Answer(__instance, ref __result, 0, 1);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuNavigationHelper), "GetButtonAbove")]
    private static void AfterAbove(MainMenuNavigationHelper __instance, ref UnityEngine.UI.Button __result) =>
        Answer(__instance, ref __result, 0, -1);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuNavigationHelper), "GetButtonOnTheLeft")]
    private static void AfterLeft(MainMenuNavigationHelper __instance, ref UnityEngine.UI.Button __result) =>
        Answer(__instance, ref __result, -1, 0);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuNavigationHelper), "GetButtonOnTheRight")]
    private static void AfterRight(MainMenuNavigationHelper __instance, ref UnityEngine.UI.Button __result) =>
        Answer(__instance, ref __result, 1, 0);

    private static void Answer(MainMenuNavigationHelper grid, ref UnityEngine.UI.Button result, int dx, int dy)
    {
        if (_failed || grid != MainMenuRow.Grid || grid == null)
            return;
        try
        {
            var target = Target(grid.CurrentObject as UnityEngine.UI.Button, dx, dy);
            if (target != null)
                result = target;
        }
        catch (Exception e)
        {
            // Once; the game's own grid keeps working without the mod rows.
            _failed = true;
            Runtime.KitRuntime.Current?.Kit.Log.Error("Main menu navigation to the mod buttons failed and is off until the kit reloads", e);
        }
    }

    private static UnityEngine.UI.Button Target(UnityEngine.UI.Button current, int dx, int dy)
    {
        var buttons = MainMenuButtons.All;
        int count = buttons.Count;
        if (current == null || count == 0)
            return null;

        int index = MainMenuButtons.IndexOf(current);
        if (index < 0)
        {
            // Only down from the game's bottom row leads into the mod rows.
            if (dy <= 0)
                return null;
            int column = Array.IndexOf(MainMenuRow.BottomRow, current);
            int below = column >= 0 ? MainMenuSlots.FromGrid(column, count) : -1;
            return below >= 0 ? buttons[below].Button : null;
        }

        int next = MainMenuSlots.Move(index, count, dx, dy);
        if (next == -2)
            return MainMenuRow.BottomRow[MainMenuSlots.GridColumnAbove(index)];
        return next >= 0 ? buttons[next].Button : null;
    }
}
