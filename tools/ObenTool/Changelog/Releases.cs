namespace ObenTool.Changelog;

/// <summary>A version and the git range holding its commits.</summary>
public sealed record ReleaseRange(string Version, string Range, bool Released, DateOnly? TagDate);

/// <summary>
/// Splits history into releases: every vX.Y.Z tag closes a release, and commits after the last
/// tag are the Unreleased ones.
/// </summary>
public static class Releases
{
    /// <param name="releasing">
    /// A version being released right now, before its tag exists: the commits after the last tag
    /// become that release, dated today. Ignored when the tag already exists.
    /// </param>
    public static List<ReleaseRange> Resolve(string repo, string? releasing = null)
    {
        var tags = Git.Run(repo, "for-each-ref", "refs/tags/v*", "--format=%(refname:short)\u001f%(creatordate:short)")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split('\u001f'))
            .Select(p => (Tag: p[0], Version: ParseVersion(p[0][1..]), Date: DateOnly.Parse(p[1])))
            .Where(t => t.Version != null)
            .OrderBy(t => t.Version)
            .ToList();

        var releases = new List<ReleaseRange>();
        string? previous = null;
        foreach (var t in tags)
        {
            releases.Add(new ReleaseRange(t.Tag[1..], previous == null ? t.Tag : $"{previous}..{t.Tag}", true, t.Date));
            previous = t.Tag;
        }

        string range = previous == null ? "HEAD" : $"{previous}..HEAD";
        if (releasing == null)
            releases.Add(new ReleaseRange("Unreleased", range, false, null));
        else if (tags.All(t => t.Tag != "v" + releasing))
            releases.Add(new ReleaseRange(releasing, range, true, DateOnly.FromDateTime(DateTime.Now)));

        releases.Reverse(); // Newest first.
        return releases;
    }

    private static Version? ParseVersion(string text) => Version.TryParse(text, out var v) ? v : null;
}
