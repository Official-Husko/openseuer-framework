using TMPro;
using UnityEngine;

namespace ObenUIKit.Controls;

/// <summary>A text field. <see cref="Set"/> changes the text without calling the mod back.</summary>
public sealed class FieldHandle
{
    /// <summary>The control's row (or its whole box), to add uGUI of your own to it or adjust its layout.</summary>
    public RectTransform Rect { get; internal set; }

    private readonly TMP_InputField _input;
    private readonly KitField _focus;

    internal FieldHandle(TMP_InputField input, KitField focus)
    {
        _input = input;
        _focus = focus;
    }

    public string Text => _input.text;

    public bool Enabled
    {
        get => _input.interactable;
        set => _input.interactable = value;
    }

    public void Set(string text)
    {
        if (_input != null)
            _input.SetTextWithoutNotify(text ?? "");
    }

    /// <summary>
    /// Shows the text as stars, for join codes, keys and passwords on a stream or a screenshot. The text
    /// itself (<see cref="Text"/>, the callbacks) stays as typed.
    /// </summary>
    public bool Masked
    {
        get => _input != null && _input.contentType == TMP_InputField.ContentType.Password;
        set
        {
            if (_input == null)
                return;
            _input.contentType = value ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            _input.ForceLabelUpdate();
        }
    }

    /// <summary>Puts the cursor in the field, as if it had been clicked.</summary>
    public void Focus()
    {
        if (_input != null)
            _focus.Focus();
    }
}

/// <summary>A key binding row. <see cref="Set(KeyCode)"/> changes the key shown without calling the mod back.</summary>
public sealed class KeyBindHandle
{
    /// <summary>The control's row (or its whole box), to add uGUI of your own to it or adjust its layout.</summary>
    public RectTransform Rect { get; internal set; }

    private readonly TMP_Text _text;
    private bool _waiting;

    private readonly bool _chord;

    internal KeyBindHandle(TMP_Text text, KeyChord chord, bool withModifiers)
    {
        _text = text;
        _chord = withModifiers;
        Chord = chord;
        Show();
    }

    public KeyCode Key => Chord.Key;

    /// <summary>The key with its Ctrl, Shift and Alt (none for a row built with a plain key).</summary>
    public KeyChord Chord { get; private set; }

    public void Set(KeyCode key) => Set(new KeyChord(key));

    public void Set(KeyChord chord)
    {
        Chord = chord;
        Show();
    }

    internal void Waiting(bool waiting)
    {
        _waiting = waiting;
        Show();
    }

    private void Show()
    {
        if (_text == null)
            return;
        _text.text = !_waiting ? Chord.ToString()
            : _chord ? "Hold Ctrl, Shift or Alt and press a key (Esc cancels)"
            : "Press a key (Esc cancels)";
    }
}
