// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
// The overlap rule comes from its KeyChord; the type itself is rebuilt as a plain value.
using System;
using ObenUIKit.Controls;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace ObenUIKit;

/// <summary>A main key with the modifiers that must be held with it, for example Ctrl + F5.</summary>
public readonly struct KeyChord : IEquatable<KeyChord>
{
    public KeyChord(KeyCode key, KeyModifiers modifiers = KeyModifiers.None)
    {
        Key = key;
        // A modifier key on its own is a plain key, not a chord with itself.
        Modifiers = key == KeyCode.None ? KeyModifiers.None : modifiers & ~ModifierOf(key);
    }

    public KeyCode Key { get; }

    public KeyModifiers Modifiers { get; }

    public bool IsSet => Key != KeyCode.None;

    /// <summary>
    /// True when pressing one chord also presses the other: same main key, and the modifiers of one
    /// are a subset of the other's.
    /// </summary>
    public bool Overlaps(KeyChord other)
    {
        return IsSet && SameKey(Key, other.Key) && ChordRules.Overlap(Modifiers, other.Modifiers);
    }

    /// <summary>
    /// True for the same key, counting a numbered pad's button (<c>Joystick1Button3</c>, as the game binds them) as
    /// the any-pad button the Hotkeys page stores (<c>JoystickButton3</c>).
    /// </summary>
    public static bool SameKey(KeyCode a, KeyCode b) => AnyPad(a) == AnyPad(b);

    /// <summary>A numbered pad's button as the any-pad button with the same number; other keys stay as they are.</summary>
    public static KeyCode AnyPad(KeyCode key)
    {
        if (key >= KeyCode.Joystick1Button0 && key <= KeyCode.Joystick8Button19)
            return KeyCode.JoystickButton0 + (key - KeyCode.Joystick1Button0) % 20;
        return key;
    }

    /// <summary>The modifiers held right now, leaving out the group <paramref name="key"/> belongs to.</summary>
    public static KeyModifiers HeldExcluding(KeyCode key) => Held() & ~ModifierOf(key);

    public static KeyModifiers Held()
    {
        var held = KeyModifiers.None;
        if (UInput.GetKey(KeyCode.LeftControl) || UInput.GetKey(KeyCode.RightControl))
            held |= KeyModifiers.Ctrl;
        if (UInput.GetKey(KeyCode.LeftShift) || UInput.GetKey(KeyCode.RightShift))
            held |= KeyModifiers.Shift;
        if (UInput.GetKey(KeyCode.LeftAlt) || UInput.GetKey(KeyCode.RightAlt))
            held |= KeyModifiers.Alt;
        return held;
    }

    /// <summary>Which modifier a key is, or <see cref="KeyModifiers.None"/> for any other key.</summary>
    public static KeyModifiers ModifierOf(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftControl:
            case KeyCode.RightControl:
                return KeyModifiers.Ctrl;
            case KeyCode.LeftShift:
            case KeyCode.RightShift:
                return KeyModifiers.Shift;
            case KeyCode.LeftAlt:
            case KeyCode.RightAlt:
                return KeyModifiers.Alt;
            default:
                return KeyModifiers.None;
        }
    }

    /// <summary>The words the Hotkeys page shows, for example "Ctrl + Shift + F5".</summary>
    public override string ToString()
    {
        if (!IsSet)
            return "Not set";
        string text = KeyNames.Of(Key);
        if ((Modifiers & KeyModifiers.Alt) != 0)
            text = "Alt + " + text;
        if ((Modifiers & KeyModifiers.Shift) != 0)
            text = "Shift + " + text;
        if ((Modifiers & KeyModifiers.Ctrl) != 0)
            text = "Ctrl + " + text;
        return text;
    }

    /// <summary>The form kept in hotkeys.json, for example "Ctrl+Shift+F5". Empty when not set.</summary>
    public string Serialize()
    {
        if (!IsSet)
            return "";
        string text = Key.ToString();
        if ((Modifiers & KeyModifiers.Alt) != 0)
            text = "Alt+" + text;
        if ((Modifiers & KeyModifiers.Shift) != 0)
            text = "Shift+" + text;
        if ((Modifiers & KeyModifiers.Ctrl) != 0)
            text = "Ctrl+" + text;
        return text;
    }

    /// <summary>Reads what <see cref="Serialize"/> wrote. An empty string is a chord that isn't set.</summary>
    public static bool TryParse(string text, out KeyChord chord)
    {
        chord = default;
        if (text == null)
            return false;
        if (text.Length == 0)
            return true;

        var modifiers = KeyModifiers.None;
        string[] parts = text.Split('+');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!Enum.TryParse(parts[i].Trim(), true, out KeyModifiers one) || one == KeyModifiers.None)
                return false;
            modifiers |= one;
        }
        if (!Enum.TryParse(parts[parts.Length - 1].Trim(), true, out KeyCode key) || key == KeyCode.None)
            return false;
        chord = new KeyChord(key, modifiers);
        return true;
    }

    public bool Equals(KeyChord other) => Key == other.Key && Modifiers == other.Modifiers;

    public override bool Equals(object obj) => obj is KeyChord other && Equals(other);

    public override int GetHashCode() => ((int)Key * 397) ^ (int)Modifiers;

    public static bool operator ==(KeyChord a, KeyChord b) => a.Equals(b);

    public static bool operator !=(KeyChord a, KeyChord b) => !a.Equals(b);
}
