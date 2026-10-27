using System;

namespace ObenLoader.Mods;

/// <summary>Loose version compare: "1.2", "1.2.3" and "1.2.3-beta" all work. Pre-release tags are ignored.</summary>
internal static class SemVer
{
    public static bool Satisfies(string version, string minimum)
    {
        if (string.IsNullOrEmpty(minimum) || minimum == "*")
            return true;
        return Parse(version).CompareTo(Parse(minimum.TrimStart('>', '=', ' '))) >= 0;
    }

    private static Version Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new Version(0, 0);
        int dash = text.IndexOf('-');
        if (dash >= 0)
            text = text.Substring(0, dash);
        return Version.TryParse(text, out var v) ? v : new Version(0, 0);
    }
}
