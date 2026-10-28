using System.Collections.Generic;
using ObenLoader.Logging;
using ObenUIKit.Controls;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// What went wrong this session, from the loader's error report: newest problem first, with who
/// reported it, how often, when and where. The same report is written to ObenLoader/logs/errors.log.
/// </summary>
internal static class ErrorsPage
{
    public const string Title = "Errors";

    private const int MaxShown = 40;
    private const int DetailLines = 3;
    private const float RebuildEvery = 2f;

    /// <summary>The line under a problem that changes while it keeps happening.</summary>
    private sealed class CountLine
    {
        public ErrorReport.Problem Problem;
        public LabelHandle Label;
        public int Shown;
    }

    public static ModPage Create() => new ModPage(Title, Build) { Order = 30 };

    private static void Build(UiBuilder ui)
    {
        int version = ErrorReport.Version;
        var problems = ErrorReport.Snapshot();
        problems.Reverse();

        ui.Label(problems.Count == 0 ? "Nothing has gone wrong this session." : Plural(problems.Count, "problem") + " this session.");
        ui.Label("Bug report? Attach ObenLoader/logs/errors.log. It holds this list with the game and mod versions.", muted: true);

        LabelHandle status = null;
        ui.Row(r =>
        {
            r.Button("Copy report", () =>
            {
                GUIUtility.systemCopyBuffer = ErrorReport.Text();
                status.Text = "Copied. Paste it into your bug report.";
            });
            r.Button("Clear", () =>
            {
                ErrorReport.Clear();
                ui.Window.Rebuild();
            });
        });
        status = ui.Label("", muted: true);
        ui.Space(10f);

        var counts = new List<CountLine>();
        for (int i = 0; i < problems.Count && i < MaxShown; i++)
            counts.Add(BuildProblem(ui, problems[i]));
        if (problems.Count > MaxShown)
            ui.Label($"{problems.Count - MaxShown} older problems are only in the log file.", muted: true);

        float built = Time.unscaledTime;
        ui.Window.AddRefresher(() =>
        {
            // A new problem rebuilds the list (not more than every couple of seconds, so a burst of
            // errors doesn't keep throwing the scroll position back); repeats only change a count.
            if (ErrorReport.Version != version)
            {
                if (Time.unscaledTime - built > RebuildEvery)
                    ui.Window.Rebuild();
                return;
            }
            foreach (var line in counts)
            {
                if (line.Problem.Count != line.Shown)
                {
                    line.Shown = line.Problem.Count;
                    line.Label.Text = Meta(line.Problem);
                }
            }
        });
    }

    private static CountLine BuildProblem(UiBuilder ui, ErrorReport.Problem problem)
    {
        ui.Header(problem.Source + (problem.Caught ? "  (caught)" : ""));
        ui.Label(Verbatim(problem.Title));
        var line = new CountLine { Problem = problem, Shown = problem.Count };
        line.Label = ui.Label(Meta(problem), muted: true);

        int shown = 0;
        foreach (string raw in (problem.Details ?? "").Split('\n'))
        {
            string text = raw.Trim();
            if (text.Length == 0)
                continue;
            ui.Label(Verbatim(text.Length > 120 ? text.Substring(0, 120) + "..." : text), muted: true, align: TextAlignmentOptions.Left);
            if (++shown == DetailLines)
                break;
        }
        ui.Space(14f);
        return line;
    }

    private static string Meta(ErrorReport.Problem problem)
    {
        string when = problem.Count == 1
            ? "Once at " + problem.First.ToString("HH:mm:ss")
            : $"{problem.Count} times, last at {problem.Last:HH:mm:ss}";
        return string.IsNullOrEmpty(problem.Scene) ? when + "." : $"{when}, in {problem.Scene}.";
    }

    // Stack traces and generic types are full of angle brackets the text renderer would take as tags.
    private static string Verbatim(string text) => "<noparse>" + text.Replace("</noparse>", "") + "</noparse>";

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
