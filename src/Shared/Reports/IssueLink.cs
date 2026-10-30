#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oben.Reports;

/// <summary>
/// The address of a new bug report on GitHub with the versions and the PC already filled in. GitHub fills an issue
/// form's fields from query parameters named after the field ids, so these names have to match
/// .github/ISSUE_TEMPLATE/bug_report.yml.
/// </summary>
public static class IssueLink
{
    public const string Repository = "https://github.com/Official-Husko/openseuer-framework";
    public const string Template = "bug_report.yml";

    /// <summary>A new bug report filled from <paramref name="s"/>, which should already be cut down to what the player ticked.</summary>
    public static string Bug(SystemSnapshot s)
    {
        var fields = new List<(string Key, string? Value)>
        {
            ("template", Template),
            ("mod-version", s.Build),
            ("game-version", ReportSummary.GameLine(s)),
            ("os", s.OsLine()),
            ("specs", string.Join("\n", ReportSummary.SpecLines(s).Select(l => l.Name + ": " + l.Value))),
            ("bepinex", ReportSummary.BepInExLine(s.BepInEx)),
        };
        return Repository + "/issues/new?" + string.Join("&", fields
            .Where(f => !string.IsNullOrEmpty(f.Value))
            .Select(f => f.Key + "=" + Uri.EscapeDataString(f.Value!)));
    }
}
