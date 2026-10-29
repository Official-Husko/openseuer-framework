using System.Text.Json.Nodes;

namespace ObenTool.Changelog;

/// <summary>Builds the changelog.json document from git history.</summary>
public static class ChangelogBuilder
{
    private static readonly ChangeKind[] Sections = Enum.GetValues<ChangeKind>();

    /// <param name="releasing">A version being released right now; see <see cref="Releases.Resolve"/>.</param>
    public static JsonObject Build(string repo, string? releasing = null)
    {
        var releases = new JsonArray();
        foreach (var release in Releases.Resolve(repo, releasing))
        {
            var commits = CommitLog.Read(repo, release.Range);
            // Releases without anything for players (only tooling or docs commits) are left out.
            if (commits.All(c => c.Lines.Count == 0))
                continue;

            var entry = new JsonObject
            {
                ["version"] = release.Version,
                ["date"] = (release.TagDate ?? commits.Max(c => c.Date)).ToString("yyyy-MM-dd"),
                ["released"] = release.Released,
            };
            foreach (var kind in Sections)
                entry[kind.ToString().ToLowerInvariant()] = Lines(commits, kind);
            releases.Add(entry);
        }

        return new JsonObject { ["releases"] = releases };
    }

    // A mod's lines stay together, the mods in alphabetical order and lines without a mod last, so every
    // section lists the mods the same way. Inside a mod the lines keep the order of the commits.
    private static JsonArray Lines(List<CommitEntry> commits, ChangeKind kind)
    {
        var items = Collect(commits, kind);
        return new JsonArray(items
            .GroupBy(item => item["area"]?.GetValue<string>())
            .OrderBy(area => area.Key == null)
            .ThenBy(area => area.Key, StringComparer.OrdinalIgnoreCase)
            .SelectMany(area => area)
            .ToArray<JsonNode?>());
    }

    private static List<JsonObject> Collect(List<CommitEntry> commits, ChangeKind kind)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var array = new List<JsonObject>();
        foreach (var commit in commits)
        {
            string? area = ModAreas.NameFor(commit.Scope);
            foreach (var line in commit.Lines)
            {
                if (line.Kind != kind || !seen.Add(line.Text))
                    continue;
                var item = new JsonObject { ["text"] = WithoutArea(line.Text, area) };
                if (area != null)
                    item["area"] = area;
                array.Add(item);
            }
        }
        return array;
    }

    /// <summary>
    /// A new mod's line often starts with its own name ("Dev bridge: ...", "Cheat Menu mod: ..."). The area
    /// is shown in front of every line already, so the name is dropped there.
    /// </summary>
    private static string WithoutArea(string text, string? area)
    {
        if (area == null)
            return text;
        foreach (string prefix in new[] { area + ":", area + " mod:" })
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            string rest = text[prefix.Length..].TrimStart();
            return rest.Length == 0 ? text : char.ToUpperInvariant(rest[0]) + rest[1..];
        }
        return text;
    }
}
