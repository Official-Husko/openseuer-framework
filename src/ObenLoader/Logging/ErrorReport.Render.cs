using System;
using System.Text;
using ObenLoader.Mods;
using UnityEngine;

namespace ObenLoader.Logging;

public static partial class ErrorReport
{
    // Callers hold Lock.
    private static string Render()
    {
        var sb = new StringBuilder();
        sb.Append("ObenseuerExtended error report\n");
        sb.Append("Written ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
        sb.Append("Game ").Append(Application.productName).Append(' ').Append(Loader.GameBuild ?? Application.version)
          .Append(", exe ").Append(Runtime.GameFiles.Short(Loader.GameExeHash) ?? "unreadable")
          .Append(", Unity ").Append(Application.unityVersion)
          .Append(", ObenLoader ").Append(Loader.Version)
          .Append(", ").Append(SystemInfo.operatingSystem).Append('\n');
        AppendMods(sb);
        sb.Append("\n\n");

        int total = Entries.Count + _notListed;
        sb.Append(total).Append(total == 1 ? " problem" : " problems").Append(" this session.\n");
        foreach (var entry in Entries)
            AppendProblem(sb, entry);
        if (_notListed > 0)
            sb.Append("\n...and ").Append(_notListed).Append(" more not listed.\n");
        return sb.ToString();
    }

    private static void AppendMods(StringBuilder sb)
    {
        sb.Append("Mods:");
        bool any = false;
        foreach (var mod in ModRegistry.Mods)
        {
            sb.Append(any ? ", " : " ").Append(mod.Id).Append(' ').Append(mod.Manifest.Version);
            if (mod.State != ModState.Loaded)
                sb.Append(" (").Append(mod.State).Append(')');
            any = true;
        }
        if (!any)
            sb.Append(" none");
    }

    private static void AppendProblem(StringBuilder sb, Problem entry)
    {
        sb.Append("\n[").Append(entry.First.ToString("HH:mm:ss")).Append("] ").Append(entry.Caught ? "CAUGHT " : "ERROR ")
          .Append(entry.Source).Append(": ").Append(entry.Title).Append('\n');
        sb.Append("  ").Append(entry.Count == 1 ? "Once" : entry.Count + " times");
        if (entry.Count > 1)
            sb.Append(", last at ").Append(entry.Last.ToString("HH:mm:ss"));
        if (!string.IsNullOrEmpty(entry.Scene))
            sb.Append(", in ").Append(entry.Scene);
        sb.Append(".\n");
        if (string.IsNullOrEmpty(entry.Details))
            return;
        foreach (string line in entry.Details.Split('\n'))
            sb.Append("  ").Append(line.TrimEnd()).Append('\n');
    }

    private static string Trim(string details)
    {
        if (string.IsNullOrEmpty(details))
            return null;
        var lines = details.TrimEnd().Split('\n');
        if (lines.Length <= MaxDetailLines)
            return string.Join("\n", lines);
        return string.Join("\n", lines, 0, MaxDetailLines) + $"\n(+{lines.Length - MaxDetailLines} more lines)";
    }

    private static string FirstLines(string text, int count)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        int end = -1;
        for (int i = 0; i < count; i++)
        {
            end = text.IndexOf('\n', end + 1);
            if (end < 0)
                return text;
        }
        return text.Substring(0, end);
    }
}
