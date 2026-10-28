using ObenFramework.Console;
using ObenUIKit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CheatMenu;

/// <summary>
/// The conditions under which the hotkey may open the window: the same ones the game puts on its
/// own keys, plus "no other kit window" and "the F1 console is closed".
/// </summary>
internal static class OpenGate
{
    public static bool CanOpen()
    {
        if (UiKit.AnyWindowOpen || GameConsole.IsOpen)
            return false;
        if (TextFieldHasFocus())
            return false;

        var bindings = KeyBindingsMenu.instance;
        if (bindings != null && bindings.HasKeySelected())
            return false;

        var game = GameController.instance;
        if (game != null && (game.keysDisabled || game.GameIsPaused || game.writeLock))
            return false;

        var ui = GameUIController.instance;
        return ui == null || !ui.IsMenuVisible;
    }

    private static bool TextFieldHasFocus()
    {
        var events = EventSystem.current;
        if (events == null)
            return false;
        var selected = events.currentSelectedGameObject;
        return selected != null && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
    }
}
