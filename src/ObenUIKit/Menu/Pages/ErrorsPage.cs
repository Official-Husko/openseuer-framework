using System.Collections.Generic;
using ObenLoader.Logging;
using ObenUIKit.Controls;
using ObenUIKit.Menu.Errors;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Menu.Pages;

/// <summary>
/// What went wrong this session, from the loader's error report: newest problem first, with who
/// reported it, how often, when and where. Errors nobody caught are red, ones a mod caught and reported
/// amber; stack traces are colored (see <see cref="StackText"/>), the first lines shown and the rest a
/// button away. The same report is written to ObenLoader/logs/errors.log.
/// </summary>
internal static class ErrorsPage
{
    public const string Title = "Errors";

    private const int MaxShown = 40;
    private const int DetailLines = 8;
    private const int MaxLineLength = 300;
    private const float RebuildEvery = 2f;

    // Problems whose whole stack trace is open. They are the report's own objects, gone after Clear.
    private static readonly HashSet<ErrorReport.Problem> Expanded = new HashSet<ErrorReport.Problem>();

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

        var colors = Colors();
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
                Expanded.Clear();
                ui.Window.Rebuild();
            });
        });
        status = ui.Label("", muted: true);
        ui.Space(10f);

        var counts = new List<CountLine>();
        for (int i = 0; i < problems.Count && i < MaxShown; i++)
            counts.Add(BuildProblem(ui, problems[i], colors));
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

    private static CountLine BuildProblem(UiBuilder ui, ErrorReport.Problem problem, StackColors colors)
    {
        string color = problem.Caught ? colors.Caught : colors.Error;
        ui.Header(KitText.Paint(color, problem.Source) + (problem.Caught ? KitText.Paint(colors.Muted, "  (caught)") : ""));
        ui.Label(StackText.Title(problem.Title, color, colors));
        var line = new CountLine { Problem = problem, Shown = problem.Count };
        line.Label = ui.Label(Meta(problem), muted: true);

        var lines = StackText.Lines(problem.Details);
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length > MaxLineLength)
                lines[i] = lines[i].Substring(0, MaxLineLength) + "...";
        }
        bool open = Expanded.Contains(problem);
        int shown = open ? lines.Count : Mathf.Min(lines.Count, DetailLines);
        for (int i = 0; i < shown; i++)
            ui.Label(StackText.Line(lines[i], color, colors), align: TextAlignmentOptions.Left);
        if (lines.Count > DetailLines)
        {
            ui.Button(open ? "Show less" : $"Show all {lines.Count} lines", () =>
            {
                if (!Expanded.Remove(problem))
                    Expanded.Add(problem);
                ui.Window.Rebuild();
            });
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

    // The text color comes from the theme (already in the linear space the game renders in); the rest are
    // picked as sRGB and converted, or they'd come out too bright. Muted is darker than the theme's muted
    // labels, so library frames and namespaces step back from the mod's own code.
    private static StackColors Colors()
    {
        var theme = UiKit.Theme;
        return new StackColors
        {
            Error = Hex(new Color(0.93f, 0.33f, 0.3f).linear),
            Caught = Hex(new Color(0.92f, 0.68f, 0.3f).linear),
            Muted = Hex(new Color(0.55f, 0.55f, 0.55f).linear),
            Text = Hex(theme.Text),
            Method = Hex(new Color(0.93f, 0.85f, 0.58f).linear),
            File = Hex(new Color(0.6f, 0.8f, 0.58f).linear),
            Number = Hex(new Color(0.6f, 0.76f, 0.93f).linear),
        };
    }

    private static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
