namespace ObenTool.Changelog;

/// <summary>A version and the git range holding its commits.</summary>
public sealed record ReleaseRange(string Version, string Range, bool Released, DateOnly? TagDate);

/// <summary>
/// Splits history into releases: every vX.Y.Z tag closes a release, and commits after the last
/// tag are the Unreleased ones. Before 1.0.0 a version may only have pre-release tags
/// (v0.1.0-pre.5+2e5d633, from before every release got a number): the newest of them closes that version.
/// After 1.0.0 such tags are test builds of a version still to come and close nothing.
/// </summary>
public static partial class Releases
{
    /// <param name="releasing">
    /// A version being released right now, before its tag exists: the commits after the last tag
    /// become that release, dated today. Ignored when the tag already exists.
    /// </param>
    public static List<ReleaseRange> Resolve(string repo, string? releasing = null)
    {
        var all = Git.Run(repo, "for-each-ref", "refs/tags/v*", "--format=%(refname:short)\u001f%(creatordate:short)")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split('\u001f'))
            .Select(p => (Tag: p[0], Date: DateOnly.Parse(p[1])))
            .ToList();
        var tags = all
            .Select(t => (t.Tag, Name: t.Tag[1..], Version: ParseVersion(t.Tag[1..]), t.Date))
            .Where(t => t.Version != null)
            .ToList();
        tags.AddRange(EarlyPreReleases(all, tags.Select(t => t.Version!).ToHashSet()));
        tags.Sort((a, b) => a.Version!.CompareTo(b.Version));

        var releases = new List<ReleaseRange>();
        string? previous = null;
        foreach (var t in tags)
        {
            releases.Add(new ReleaseRange(t.Name, previous == null ? t.Tag : $"{previous}..{t.Tag}", true, t.Date));
            previous = t.Tag;
        }

        string range = previous == null ? "HEAD" : $"{previous}..HEAD";
        if (releasing == null)
            releases.Add(new ReleaseRange("Unreleased", range, false, null));
        else if (tags.All(t => t.Name != releasing))
            releases.Add(new ReleaseRange(releasing, range, true, DateOnly.FromDateTime(DateTime.Now)));

        releases.Reverse(); // Newest first.
        return releases;
    }

    private static Version? ParseVersion(string text) => Version.TryParse(text, out var v) ? v : null;

    // The newest pre-release tag of each 0.x version that has no plain tag of its own.
    private static IEnumerable<(string Tag, string Name, Version? Version, DateOnly Date)> EarlyPreReleases(
        List<(string Tag, DateOnly Date)> all, HashSet<Version> plain)
    {
        return all
            .Select(t => (t.Tag, t.Date, Match: PreTag().Match(t.Tag)))
            .Where(t => t.Match.Success)
            .Select(t => (t.Tag, t.Date, Version: Version.Parse(t.Match.Groups["version"].Value), Number: int.Parse(t.Match.Groups["number"].Value)))
            .Where(t => t.Version.Major == 0 && !plain.Contains(t.Version))
            .GroupBy(t => t.Version)
            .Select(g => g.OrderByDescending(t => t.Number).First())
            .Select(t => (t.Tag, t.Version.ToString(3), (Version?)t.Version, t.Date));
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^v(?<version>\d+\.\d+\.\d+)-pre\.(?<number>\d+)(\+.*)?$")]
    private static partial System.Text.RegularExpressions.Regex PreTag();
}
