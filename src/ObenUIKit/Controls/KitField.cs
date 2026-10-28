using ObenUIKit.Input;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Controls;

/// <summary>
/// Watches one text field's focus. While it has focus the window's navigation and Esc are held
/// off (see <see cref="InputFocus"/>); game keys are already locked by the window itself. It only
/// runs while the field is focused or is about to be, so idle fields cost nothing.
/// </summary>
internal sealed class KitField : MonoBehaviour
{
    private TMP_InputField _input;
    private bool _held;
    private int _requestFrame = -1;

    internal void Init(TMP_InputField input)
    {
        _input = input;
        enabled = false;
        input.onSelect.AddListener(_ => Watch());
    }

    /// <summary>
    /// Puts the cursor in the field. It happens a frame later: the Enter that asked for it is also
    /// waiting in the GUI event queue, and the field would take it as submitting the text at once.
    /// </summary>
    internal void Focus()
    {
        _requestFrame = Time.frameCount;
        enabled = true;
    }

    private void Watch()
    {
        if (!_held)
        {
            _held = true;
            InputFocus.Hold();
        }
        enabled = true;
    }

    private void Update()
    {
        if (_requestFrame >= 0 && Time.frameCount > _requestFrame)
        {
            _requestFrame = -1;
            _input.Select();
            _input.ActivateInputField();
        }

        if (_input.isFocused)
        {
            InputFocus.Touch();
            return;
        }
        if (_requestFrame >= 0)
            return;
        Drop();
        enabled = false;
    }

    private void OnDisable() => Drop();

    private void Drop()
    {
        if (!_held)
            return;
        _held = false;
        InputFocus.Release();
    }
}
