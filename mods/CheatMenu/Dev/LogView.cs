using System;
using System.Collections.Generic;
using ObenLoader.Logging;

namespace CheatMenu.Dev;

/// <summary>
/// What the developer menu's Log part lists: the lines of the loader's log buffer (mods, the loader and
/// Unity, the last few thousand of this session) that pass a level, a source and a text filter, oldest
/// first, taken in as they come. No Unity types, so it is unit tested on its own.
/// </summary>
public sealed class LogView
{
    /// <summary>Most lines kept; the oldest go first.</summary>
    public const int MaxLines = 4096;

    /// <summary>How long a row's text gets before it is cut.</summary>
    public const int RowLength = 220;

    private readonly List<LogEntry> _lines = new List<LogEntry>();
    private long _last = -1;

    /// <summary>The lowest level shown.</summary>
    public LogLevel MinLevel { get; set; } = LogLevel.Info;

    /// <summary>Only lines from this source (a mod id, "Unity"); empty for every source.</summary>
    public string Source { get; set; } = "";

    /// <summary>Only lines with this text in their source or message, any case; empty for all.</summary>
    public string Filter { get; set; } = "";

    public IReadOnlyList<LogEntry> Lines => _lines;

    /// <summary>Forgets what was taken in, so the next <see cref="Pull"/> checks every buffered line again.</summary>
    public void Reset()
    {
        _lines.Clear();
        _last = -1;
    }

    /// <summary>Takes in the lines logged since the last call that pass the filters. Returns how many.</summary>
    public int Pull(LogBuffer buffer)
    {
        long upTo = buffer.LastSequence;
        if (upTo <= _last)
            return 0;
        var fresh = buffer.Read(_last, MaxLines, MinLevel);
        int added = 0;
        foreach (var entry in fresh)
        {
            if (!Matches(entry))
                continue;
            _lines.Add(entry);
            added++;
        }
        // Lines below the level aren't in the result, so the buffer's own last number is what was seen.
        _last = Math.Max(upTo, fresh.Count > 0 ? fresh[fresh.Count - 1].Sequence : -1);
        if (_lines.Count > MaxLines)
            _lines.RemoveRange(0, _lines.Count - MaxLines);
        return added;
    }

    public bool Matches(LogEntry entry)
    {
        if (entry.Level < MinLevel)
            return false;
        if (!string.IsNullOrEmpty(Source) && !string.Equals(entry.Source, Source, StringComparison.OrdinalIgnoreCase))
            return false;
        return DevRules.Matches(entry.Message, Filter) || DevRules.Matches(entry.Source, Filter);
    }

    /// <summary>Where the line with this sequence number is in <see cref="Lines"/>, or -1.</summary>
    public int IndexOf(long sequence)
    {
        int low = 0, high = _lines.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            long at = _lines[middle].Sequence;
            if (at == sequence)
                return middle;
            if (at < sequence)
                low = middle + 1;
            else
                high = middle - 1;
        }
        return -1;
    }

    /// <summary>The sources in the buffer right now, sorted, for picking one.</summary>
    public static List<string> Sources(LogBuffer buffer)
    {
        var names = new List<string>();
        foreach (var entry in buffer.Read(-1, MaxLines))
        {
            if (!string.IsNullOrEmpty(entry.Source) && !names.Contains(entry.Source))
                names.Add(entry.Source);
        }
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    /// <summary>One line for a list row: time, source and the first line of the message, cut.</summary>
    public static string Row(LogEntry entry)
    {
        string message = entry.Message ?? "";
        int end = message.IndexOf('\n');
        string first = end < 0 ? message : message.Substring(0, end).TrimEnd('\r');
        return DevRules.Cut($"{entry.Time:HH:mm:ss} {entry.Source}: {first}", RowLength);
    }

    /// <summary>How many more lines the message has after its first, for the row's right side.</summary>
    public static int ExtraLines(LogEntry entry)
    {
        string message = (entry.Message ?? "").TrimEnd('\r', '\n');
        int count = 0;
        for (int i = message.IndexOf('\n'); i >= 0; i = message.IndexOf('\n', i + 1))
            count++;
        return count;
    }
}
