// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace CheatMenu.Dev;

/// <summary>
/// The developer menu's plain rules: filtering, paging, telling a mod's task id apart, and
/// watch paths. No Unity types, so it is unit tested on its own.
/// </summary>
public static class DevRules
{
    /// <summary>Most identifiers a watch path may have (Type.instance.a.b.c...).</summary>
    public const int MaxPathParts = 8;

    /// <summary>Case-insensitive "contains"; an empty filter matches everything.</summary>
    public static bool Matches(string text, string filter) =>
        string.IsNullOrEmpty(filter) || (text != null && text.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

    /// <summary>The entries matching <paramref name="filter"/>, at most <paramref name="max"/>, and how many matched in all.</summary>
    public static List<string> Page(IEnumerable<string> all, string filter, int max, out int matched)
    {
        var shown = new List<string>();
        matched = 0;
        foreach (string entry in all)
        {
            if (!Matches(entry, filter))
                continue;
            matched++;
            if (shown.Count < max)
                shown.Add(entry);
        }
        return shown;
    }

    /// <summary>
    /// The mod id a mod-made task id starts with ("MyMod.intro" is MyMod's), or null for the game's own ids
    /// (which are asset names without a dot before a plain key).
    /// </summary>
    public static string TaskOwner(string taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return null;
        int dot = taskId.LastIndexOf('.');
        if (dot <= 0 || dot >= taskId.Length - 1)
            return null;
        string owner = taskId.Substring(0, dot);
        return IsName(owner, allowDots: true) && IsName(taskId.Substring(dot + 1), allowDots: false) ? owner : null;
    }

    /// <summary>
    /// Splits a member path like "instance.timeSpeedOverrideFactor" into identifiers. Null with a reason when it
    /// isn't one: empty parts, characters other than letters, digits and underscores, or too many parts.
    /// </summary>
    public static string[] SplitPath(string path, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Give a field or property name.";
            return null;
        }
        string[] parts = path.Trim().Split('.');
        if (parts.Length > MaxPathParts)
        {
            error = $"At most {MaxPathParts} names in a path.";
            return null;
        }
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i].Trim();
            if (!IsName(parts[i], allowDots: false))
            {
                error = $"'{parts[i]}' isn't a field or property name.";
                return null;
            }
        }
        return parts;
    }

    /// <summary>"Type:path", the way a watch is kept in the config.</summary>
    public static string WatchKey(string type, string path) => type.Trim() + ":" + path.Trim();

    /// <summary>Splits a kept watch back into its type and path. False for anything else.</summary>
    public static bool TrySplitWatch(string key, out string type, out string path)
    {
        type = path = null;
        if (string.IsNullOrEmpty(key))
            return false;
        int colon = key.IndexOf(':');
        if (colon <= 0 || colon >= key.Length - 1)
            return false;
        type = key.Substring(0, colon).Trim();
        path = key.Substring(colon + 1).Trim();
        return type.Length > 0 && SplitPath(path, out _) != null;
    }

    /// <summary>
    /// Reads three typed coordinates. A point or a comma both work as the decimal mark; anything that isn't a
    /// finite number makes it false.
    /// </summary>
    public static bool TryParseVector(string x, string y, string z, out float a, out float b, out float c)
    {
        b = c = 0f;
        return TryParseNumber(x, out a) & TryParseNumber(y, out b) & TryParseNumber(z, out c);
    }

    /// <summary>A typed number, with a point or a comma as the decimal mark.</summary>
    public static bool TryParseNumber(string text, out float value)
    {
        string trimmed = (text ?? "").Trim().Replace(',', '.');
        return float.TryParse(trimmed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)
            && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>Cuts long text to <paramref name="max"/> characters with "..." at the end.</summary>
    public static string Cut(string text, int max)
    {
        if (text == null)
            return "";
        return text.Length <= max ? text : text.Substring(0, Math.Max(0, max - 3)) + "...";
    }

    private static bool IsName(string text, bool allowDots)
    {
        if (string.IsNullOrEmpty(text) || char.IsDigit(text[0]))
            return false;
        foreach (char c in text)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || (allowDots && c == '.')))
                return false;
        }
        return true;
    }
}
