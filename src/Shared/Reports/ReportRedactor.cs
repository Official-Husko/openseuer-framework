#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oben.Reports;

/// <summary>
/// Takes personal details out of a report's text before anyone posts it: the user name in folder paths (and the same
/// name anywhere else), Steam ids and IP addresses become "&lt;user&gt;", "&lt;steam id&gt;" and "&lt;ip&gt;".
/// Names are learned from paths first (<see cref="Learn"/> every text, then <see cref="Apply"/>), so a name that only
/// shows up in one log is hidden in all of them. Wine's own "steamuser", loopback addresses and version numbers stay.
/// </summary>
public sealed class ReportRedactor
{
    public const string UserMark = "<user>";
    public const string SteamMark = "<steam id>";
    public const string AddressMark = "<ip>";

    // The folder after these is a user's own: C:\Users\<name>, /home/<name>, /run/media/<name>, /Users/<name>.
    private static readonly Regex UserFolder = new Regex(
        @"[\\/](?:users|home|run[\\/]+media)[\\/]+(?<name>[^\\/\s""'<>:|*?]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SteamId = new Regex(@"(?<!\d)7656119\d{10}(?!\d)", RegexOptions.Compiled);

    private static readonly Regex Ipv4 = new Regex(
        @"(?<![\d.])(?<a>25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}(?![\d.]*\d)", RegexOptions.Compiled);

    // Folders under Users or home that belong to nobody in particular.
    private static readonly HashSet<string> NotPersonal = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "steamuser", "public", "default", "default user", "all users", "shared", "root", "deck", UserMark,
    };

    private readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private Regex? _namePattern;

    /// <summary>The user names found so far.</summary>
    public IReadOnlyCollection<string> Names => _names;

    /// <summary>Hides <paramref name="name"/> too, wherever it appears (a name known some other way).</summary>
    public void AddName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name!.Length < 2 || NotPersonal.Contains(name))
            return;
        if (_names.Add(name.Trim()))
            _namePattern = null;
    }

    /// <summary>Collects the user names in <paramref name="text"/>'s folder paths.</summary>
    public void Learn(string text)
    {
        foreach (Match match in UserFolder.Matches(text))
            AddName(match.Groups["name"].Value);
    }

    /// <summary>The text with every learned name, Steam id and IP address replaced.</summary>
    public string Apply(string text)
    {
        if (_names.Count > 0)
        {
            _namePattern ??= new Regex(
                @"(?<![A-Za-z0-9])(?:" + string.Join("|", _names.OrderByDescending(n => n.Length).Select(Regex.Escape)) + @")(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase);
            text = _namePattern.Replace(text, UserMark);
        }
        text = SteamId.Replace(text, SteamMark);
        return Ipv4.Replace(text, m => KeepAddress(text, m) ? m.Value : AddressMark);
    }

    // Loopback and "any" addresses tell nobody anything, and "Version=1.2.3.4" is a version, not an address.
    private static bool KeepAddress(string text, Match match)
    {
        string first = match.Groups["a"].Value;
        if (first == "127" || first == "0")
            return true;
        int start = Math.Max(0, match.Index - 8);
        string before = text.Substring(start, match.Index - start);
        return before.EndsWith("Version=", StringComparison.OrdinalIgnoreCase)
            || before.EndsWith("version ", StringComparison.OrdinalIgnoreCase)
            || before.EndsWith("v", StringComparison.Ordinal);
    }
}
