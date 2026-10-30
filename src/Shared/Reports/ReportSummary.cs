#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Oben.Reports;

/// <summary>
/// report.md: what a bug report holds, in markdown that reads well pasted into a GitHub issue. Built from a snapshot
/// already cut down to what the player ticked (<see cref="SystemSnapshot.For"/>).
/// </summary>
public static class ReportSummary
{
    public const int MaxProblems = 10;

    /// <summary>The whole summary.</summary>
    /// <param name="problems">Problem titles, newest first; only the first <see cref="MaxProblems"/> are listed.</param>
    /// <param name="problemCount">How many problems the session had in all.</param>
    /// <param name="files">The files in the report, as named inside the zip.</param>
    /// <param name="save">Which save is in it ("Esko_Virtanen/Slot2"), or null.</param>
    /// <param name="crash">The crash folder in it ("Crash_2026-10-29_162427293"), or null.</param>
    public static string Markdown(SystemSnapshot s, ReportOptions options, IReadOnlyList<string> problems, int problemCount,
        IEnumerable<string> files, string? save, DateTime made, string? crash = null)
    {
        var sb = new StringBuilder();
        sb.Append("## ObenseuerExtended report\n\n| | |\n| --- | --- |\n");
        Row(sb, "ObenseuerExtended", s.Build);
        Row(sb, "Game", GameLine(s));
        Row(sb, "Game code (Assembly-CSharp.dll)", Code(s.GameCodeHash));
        Row(sb, "Game exe", Code(s.GameExeHash));
        Row(sb, "Operating system", s.OsLine());
        foreach (var (name, value) in SpecLines(s))
            Row(sb, name, value);
        Row(sb, "Language", s.Language);
        Row(sb, "Last crash", CrashTime(crash));
        Row(sb, "Made", made.ToString("yyyy-MM-dd HH:mm"));

        sb.Append("\n### Mods\n\n");
        if (s.Mods.Count == 0)
            sb.Append("None found.\n");
        foreach (var mod in s.Mods)
            sb.Append("- ").Append(mod.Id).Append(' ').Append(mod.Version).Append(mod.State == "Loaded" || mod.State == null ? "" : " (" + mod.State + ")").Append('\n');

        AppendBepInEx(sb, s.BepInEx);

        sb.Append("\n### Problems this session\n\n");
        if (problemCount == 0)
            sb.Append("None.\n");
        else
        {
            sb.Append(problemCount).Append(problemCount == 1 ? " problem" : " problems").Append(problemCount > problems.Count ? ", the newest:" : ":").Append('\n');
            foreach (string title in problems.Take(MaxProblems))
                sb.Append("- ").Append(OneLine(title)).Append('\n');
            sb.Append("\nThe whole list with stack traces is logs/errors.log.\n");
        }

        sb.Append("\n### In this report\n\n");
        foreach (string file in files)
            sb.Append("- ").Append(file).Append('\n');
        if (save != null)
            sb.Append("- the save ").Append(save).Append(" (in save/)\n");
        if (options.HidePersonal)
        {
            sb.Append("\nPersonal info is hidden: user names in folder paths, Steam ids and IP addresses are replaced with placeholders");
            sb.Append(crash == null ? ".\n" : ", except inside crash.dmp, which is binary.\n");
        }
        return sb.ToString();
    }

    /// <summary>"Obenseuer 0.5.13, Steam build 26179069".</summary>
    public static string GameLine(SystemSnapshot s) =>
        "Obenseuer " + (s.Game ?? "?") + (s.GameBuild == null ? "" : ", Steam build " + s.GameBuild);

    /// <summary>The processor, graphics, display and memory rows that are in the snapshot.</summary>
    public static List<(string Name, string Value)> SpecLines(SystemSnapshot s)
    {
        var lines = new List<(string, string)>();
        if (s.Cpu != null)
            lines.Add(("Processor", s.Cpu + (s.CpuCores > 0 ? $", {s.CpuCores} threads" : "") + (s.CpuMhz > 0 ? $", {s.CpuMhz} MHz" : "")));
        if (s.Gpu != null)
            lines.Add(("Graphics", s.Gpu + (s.GpuMemoryMb > 0 ? ", " + Size(s.GpuMemoryMb) : "") + (s.GpuApi == null ? "" : ", " + s.GpuApi)));
        if (s.Display != null)
            lines.Add(("Display", s.Display));
        if (s.RamMb > 0)
            lines.Add(("Memory", Size(s.RamMb)));
        return lines;
    }

    /// <summary>"BepInEx 5.4.23.2 with 3 plugins", or null when it isn't installed.</summary>
    public static string? BepInExLine(BepInExInfo? b) => b == null ? null
        : "BepInEx " + (b.Version ?? "(version unknown)") + " with " + b.Plugins.Count + (b.Plugins.Count == 1 ? " plugin" : " plugins")
          + (b.Patchers.Count > 0 ? " and " + b.Patchers.Count + (b.Patchers.Count == 1 ? " patcher" : " patchers") : "");

    private static void AppendBepInEx(StringBuilder sb, BepInExInfo? b)
    {
        if (b == null)
            return;
        sb.Append("\n### ").Append(BepInExLine(b)).Append("\n\n");
        sb.Append(b.Doorstop == null ? "Its doorstop proxy is missing, so it probably doesn't start.\n" : "Started through " + b.Doorstop + ".\n");
        foreach (var plugin in b.Plugins.Concat(b.Patchers))
        {
            sb.Append("- ").Append(plugin.File).Append(": ").Append(plugin.Name ?? "?").Append(' ').Append(plugin.Version ?? "");
            if (plugin.Guid != null)
                sb.Append(" (").Append(plugin.Guid).Append(')');
            if (plugin.Sha256 != null)
                sb.Append(", ").Append(plugin.Sha256);
            sb.Append('\n');
        }
    }

    private static void Row(StringBuilder sb, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            sb.Append("| ").Append(name).Append(" | ").Append(value!.Replace("|", "\\|")).Append(" |\n");
    }

    // Unity names a crash folder after the UTC time: Crash_2026-10-29_162427293 is "2026-10-29 16:24 UTC".
    private static string? CrashTime(string? crash)
    {
        if (crash == null)
            return null;
        var m = System.Text.RegularExpressions.Regex.Match(crash, @"^Crash_(\d{4}-\d\d-\d\d)_(\d\d)(\d\d)");
        return (m.Success ? m.Groups[1].Value + " " + m.Groups[2].Value + ":" + m.Groups[3].Value + " UTC" : crash) + ", its dump is in crash/";
    }

    private static string? Code(string? hash) => hash == null ? null : "`" + hash + "`";

    private static string Size(int mb) => mb >= 1024 ? $"{Math.Round(mb / 1024.0, 1):0.#} GB" : mb + " MB";

    private static string OneLine(string text)
    {
        int end = text.IndexOf('\n');
        return (end < 0 ? text : text.Substring(0, end)).Trim();
    }
}
