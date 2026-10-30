// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DevBridge.Tracing;

/// <summary>
/// The trace as plain text, the way the reference's debug log read: one line per record with the time and
/// frame, calls indented by how deep they were nested, the thread when it wasn't the main one, the mod that
/// called; marks set apart, and the most called methods at the end. Written to logs/trace.log when a trace
/// stops. No Unity types, so it is unit tested on its own.
/// </summary>
public static class TraceText
{
    /// <summary>Nesting deeper than this is drawn at this depth, so a runaway recursion stays readable.</summary>
    public const int MaxIndent = 20;

    public static string Line(TraceRecord record)
    {
        var line = new StringBuilder(160);
        line.Append(record.Time.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(9))
            .Append(" f").Append(record.Frame).Append("  ");
        switch (record.Kind)
        {
            case TraceBook.MarkKind:
                line.Append("--- ").Append(record.Method);
                break;
            case TraceBook.MoreKind:
                line.Append(record.Method).Append(": ").Append(record.More).Append(record.More == 1 ? " more call" : " more calls");
                if (record.Args != null)
                    line.Append(", the last (").Append(record.Args).Append(") -> ").Append(record.Result);
                break;
            default:
                line.Append(' ', Math.Max(0, Math.Min(record.Depth, MaxIndent)) * 2)
                    .Append(record.Method).Append('(').Append(record.Args).Append(')');
                if (record.Error != null)
                    line.Append(" threw ").Append(record.Error);
                else
                    line.Append(" -> ").Append(record.Result);
                line.Append(" (").Append(record.Ms.ToString("0.000", CultureInfo.InvariantCulture)).Append(" ms)");
                if (record.Thread != 0)
                    line.Append(" [thread ").Append(record.Thread).Append(']');
                if (record.From != null)
                    line.Append(" from ").Append(record.From);
                break;
        }
        return line.ToString();
    }

    /// <summary>The whole trace: a heading, every record, then the most called methods.</summary>
    public static void Write(TextWriter writer, string heading, IEnumerable<TraceRecord> records, IEnumerable<TraceStats> top)
    {
        writer.WriteLine(heading);
        writer.WriteLine();
        foreach (var record in records)
            writer.WriteLine(Line(record));
        writer.WriteLine();
        writer.WriteLine("Most called:");
        foreach (var stats in top)
        {
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture, "  {0} x{1}, {2:0.0000} ms on average, {3:0.000} ms at most{4}",
                stats.Method, stats.Calls, stats.Calls == 0 ? 0d : stats.TotalMs / stats.Calls, stats.MaxMs,
                stats.Errors > 0 ? $", {stats.Errors} threw" : ""));
        }
    }
}

/// <summary>
/// How many log lines a second the trace copies in (the reference capped its debug log at 1000 lines a
/// second); the rest are counted and the count goes into the trace as a mark. Thread-safe.
/// </summary>
public sealed class LineGate
{
    private readonly object _lock = new object();
    private readonly int _perSecond;
    private double _windowStart = double.MinValue;
    private int _taken;
    private long _dropped;

    public LineGate(int perSecond) => _perSecond = Math.Max(1, perSecond);

    /// <summary>True while the second that <paramref name="now"/> falls in still has room.</summary>
    public bool Take(double now)
    {
        lock (_lock)
        {
            if (now - _windowStart >= 1d)
            {
                _windowStart = now;
                _taken = 0;
            }
            if (_taken < _perSecond)
            {
                _taken++;
                return true;
            }
            _dropped++;
            return false;
        }
    }

    /// <summary>The lines left out since the last call.</summary>
    public long TakeDropped()
    {
        lock (_lock)
        {
            long dropped = _dropped;
            _dropped = 0;
            return dropped;
        }
    }
}
