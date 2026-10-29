using System;

namespace ObenLoader.Mods;

/// <summary>Loose version compare: "1", "1.2", "1.2.3" and "1.2.3-beta" all work. Pre-release tags are ignored.</summary>
internal static class SemVer
{
    public static bool Satisfies(string version, string minimum)
    {
        if (string.IsNullOrEmpty(minimum) || minimum == "*")
            return true;
        return Parse(version).CompareTo(Parse(StripPrefix(minimum))) >= 0;
    }

    /// <summary>The text is a version this compares properly (unreadable ones count as 0.0).</summary>
    public static bool IsReadable(string version) => TryParse(version, out _);

    /// <summary>A dependency's minimum: empty, "*", or a version with an optional "&gt;=".</summary>
    public static bool IsReadableMinimum(string minimum) =>
        string.IsNullOrEmpty(minimum) || minimum == "*" || TryParse(StripPrefix(minimum), out _);

    private static string StripPrefix(string minimum) => minimum.TrimStart('>', '=', ' ');

    private static Version Parse(string text) => TryParse(text, out var version) ? version : new Version(0, 0);

    private static bool TryParse(string text, out Version version)
    {
        version = null;
        if (string.IsNullOrEmpty(text))
            return false;
        int dash = text.IndexOf('-');
        if (dash >= 0)
            text = text.Substring(0, dash);
        // A lone number ("2") is a version too; System.Version wants at least two parts.
        if (text.IndexOf('.') < 0)
            text += ".0";
        return Version.TryParse(text, out version);
    }
}
