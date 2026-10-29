using UnityEngine;

namespace ObenUIKit.Controls;

/// <summary>Short names for keys, in the words the game's own key bindings page uses.</summary>
internal static class KeyNames
{
    // The game's own pad layout (KeyBindings.ControllerA and the rest).
    private static readonly string[] PadButtons = { "A", "B", "X", "Y", "LB", "RB", "View", "Menu", "Left stick", "Right stick" };

    private static string Pad(int button) => "Pad " + (button < PadButtons.Length ? PadButtons[button] : "button " + button);

    public static string Of(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.None: return "Not set";
            case KeyCode.LeftControl: return "Left CTRL";
            case KeyCode.RightControl: return "Right CTRL";
            case KeyCode.LeftShift: return "Left Shift";
            case KeyCode.RightShift: return "Right Shift";
            case KeyCode.Escape: return "ESC";
            case KeyCode.UpArrow: return "Up";
            case KeyCode.DownArrow: return "Down";
            case KeyCode.LeftArrow: return "Left";
            case KeyCode.RightArrow: return "Right";
        }
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            return ((char)('0' + (key - KeyCode.Alpha0))).ToString();
        if (key >= KeyCode.JoystickButton0 && key <= KeyCode.Joystick8Button19)
            return Pad((int)(KeyChord.AnyPad(key) - KeyCode.JoystickButton0));
        return key.ToString();
    }
}
