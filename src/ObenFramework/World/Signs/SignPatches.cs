// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.World;

/// <summary>Notes the text when the player starts typing on a sign and compares when they stop.</summary>
[HarmonyPatch]
internal static class SignPatches
{
    [HarmonyPatch(typeof(EditableSign), "StartEditing")]
    [HarmonyPostfix]
    private static void Started(EditableSign __instance)
    {
        if (!Signs.TextChanged.HasSubscribers)
            return;
        try
        {
            // StartEditing does nothing while another sign is being edited; only a real start makes the field interactable.
            if (__instance.textComponent != null && __instance.textComponent.interactable)
                Signs.BeginEdit(__instance);
        }
        catch (Exception e) { WorldLog.Failed("Sign hook (start)", e); }
    }

    [HarmonyPatch(typeof(EditableSign), "StopEditing")]
    [HarmonyPostfix]
    private static void Stopped(EditableSign __instance)
    {
        try { Signs.EndEdit(__instance); }
        catch (Exception e) { WorldLog.Failed("Sign hook (stop)", e); }
    }
}
