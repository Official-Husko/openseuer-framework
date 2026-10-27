using System.Text.RegularExpressions;

namespace ObenTool.Changelog;

/// <summary>A commit reduced to what the changelog needs.</summary>
public sealed record CommitEntry(string Hash, DateOnly Date, string Type, string? Scope, IReadOnlyList<ChangeLine> Lines);

/// <summary>One player-facing line from a Changelog-* trailer.</summary>
public sealed record ChangeLine(ChangeKind Kind, string Text);

public enum ChangeKind
{
    Added,
    Changed,
    Fixed,
    Removed,
}

/// <summary>Reads commits and their Changelog-* trailers from git.</summary>
public static partial class CommitLog
{
    private const char FieldSep = '\u001f';
    private const char RecordSep = '\u001e';

    /// <summary>Commits in <paramref name="range"/> (e.g. "v0.1.0..HEAD"), oldest first.</summary>
    public static List<CommitEntry> Read(string repo, string range)
    {
        string format = "%H%x1f%as%x1f%s%x1f%(trailers:only,unfold)%x1e";
        string raw = Git.Run(repo, "log", "--reverse", $"--format={format}", range);

        var commits = new List<CommitEntry>();
        foreach (string record in raw.Split(RecordSep, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] f = record.Trim('\n').Split(FieldSep);
            if (f.Length < 4)
                continue;

            var subject = Subject().Match(f[2]);
            commits.Add(new CommitEntry(
                f[0],
                DateOnly.Parse(f[1]),
                subject.Success ? subject.Groups["type"].Value : "other",
                subject.Success && subject.Groups["scope"].Success ? subject.Groups["scope"].Value : null,
                ParseTrailers(f[3])));
        }
        return commits;
    }

    private static List<ChangeLine> ParseTrailers(string trailers)
    {
        var lines = new List<ChangeLine>();
        foreach (Match m in Trailer().Matches(trailers))
        {
            if (Enum.TryParse(m.Groups["kind"].Value, ignoreCase: true, out ChangeKind kind))
                lines.Add(new ChangeLine(kind, m.Groups["text"].Value.Trim()));
        }
        return lines;
    }

    [GeneratedRegex(@"^(?<type>[a-z]+)(\((?<scope>[^)]+)\))?!?:")]
    private static partial Regex Subject();

    [GeneratedRegex(@"^Changelog-(?<kind>Added|Changed|Fixed|Removed):\s*(?<text>.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex Trailer();
}
