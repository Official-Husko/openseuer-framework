using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ObenUIKit.Computer;

/// <summary>
/// Keeps the computer from reacting to keys meant for a mod app. The computer gets up on Esc or Interact
/// (<c>Controls</c>), and its pad handler clicks whatever is under the cursor on A and gets up on B.
/// </summary>
[HarmonyPatch]
internal static class ComputerGates
{
    /// <summary>
    /// No getting up while a kit window is open over the computer (the Esc closes the window), on the frame it
    /// closed, or while the player types in a text field (an app's "e" isn't Interact).
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(InteractableComputer), nameof(InteractableComputer.Controls))]
    private static bool BeforeControls() => !KitWindowUp() && !Typing();

    /// <summary>The pad's A and B belong to the kit window while one is open over the computer.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ComputerControllerInput), "Update")]
    private static bool BeforePad() => !KitWindowUp();

    private static bool KitWindowUp() => UiKit.AnyWindowOpen || Time.frameCount <= ComputerApps.ClosedFrame + 1;

    private static bool Typing()
    {
        var events = EventSystem.current;
        var selected = events != null ? events.currentSelectedGameObject : null;
        var field = selected != null ? selected.GetComponent<TMP_InputField>() : null;
        return field != null && field.isFocused;
    }
}
