using System;
using ObenFramework.Diagnostics.Reports;
using ObenUIKit.Controls;
using Oben.Reports;
using UnityEngine;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// Report a problem: makes a bug report zip (<see cref="BugReports"/>) with what the player ticks, then opens the
/// folder it is in, copies its summary, or opens a GitHub bug report with the versions already filled in.
/// </summary>
internal static class ReportPage
{
    public const string Title = "Report";

    // Kept while the game runs, so coming back to the page shows the same ticks.
    private static readonly ReportOptions Options = new ReportOptions();

    public static ModPage Create() => new ModPage(Title, Build) { Order = 35 };

    private static void Build(UiBuilder ui)
    {
        ui.Label("Found a bug? Make a report here and attach it to your bug report on GitHub.");
        ui.Label("It always holds the game and mod versions, your operating system, the logs and the mods' settings.", muted: true);
        ui.Space(10f);

        ui.Header("Also add");
        ui.Toggle("Processor", Options.Cpu, on => Options.Cpu = on);
        ui.Toggle("Graphics card and screen", Options.Gpu, on => Options.Gpu = on);
        ui.Toggle("Memory", Options.Ram, on => Options.Ram = on);
        ui.Toggle("The save you're playing", Options.Save, on => Options.Save = on);
        ui.Tooltip("Every file of the loaded save, or of your newest one at the main menu. Bugs in a save are much easier to find with it.");
        ui.Toggle("The last crash", Options.Crash, on => Options.Crash = on);
        ui.Tooltip("If the game crashed in the last week: the crash dump and the log Unity kept. The dump is binary, so names in it can't be hidden.");
        ui.Toggle("Hide personal info", Options.HidePersonal, on => Options.HidePersonal = on);
        ui.Tooltip("Your user name in folder paths, Steam ids and IP addresses are replaced with placeholders.");
        ui.Space(10f);

        LabelHandle status = null;
        ui.Button("Make report", () =>
        {
            bool started = BugReports.Make(Copy(Options), report =>
            {
                // The page may have been closed or rebuilt meanwhile.
                if (status != null && status.Alive)
                    status.Text = report == null ? "That didn't work; the Errors page says why." : Describe(report);
            });
            status.Text = started ? "Making the report..." : "A report is being made already.";
        });
        status = ui.Label(BugReports.Last == null ? "" : Describe(BugReports.Last), muted: true);
        ui.Row(r =>
        {
            r.Button("Open folder", BugReports.OpenFolder);
            r.Tooltip("Shows ObenLoader/reports in your file manager.");
            r.Button("Copy summary", () =>
            {
                if (BugReports.Last == null)
                {
                    status.Text = "Make a report first.";
                    return;
                }
                GUIUtility.systemCopyBuffer = BugReports.Last.Summary;
                status.Text = "Copied. Paste it into your bug report.";
            });
            r.Tooltip("The versions, your system and what's in the report, ready to paste.");
        });
        ui.Button("Report on GitHub", () => BugReports.OpenIssue());
        ui.Tooltip("Opens a new bug report in your browser with the versions and the ticked parts filled in. Drag the zip into it.");
    }

    private static string Describe(BugReport report)
    {
        string text = $"Saved {System.IO.Path.GetFileName(report.Path)} ({Size(report.Bytes)})";
        if (report.Save != null)
            text += " with the save " + report.Save;
        if (report.TooBigForGitHub)
            text += ". It's too big for GitHub (25 MB at most); leave the save out or share it another way.";
        return text + ".";
    }

    private static string Size(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / (1024f * 1024f):0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";

    private static ReportOptions Copy(ReportOptions o) =>
        new ReportOptions { Cpu = o.Cpu, Gpu = o.Gpu, Ram = o.Ram, Save = o.Save, Crash = o.Crash, HidePersonal = o.HidePersonal };
}
