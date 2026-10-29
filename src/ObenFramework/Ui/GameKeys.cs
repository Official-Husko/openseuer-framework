// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;

namespace ObenFramework.Ui;

/// <summary>
/// The game's own key bindings, by action name ("Interact", "Jump", "Inventory", "Map"; the list is in
/// docs/modding/framework/ui-state.md): the key and pad button the player bound, how the game writes the
/// key on screen, and whether the action is pressed. Presses are read through the game's
/// <c>InputManager</c> like the game reads them, so an <see cref="InputCapture"/> of the action makes them
/// read as not pressed. Everything answers <c>KeyCode.None</c>, "" or false before the game's input is up.
/// </summary>
public static class GameKeys
{
    /// <summary>The game's input manager and its bindings are loaded.</summary>
    public static bool IsAvailable
    {
        get
        {
            var input = InputManager.instance;
            return input != null && input.keyBindings != null;
        }
    }

    /// <summary>The keyboard key bound to <paramref name="action"/>.</summary>
    public static KeyCode Key(string action) =>
        Input(action) is { } input ? input.GetKeyCode(action, ignoreController: true) : KeyCode.None;

    /// <summary>The controller button bound to <paramref name="action"/>.</summary>
    public static KeyCode Button(string action) =>
        Input(action) is { } input ? input.keyBindings.CheckKeyController(action) : KeyCode.None;

    /// <summary>The key as the game shows it ("E", "SPACE"), for the device the player is using right now.</summary>
    public static string Label(string action) =>
        Input(action) is { } input ? input.GetKeyCodeDisplayText(action) : "";

    /// <summary>The action went down this frame.</summary>
    public static bool Down(string action) => Input(action) is { } input && input.GetKeyDown(action);

    /// <summary>The action is held.</summary>
    public static bool Held(string action) => Input(action) is { } input && input.GetKey(action);

    /// <summary>The action was let go this frame.</summary>
    public static bool Up(string action) => Input(action) is { } input && input.GetKeyUp(action);

    // The game's InputManager is a scene object: check it the Unity way, not with ?. (a destroyed one isn't null).
    private static InputManager Input(string action)
    {
        if (string.IsNullOrEmpty(action))
            throw new ArgumentException("Name one of the game's actions, like \"Interact\".", nameof(action));
        var input = InputManager.instance;
        return input != null && input.keyBindings != null ? input : null;
    }
}
