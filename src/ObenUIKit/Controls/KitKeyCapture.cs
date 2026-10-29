// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
// The idea and the key filter come from its KeyCapture; it's rebuilt on the kit's input focus and theme.
using System;
using ObenUIKit.Input;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace ObenUIKit.Controls;

/// <summary>
/// Waits for the next key press for a key bind row. Esc, the pad's B or a click anywhere cancels. Pad buttons are
/// kept as "any pad" (<c>JoystickButton3</c>). Mouse buttons, Esc and the pad's B can't be bound; modifiers can,
/// since the game binds Walk and Crouch to them, except for a row that takes a key with modifiers, where they are
/// what is held while the key is pressed. Only enabled while waiting.
/// </summary>
internal sealed class KitKeyCapture : MonoBehaviour
{
    private static KeyCode[] _codes;

    private Action<KeyCode> _picked;
    private Action<bool> _waiting;
    private bool _active;
    private bool _skipModifiers;
    private int _startFrame;

    internal void Init(Action<KeyCode> picked, Action<bool> waiting, bool skipModifiers = false)
    {
        _picked = picked;
        _waiting = waiting;
        _skipModifiers = skipModifiers;
        enabled = false;
    }

    internal void Begin()
    {
        if (_active)
            return;
        _active = true;
        _startFrame = Time.frameCount;
        InputFocus.Hold();
        enabled = true;
        _waiting(true);
    }

    private void Update()
    {
        InputFocus.Touch();
        // The press or click that started the capture is still down in its own frame.
        if (Time.frameCount == _startFrame || !UInput.anyKeyDown)
            return;

        if (UInput.GetKeyDown(KeyCode.Escape) || UInput.GetKeyDown(KeyCode.Joystick1Button1) || UInput.GetMouseButtonDown(0))
        {
            End();
            return;
        }

        var codes = _codes ??= (KeyCode[])Enum.GetValues(typeof(KeyCode));
        for (int i = 0; i < codes.Length; i++)
        {
            var key = codes[i];
            // The any-pad buttons come before the numbered pads' in KeyCode, so a pad press lands there.
            if (key == KeyCode.None || key == KeyCode.Escape || key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6 || key > KeyCode.JoystickButton19)
                continue;
            if (!UInput.GetKeyDown(key) || _skipModifiers && IsModifier(key))
                continue;
            End();
            _picked(key);
            return;
        }
    }

    private static bool IsModifier(KeyCode key) => key switch
    {
        KeyCode.LeftControl or KeyCode.RightControl or KeyCode.LeftShift or KeyCode.RightShift => true,
        KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.AltGr => true,
        _ => false,
    };

    private void OnDisable() => End();

    private void End()
    {
        if (!_active)
            return;
        _active = false;
        enabled = false;
        InputFocus.Release();
        _waiting(false);
    }
}
