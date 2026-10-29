using System.Text;
using System.Text.Json.Nodes;

namespace ObenTool.Changelog;

/// <summary>Markdown release notes for one version, from the same data the game shows.</summary>
public static class ReleaseNotes
{
    private static readonly (string Key, string Title)[] Sections =
    {
        ("added", "Added"), ("changed", "Changed"), ("fixed", "Fixed"), ("removed", "Removed"),
    };

    public static string Markdown(JsonObject changelog, string version)
    {
        var release = changelog["releases"]?.AsArray()
            .OfType<JsonObject>()
            .FirstOrDefault(r => r["version"]?.GetValue<string>() == version)
            ?? throw new InvalidOperationException($"The changelog has no lines for version {version}.");
        return Markdown(release);
    }

    /// <summary>The sections of one release entry (<see cref="ChangelogBuilder.Entry"/>).</summary>
    public static string Markdown(JsonObject release)
    {
        var sb = new StringBuilder();
        foreach (var (key, title) in Sections)
        {
            var lines = release[key]?.AsArray().OfType<JsonObject>().ToList();
            if (lines == null || lines.Count == 0)
                continue;
            sb.Append("### ").Append(title).Append("\n\n");
            foreach (var line in lines)
            {
                string? area = line["area"]?.GetValue<string>();
                sb.Append("- ");
                if (area != null)
                    sb.Append("**").Append(area).Append(":** ");
                sb.Append(line["text"]!.GetValue<string>()).Append('\n');
            }
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd() + "\n";
    }
}
