using System;
using System.Collections.Generic;
using System.Globalization;

namespace ObenLoader.Mods;

/// <summary>
/// A version range such as the <c>gameVersion</c> of a mod.json. Forms, combinable with spaces (all must
/// hold) and <c>||</c> (either side):
/// <list type="bullet">
/// <item><c>*</c> or empty: any version.</item>
/// <item><c>0.5.13</c>: that version (more parts are ignored, so 0.5.13 also matches 0.5.13.2).</item>
/// <item><c>0.5</c>, <c>0.5.x</c>, <c>0.5.*</c>: anything starting with those parts.</item>
/// <item><c>&gt;=0.5.10</c>, <c>&gt;0.5.10</c>, <c>&lt;=0.5.13</c>, <c>&lt;0.6</c>, <c>=0.5.13</c>: compared part by part, missing parts are 0.</item>
/// </list>
/// The checked version is read up to the first character that isn't a digit or a dot, so "0.5.13f1" is
/// 0.5.13 and "0.5.08" is 0.5.8.
/// </summary>
internal sealed class VersionRange
{
    private enum Op { Prefix, Equal, Greater, GreaterOrEqual, Less, LessOrEqual }

    private readonly struct Part
    {
        public readonly Op Op;
        public readonly int[] Version;

        public Part(Op op, int[] version)
        {
            Op = op;
            Version = version;
        }
    }

    // Outer list: alternatives (||). Inner: comparisons that must all hold. Empty means any version.
    private readonly List<List<Part>> _alternatives;

    private VersionRange(List<List<Part>> alternatives, string text)
    {
        _alternatives = alternatives;
        Text = text;
    }

    public string Text { get; }

    public bool IsAny => _alternatives.Count == 0;

    /// <summary>Reads a range. Returns false (and a reason) for text that isn't one.</summary>
    public static bool TryParse(string text, out VersionRange range, out string error)
    {
        range = null;
        error = null;
        var alternatives = new List<List<Part>>();
        string trimmed = (text ?? "").Trim();

        foreach (string alternative in trimmed.Split(new[] { "||" }, StringSplitOptions.None))
        {
            var parts = new List<Part>();
            bool any = false;
            foreach (string token in alternative.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TryParsePart(token, out var part, out bool matchesAll))
                {
                    error = $"'{token}' isn't a version or a comparison like >=0.5.13";
                    return false;
                }
                if (matchesAll)
                    any = true;
                else
                    parts.Add(part);
            }
            if (parts.Count == 0)
            {
                if (!any && trimmed.Length > 0)
                {
                    error = "one side of || is empty";
                    return false;
                }
                // An empty range, or "*" on its own side of ||, matches everything.
                alternatives.Clear();
                range = new VersionRange(alternatives, trimmed);
                return true;
            }
            alternatives.Add(parts);
        }
        range = new VersionRange(alternatives, trimmed);
        return true;
    }

    /// <summary>Whether <paramref name="version"/> is in the range. A version that can't be read only matches "any".</summary>
    public bool Matches(string version)
    {
        if (IsAny)
            return true;
        int[] parsed = ReadVersion(version);
        if (parsed == null)
            return false;
        foreach (var alternative in _alternatives)
        {
            bool all = true;
            foreach (var part in alternative)
            {
                if (!Holds(part, parsed))
                {
                    all = false;
                    break;
                }
            }
            if (all)
                return true;
        }
        return false;
    }

    public override string ToString() => Text;

    private static bool Holds(Part part, int[] version)
    {
        if (part.Op == Op.Prefix)
        {
            for (int i = 0; i < part.Version.Length; i++)
            {
                if (At(version, i) != part.Version[i])
                    return false;
            }
            return true;
        }
        int compare = Compare(version, part.Version);
        return part.Op switch
        {
            Op.Equal => compare == 0,
            Op.Greater => compare > 0,
            Op.GreaterOrEqual => compare >= 0,
            Op.Less => compare < 0,
            _ => compare <= 0,
        };
    }

    private static int Compare(int[] a, int[] b)
    {
        int length = Math.Max(a.Length, b.Length);
        for (int i = 0; i < length; i++)
        {
            int c = At(a, i).CompareTo(At(b, i));
            if (c != 0)
                return c;
        }
        return 0;
    }

    private static int At(int[] version, int index) => index < version.Length ? version[index] : 0;

    private static bool TryParsePart(string token, out Part part, out bool matchesAll)
    {
        part = default;
        matchesAll = false;
        Op op = Op.Prefix;
        string rest = token;
        if (token.StartsWith(">=", StringComparison.Ordinal)) { op = Op.GreaterOrEqual; rest = token.Substring(2); }
        else if (token.StartsWith("<=", StringComparison.Ordinal)) { op = Op.LessOrEqual; rest = token.Substring(2); }
        else if (token.StartsWith("==", StringComparison.Ordinal)) { op = Op.Equal; rest = token.Substring(2); }
        else if (token.StartsWith(">", StringComparison.Ordinal)) { op = Op.Greater; rest = token.Substring(1); }
        else if (token.StartsWith("<", StringComparison.Ordinal)) { op = Op.Less; rest = token.Substring(1); }
        else if (token.StartsWith("=", StringComparison.Ordinal)) { op = Op.Equal; rest = token.Substring(1); }

        if (op == Op.Prefix && (rest == "*" || rest == "x" || rest == "X"))
        {
            matchesAll = true;
            return true;
        }
        int[] version = ReadRangeVersion(rest, allowWildcard: op == Op.Prefix);
        if (version == null)
            return false;
        part = new Part(op, version);
        return true;
    }

    /// <summary>
    /// A version inside a range: numbers separated by dots, at most four, nothing else. A bare version may
    /// end in ".x" or ".*", which is the same as leaving that part out.
    /// </summary>
    private static int[] ReadRangeVersion(string text, bool allowWildcard)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        string[] pieces = text.Split('.');
        if (pieces.Length > 4)
            return null;
        var parts = new List<int>(pieces.Length);
        for (int i = 0; i < pieces.Length; i++)
        {
            string piece = pieces[i];
            if (allowWildcard && i > 0 && i == pieces.Length - 1 && (piece == "x" || piece == "X" || piece == "*"))
                break;
            if (!int.TryParse(piece, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                return null;
            parts.Add(number);
        }
        return parts.ToArray();
    }

    /// <summary>The checked version: leading numbers and dots, anything after them ("f1", "-beta") is ignored.</summary>
    private static int[] ReadVersion(string text)
    {
        if (string.IsNullOrEmpty(text))
            return null;
        var parts = new List<int>(4);
        int i = 0;
        while (parts.Count < 4)
        {
            int start = i;
            long value = 0;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
            {
                value = value * 10 + (text[i++] - '0');
                if (value > int.MaxValue)
                    return null;
            }
            if (i == start)
                break;
            parts.Add((int)value);
            if (i >= text.Length || text[i] != '.')
                break;
            i++;
        }
        return parts.Count == 0 ? null : parts.ToArray();
    }
}
