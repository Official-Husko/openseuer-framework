#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Oben.Reports;

/// <summary>
/// Puts a report together the same way in the game and in the tool: the summary (report.md) and system.json in front
/// of the files, and the redactor that goes with the options. Writing the zip is <see cref="ReportPackager"/>.
/// </summary>
public static class ReportAssembly
{
    /// <summary>The name a report zip gets: "ObenseuerExtended-report-20261030-012000.zip".</summary>
    public static string FileName(DateTime made) => $"ObenseuerExtended-report-{made:yyyyMMdd-HHmmss}.zip";

    /// <param name="system">The snapshot, already cut down to the options (<see cref="SystemSnapshot.For"/>).</param>
    /// <param name="files">The files to add (<see cref="ReportFiles"/>); ones that don't exist are left out quietly.</param>
    /// <param name="toJson">Writes the snapshot as JSON (the game and the tool use different JSON libraries).</param>
    /// <param name="summary">The summary as it goes into the zip, before redaction.</param>
    public static List<ReportEntry> Entries(SystemSnapshot system, ReportOptions options, IReadOnlyList<string> problems, int problemCount,
        IEnumerable<ReportEntry> files, string? save, DateTime made, Func<SystemSnapshot, string> toJson, out string summary, string? crash = null)
    {
        var all = files.Where(f => f.Source == null || f.Text != null || File.Exists(f.Source)).ToList();
        var listed = all.Where(f => !f.Name.StartsWith("save/", StringComparison.Ordinal)).Select(f => f.Name);
        summary = ReportSummary.Markdown(system, options, problems, problemCount, new[] { "report.md", "system.json" }.Concat(listed), save, made, crash);
        all.Insert(0, new ReportEntry { Name = "report.md", Text = summary });
        all.Insert(1, new ReportEntry { Name = "system.json", Text = toJson(system) });
        return all;
    }

    /// <summary>The redactor for the options, knowing <paramref name="userName"/> already; null when personal info stays.</summary>
    public static ReportRedactor? Redactor(ReportOptions options, string? userName)
    {
        if (!options.HidePersonal)
            return null;
        var redactor = new ReportRedactor();
        redactor.AddName(userName);
        return redactor;
    }

    private static readonly Regex Problem = new Regex(@"^\[\d\d:\d\d:\d\d\] (?:ERROR|CAUGHT) (?<title>.+)$", RegexOptions.Multiline);
    private static readonly Regex Count = new Regex(@"^(?<n>\d+) problems? this session", RegexOptions.Multiline);

    /// <summary>The problem titles in an errors.log, newest first, and how many there were (the file may list fewer).</summary>
    public static List<string> ProblemsFromErrorLog(string? text, out int count)
    {
        count = 0;
        var titles = new List<string>();
        if (string.IsNullOrEmpty(text))
            return titles;
        foreach (Match m in Problem.Matches(text!))
            titles.Add(m.Groups["title"].Value.TrimEnd('\r'));
        titles.Reverse();
        var total = Count.Match(text!);
        count = total.Success ? int.Parse(total.Groups["n"].Value) : titles.Count;
        return titles;
    }
}
