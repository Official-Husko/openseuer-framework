using UnityEngine;

namespace ObenUIKit.Controls;

/// <summary>Short names for keys, in the words the game's own key bindings page uses.</summary>
internal static class KeyNames
{
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
        return key.ToString();
    }
}
