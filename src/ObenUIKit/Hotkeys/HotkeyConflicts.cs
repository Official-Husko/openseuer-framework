using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// Finds hotkeys that share a key with another mod's hotkey or with one of the game's own keys: the
/// player's bindings (read from the game's <c>KeyBindings</c>, so a rebound key is noticed) and the few
/// keys the game reads directly. Modifiers are ignored for the game's keys, since a chord that includes
/// W still lets W move the player. Used by the Hotkeys page and for the warning at registration.
/// </summary>
internal static class HotkeyConflicts
{
    // Keys the game reads without InputManager, so rebinding doesn't move them.
    private static readonly KeyValuePair<KeyCode, string>[] Fixed =
    {
        new KeyValuePair<KeyCode, string>(KeyCode.F1, "developer console"),
        new KeyValuePair<KeyCode, string>(KeyCode.F11, "screenshot"),
        new KeyValuePair<KeyCode, string>(KeyCode.F12, "screenshot without UI"),
        new KeyValuePair<KeyCode, string>(KeyCode.Alpha1, "primary slot"),
        new KeyValuePair<KeyCode, string>(KeyCode.Alpha2, "auxiliary slot"),
        new KeyValuePair<KeyCode, string>(KeyCode.Alpha3, "backpack slot"),
        new KeyValuePair<KeyCode, string>(KeyCode.Alpha4, "head slot"),
        new KeyValuePair<KeyCode, string>(KeyCode.Escape, "pause menu"),
        new KeyValuePair<KeyCode, string>(KeyCode.Return, "menus"),
    };

    private static FieldInfo[] _bindingFields;

    /// <summary>What the hotkey clashes with, as a short sentence, or null when it doesn't.</summary>
    public static string Describe(Hotkey hotkey, IReadOnlyList<Hotkey> all)
    {
        var chord = hotkey.Chord;
        if (!chord.IsSet)
            return null;

        StringBuilder text = null;
        for (int i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == hotkey || !chord.Overlaps(other.Chord))
                continue;
            Add(ref text, other.OwnerName + " " + other.Label);
        }
        AddGameKeys(chord.Key, ref text);
        return text == null ? null : "Also used by " + text;
    }

    private static void AddGameKeys(KeyCode key, ref StringBuilder text)
    {
        for (int i = 0; i < Fixed.Length; i++)
        {
            if (Fixed[i].Key == key)
                Add(ref text, "the game (" + Fixed[i].Value + ")");
        }

        var input = InputManager.instance;
        var bindings = input != null ? input.keyBindings : null;
        if (bindings == null)
            return;

        var fields = _bindingFields ??= FindBindingFields();
        for (int i = 0; i < fields.Length; i++)
        {
            if ((KeyCode)fields[i].GetValue(bindings) == key)
                Add(ref text, "the game (" + Pretty(fields[i].Name) + ")");
        }
    }

    private static FieldInfo[] FindBindingFields()
    {
        var found = new List<FieldInfo>();
        foreach (var field in typeof(KeyBindings).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(KeyCode))
                found.Add(field);
        }
        return found.ToArray();
    }

    private static void Add(ref StringBuilder text, string what)
    {
        if (text == null)
            text = new StringBuilder();
        else
            text.Append(", ");
        text.Append(what);
    }

    // "hideHud" to "hide hud", "PrimaryUse" to "primary use".
    private static string Pretty(string field)
    {
        var text = new StringBuilder(field.Length + 4);
        for (int i = 0; i < field.Length; i++)
        {
            char c = field[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    text.Append(' ');
                text.Append(char.ToLowerInvariant(c));
            }
            else
            {
                text.Append(c);
            }
        }
        return text.ToString();
    }
}
