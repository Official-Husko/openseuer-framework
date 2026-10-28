// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Text;

namespace ObenFramework.Patching;

/// <summary>
/// Turns the exceptions Harmony throws into one readable line. Harmony wraps the real cause a few levels
/// deep ("Patching exception in method ..."), so the inner messages are added, each once, flattened to a
/// single line and cut at a sensible length.
/// </summary>
internal static class PatchErrors
{
    private const int MaxChars = 600;
    private const int MaxParts = 6;
    private const int MaxDepth = 16;

    public static string Describe(Exception e)
    {
        if (e == null)
            return "unknown error";
        var parts = new List<string>(4);
        int length = 0;
        if (e is AggregateException group)
        {
            foreach (var inner in group.InnerExceptions)
                Collect(parts, inner, ref length);
        }
        else
        {
            Collect(parts, e, ref length);
        }
        return string.Join(" | ", parts);
    }

    private static void Collect(List<string> parts, Exception e, ref int length)
    {
        for (int depth = 0; e != null && depth < MaxDepth && parts.Count < MaxParts && length < MaxChars; depth++, e = e.InnerException)
        {
            string text = Flatten(e.GetType().Name + ": " + e.Message);
            if (text.Length > MaxChars - length)
                text = text.Substring(0, Math.Max(0, MaxChars - length)) + "...";
            if (!parts.Contains(text))
            {
                parts.Add(text);
                length += text.Length;
            }
        }
    }

    private static string Flatten(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text)
        {
            bool white = c == '\r' || c == '\n' || c == '\t';
            if (white)
            {
                if (!space)
                    sb.Append(' ');
                space = true;
                continue;
            }
            sb.Append(c);
            space = c == ' ';
        }
        return sb.ToString().Trim();
    }
}
