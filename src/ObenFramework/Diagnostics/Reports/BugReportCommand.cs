using System;
using System.Linq;
using System.Text;
using ObenFramework.Console;
using ObenLoader;
using Oben.Reports;

namespace ObenFramework.Diagnostics.Reports;

/// <summary>"report" in the F1 console, for players and for programs that drive the console.</summary>
internal static class BugReportCommand
{
    private const string Usage = "report [cpu] [gpu] [ram] [save] [all] [no-crash] [show-personal] | report folder | report issue";

    public static void Register(ObenMod framework) => GameConsole.Register(framework, "report", Usage, Run);

    private static string Run(string[] args)
    {
        var words = args.Select(a => a.ToLowerInvariant()).ToList();
        if (words.Contains("folder"))
        {
            BugReports.OpenFolder();
            return "Opened " + BugReports.Folder;
        }
        if (words.Contains("issue"))
        {
            BugReports.OpenIssue();
            return BugReports.Last == null ? "No report made yet; opened an empty bug report." : "Opened a bug report filled from " + BugReports.Last.Path;
        }

        bool all = words.Contains("all");
        var options = new ReportOptions
        {
            Cpu = all || words.Contains("cpu"),
            Gpu = all || words.Contains("gpu"),
            Ram = all || words.Contains("ram"),
            Save = all || words.Contains("save"),
            Crash = !words.Contains("no-crash"),
            HidePersonal = !words.Contains("show-personal"),
        };
        string unknown = string.Join(" ", words.Where(w => !new[] { "cpu", "gpu", "ram", "save", "all", "no-crash", "show-personal" }.Contains(w)));
        if (unknown.Length > 0)
            return "Unknown: " + unknown + ". Usage: " + Usage;

        if (!BugReports.Make(options, report => GameConsole.Print(report == null ? "The report failed; the log says why." : Describe(report, options))))
            return "A report is being made already.";
        return "Making the report...";
    }

    private static string Describe(BugReport report, ReportOptions options)
    {
        var sb = new StringBuilder();
        sb.Append("Made ").Append(report.Path).Append(" (").Append(Math.Max(1, report.Bytes / 1024)).Append(" KB)");
        if (report.Save != null)
            sb.Append(" with the save ").Append(report.Save);
        if (options.Save && report.Save == null)
            sb.Append(", no save found to add");
        foreach (string note in report.Notes)
            sb.Append("\n  ").Append(note);
        if (report.TooBigForGitHub)
            sb.Append("\nIt's over GitHub's 25 MB limit for attachments; leave the save out or share it another way.");
        sb.Append("\n\"report folder\" opens the folder, \"report issue\" a bug report on GitHub with the versions filled in.");
        return sb.ToString();
    }
}
