using TMPro;
using UnityEngine;

namespace ObenUIKit.Controls;

/// <summary>A text field. <see cref="Set"/> changes the text without calling the mod back.</summary>
public sealed class FieldHandle
{
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

    /// <summary>Puts the cursor in the field, as if it had been clicked.</summary>
    public void Focus()
    {
        if (_input != null)
            _focus.Focus();
    }
}

/// <summary>A key binding row. <see cref="Set"/> changes the key shown without calling the mod back.</summary>
public sealed class KeyBindHandle
{
    private readonly TMP_Text _text;
    private bool _waiting;

    internal KeyBindHandle(TMP_Text text, KeyCode key)
    {
        _text = text;
        Key = key;
        Show();
    }

    public KeyCode Key { get; private set; }

    public void Set(KeyCode key)
    {
        Key = key;
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
        _text.text = _waiting ? "Press a key (Esc cancels)" : KeyNames.Of(Key);
    }
}
