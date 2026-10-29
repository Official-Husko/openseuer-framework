using ObenFramework.Console;
using ObenFramework.Game;
using ObenUIKit.Input;
using TMPro;
using UnityEngine.EventSystems;

namespace ObenUIKit;

/// <summary>
/// The conditions under which a hotkey may fire: the ones the game puts on its own keys (no menu,
/// pause, dialogue, shop or locked controls), plus no typing, no key being rebound and the F1
/// console closed. Only asked on the frame a hotkey's key goes down, never per frame.
/// </summary>
internal static class HotkeyGate
{
    public static bool CanFire(HotkeyScope scope)
    {
        if (InputFocus.Busy || GameConsole.IsOpen || TextFieldHasFocus() || RebindingGameKey())
            return false;

        var pause = PauseMenu.instance;
        bool paused = pause != null && pause.PauseIsActive();

        if (UiKit.AnyWindowOpen)
            return !paused && (scope & HotkeyScope.Windows) != 0;

        var game = GameController.instance;
        if (game == null)
            return WorldState.InMainMenu && (scope & HotkeyScope.MainMenu) != 0;

        if ((scope & HotkeyScope.InGame) == 0)
            return false;
        if (paused || game.keysDisabled || game.GameIsPaused || game.writeLock)
            return false;

        var ui = GameUIController.instance;
        if (ui != null && ui.IsMenuVisible)
            return false;
        var shop = ShopController.instance;
        if (shop != null && shop.menuIsOpen)
            return false;
        var dialogue = DialogueController.instance;
        return dialogue == null || !dialogue.DialogueActive;
    }

    private static bool RebindingGameKey()
    {
        var bindings = KeyBindingsMenu.instance;
        return bindings != null && bindings.HasKeySelected();
    }

    private static bool TextFieldHasFocus()
    {
        var events = EventSystem.current;
        if (events == null)
            return false;
        var selected = events.currentSelectedGameObject;
        if (selected == null)
            return false;
        var field = selected.GetComponentInParent<TMP_InputField>();
        return field != null && field.isFocused;
    }
}
