// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Logging;
using ObenUIKit.Controls;
using TMPro;
using UnityEngine;

namespace CheatMenu.Dev;

/// <summary>
/// The log in game, from the loader's buffer of this session's lines (mods, the loader and Unity): pick
/// a level and a source, filter by text, and the list follows new lines as they come. Picking a line shows
/// all of it, stack trace included, with a button to copy it. The reference read the tail of its log files;
/// the buffer has every source in one place, debug lines included even when the files leave them out.
/// </summary>
internal static class LogSection
{
    private const int Rows = 12;
    private const int MaxShown = 3000;

    private static readonly string[] Levels = { "Everything", "Info and up", "Warnings and errors", "Errors only" };
    private static readonly LogLevel[] LevelValues = { LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error };
    private static readonly LogView View = new LogView();
    private static bool _follow = true;
    private static long _picked = -1;

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Log");
        ui.Label($"The last {LogView.MaxLines} lines of this session from mods, the loader and Unity. The files are in {LoaderPaths.LogsDir}: latest.log, errors.log and mods/(mod id).log.", muted: true);
        ui.Cycle("Show", Levels, System.Array.IndexOf(LevelValues, View.MinLevel), i =>
        {
            View.MinLevel = LevelValues[i];
            ui.Window.Rebuild();
        });
        var sources = new List<string> { "Everyone" };
        sources.AddRange(LogView.Sources(LogHub.Buffer));
        int source = System.Math.Max(0, sources.FindIndex(s => string.Equals(s, View.Source, System.StringComparison.OrdinalIgnoreCase)));
        ui.Cycle("From", sources.ToArray(), source, i =>
        {
            View.Source = i == 0 ? "" : sources[i];
            ui.Window.Rebuild();
        });
        ui.TextField("Lines with (Enter applies)", View.Filter, t => { }, t =>
        {
            View.Filter = (t ?? "").Trim();
            ui.Window.Rebuild();
        });
        ui.Toggle("Follow new lines", _follow, on => _follow = on);

        View.Reset();
        View.Pull(LogHub.Buffer);
        var colors = new RowColors();
        ListHandle list = null;
        list = ui.List(View.Lines.Count, (i, row) => Bind(i, row, colors), i => _picked = View.Lines[i].Sequence, Rows);
        if (_follow && View.Lines.Count > 0)
            list.ScrollTo(View.Lines.Count - 1);
        ui.Live(() => View.Lines.Count == 0 ? "No line matches." : $"{View.Lines.Count} lines.", muted: true);
        ui.Poll(() =>
        {
            if (View.Pull(LogHub.Buffer) == 0)
                return;
            list.SetCount(View.Lines.Count);
            if (_follow)
                list.ScrollTo(View.Lines.Count - 1);
        });

        ui.Header("The picked line");
        var picked = ui.Label(Picked(), align: TextAlignmentOptions.TopLeft);
        long shown = _picked;
        ui.Poll(() =>
        {
            if (_picked == shown)
                return;
            shown = _picked;
            picked.Text = Picked();
        });
        ui.Button("Copy it", () =>
        {
            int index = View.IndexOf(_picked);
            if (index < 0)
            {
                ctx.Say("Pick a line in the list first.");
                return;
            }
            GUIUtility.systemCopyBuffer = View.Lines[index].ToString();
            ctx.Say("Copied the line.");
        });
    }

    private static void Bind(int index, ListRow row, RowColors colors)
    {
        var entry = View.Lines[index];
        string text = LogView.Row(entry);
        row.Text = entry.Level switch
        {
            LogLevel.Error => UiText.Paint(colors.Error, text),
            LogLevel.Warning => UiText.Paint(colors.Warning, text),
            _ => UiText.Plain(text),
        };
        row.Muted = entry.Level == LogLevel.Debug;
        int extra = LogView.ExtraLines(entry);
        row.Detail = extra > 0 ? $"+{extra} lines" : "";
    }

    private static string Picked()
    {
        int index = View.IndexOf(_picked);
        if (index < 0)
            return UiText.Plain("Pick a line in the list to see all of it.");
        return UiText.Plain(DevRules.Cut(View.Lines[index].ToString(), MaxShown));
    }

    // The Errors page's reds and ambers, so a warning here looks like a caught error there.
    private sealed class RowColors
    {
        public readonly Color Error = new Color(0.93f, 0.33f, 0.3f);
        public readonly Color Warning = new Color(0.92f, 0.68f, 0.3f);
    }
}
