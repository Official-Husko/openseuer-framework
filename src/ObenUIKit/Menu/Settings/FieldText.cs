using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// The text side of the Settings page: field names as words, values written for the player, lists and
/// vectors as one line of text and back, and the search match. No Unity types, so it is unit tested.
/// </summary>
internal static class FieldText
{
    /// <summary>"PauseWhileOpen" becomes "Pause while open".</summary>
    public static string Prettify(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c == '_')
                sb.Append(' ');
            else if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) && name[i - 1] != '_')
                sb.Append(' ').Append(char.ToLowerInvariant(c));
            else
                sb.Append(i == 0 ? char.ToUpperInvariant(c) : c);
        }
        return sb.ToString();
    }

    /// <summary>A value as the page shows it: On/Off, numbers without trailing zeros, enum names as words, lists joined.</summary>
    public static string Describe(object value)
    {
        switch (value)
        {
            case null:
                return "nothing";
            case bool on:
                return on ? "On" : "Off";
            case string text:
                return text.Length == 0 ? "empty" : text;
            case float f:
                return f.ToString("0.###", CultureInfo.InvariantCulture);
            case double d:
                return d.ToString("0.###", CultureInfo.InvariantCulture);
            case Enum e:
                return Prettify(e.ToString());
            case IEnumerable<string> list:
                string joined = JoinList(list);
                return joined.Length == 0 ? "empty" : joined;
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>"a, b, c".</summary>
    public static string JoinList(IEnumerable<string> items) => items == null ? "" : string.Join(", ", items);

    /// <summary>Splits on commas, trims each part and drops the empty ones.</summary>
    public static List<string> SplitList(string text)
    {
        var items = new List<string>();
        if (string.IsNullOrEmpty(text))
            return items;
        foreach (string part in text.Split(','))
        {
            string item = part.Trim();
            if (item.Length > 0)
                items.Add(item);
        }
        return items;
    }

    /// <summary>"1, 2.5, 0".</summary>
    public static string JoinNumbers(float[] values)
    {
        var parts = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
            parts[i] = values[i].ToString("0.###", CultureInfo.InvariantCulture);
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Reads exactly <paramref name="count"/> numbers separated by commas (spaces also work when there are
    /// no commas). False for anything else.
    /// </summary>
    public static bool TryNumbers(string text, int count, out float[] values)
    {
        values = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string[] parts = text.IndexOf(',') >= 0
            ? text.Split(',')
            : text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != count)
            return false;
        var read = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out read[i])
                || float.IsNaN(read[i]) || float.IsInfinity(read[i]))
                return false;
        }
        values = read;
        return true;
    }

    /// <summary>
    /// Whether every word of <paramref name="query"/> appears somewhere in <paramref name="text"/>, ignoring
    /// case: "third key" finds "Third-person key". An empty query matches everything.
    /// </summary>
    public static bool Matches(string query, string text)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;
        if (string.IsNullOrEmpty(text))
            return false;
        foreach (string word in query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        }
        return true;
    }

    /// <summary>The words a field is found by, in one string.</summary>
    public static string SearchText(params string[] parts)
    {
        var sb = new StringBuilder();
        foreach (string part in parts)
        {
            if (!string.IsNullOrEmpty(part))
                sb.Append(part).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>The options of a choice list, with <paramref name="current"/> put first when it isn't one of them.</summary>
    public static string[] WithCurrent(string[] choices, string current, out int index)
    {
        index = Array.IndexOf(choices, current);
        if (index >= 0 || current == null)
        {
            index = Math.Max(0, index);
            return choices;
        }
        var all = new string[choices.Length + 1];
        all[0] = current;
        Array.Copy(choices, 0, all, 1, choices.Length);
        index = 0;
        return all;
    }
}
