// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DevBridge.Tracing;

/// <summary>One line of the trace: a call, a note that more calls were left out, or a marker (a scene loaded).</summary>
public sealed class TraceRecord
{
    public long Seq;
    public double Time;
    public int Frame;
    public string Kind;
    public string Method;
    public string Args;
    public string Result;
    public double Ms;
    public int Depth;
    public int Thread;
    public string From;
    public string Error;
    public long More;
}

/// <summary>Running totals for one traced method.</summary>
public sealed class TraceStats
{
    internal TraceStats(string method, double now)
    {
        Method = method;
        WindowStart = now;
    }

    public string Method { get; }
    public long Calls { get; internal set; }
    public long Errors { get; internal set; }
    public double TotalMs { get; internal set; }
    public double MaxMs { get; internal set; }

    internal int Shown;
    internal int ShownErrors;
    internal long Hidden;
    internal double WindowStart;

    // The newest call left out, formatted only when its window closes.
    internal ParameterInfo[] LastParameters;
    internal object[] LastArgs;
    internal bool LastVoid;
    internal object LastResult;
}

/// <summary>
/// What the tracer keeps: the newest records in a ring, and totals per method. A method's first calls in each
/// window (20 in 10 s by default) are kept in full; the rest are only counted, and one "more" record per
/// window says how many were left out and what the last one looked like, so a method called every frame
/// can't flood the ring. Thread-safe. No Unity types, so it is unit tested on its own.
/// </summary>
public sealed class TraceBook
{
    public const string CallKind = "call";
    public const string MoreKind = "more";
    public const string MarkKind = "mark";

    private readonly object _lock = new object();
    private readonly TraceRecord[] _ring;
    private readonly Dictionary<string, TraceStats> _stats = new Dictionary<string, TraceStats>(StringComparer.Ordinal);
    private long _next = 1;
    private int _count;

    public TraceBook(int capacity = 2000, int linesPerWindow = 20, double windowSeconds = 10d)
    {
        _ring = new TraceRecord[Math.Max(16, capacity)];
        LinesPerWindow = Math.Max(1, linesPerWindow);
        WindowSeconds = Math.Max(0.1d, windowSeconds);
    }

    public int LinesPerWindow { get; }
    public double WindowSeconds { get; }

    /// <summary>The sequence number the next record gets.</summary>
    public long NextSeq { get { lock (_lock) return _next; } }

    /// <summary>The stats entry for a method, made on first use. Keep it; it is what <see cref="Admit"/> takes.</summary>
    public TraceStats StatsFor(string method, double now)
    {
        lock (_lock)
        {
            if (!_stats.TryGetValue(method, out var stats))
                _stats[method] = stats = new TraceStats(method, now);
            return stats;
        }
    }

    /// <summary>True when the call is still among the first few of its window, so it is worth formatting.</summary>
    public bool WantsDetail(TraceStats stats, double now)
    {
        lock (_lock)
            return stats.Shown < LinesPerWindow || now - stats.WindowStart >= WindowSeconds;
    }

    /// <summary>
    /// Counts a finished call. Returns true when it should be kept in full (then format it and call
    /// <see cref="AddCall"/>); otherwise it is only counted and remembered, unformatted, as the newest call
    /// left out, for the window's "more" record. Calls that threw are kept past the window's lines, up to as many
    /// again, so a method that throws every frame can't flood the trace either.
    /// </summary>
    public bool Admit(TraceStats stats, double ms, bool failed, double now,
        ParameterInfo[] parameters = null, object[] args = null, bool isVoid = true, object result = null)
    {
        lock (_lock)
        {
            stats.Calls++;
            stats.TotalMs += ms;
            if (ms > stats.MaxMs)
                stats.MaxMs = ms;
            if (failed)
                stats.Errors++;
            if (now - stats.WindowStart >= WindowSeconds)
                CloseWindow(stats, now);
            if (failed ? stats.ShownErrors < LinesPerWindow : stats.Shown < LinesPerWindow)
            {
                stats.Shown++;
                if (failed)
                    stats.ShownErrors++;
                return true;
            }
            stats.Hidden++;
            stats.LastParameters = parameters;
            stats.LastArgs = args;
            stats.LastVoid = isVoid;
            stats.LastResult = result;
            return false;
        }
    }

    public void AddCall(TraceRecord record)
    {
        record.Kind ??= CallKind;
        lock (_lock)
            Push(record);
    }

    public void AddMark(string text, double now, int frame)
    {
        lock (_lock)
            Push(new TraceRecord { Kind = MarkKind, Method = text, Time = now, Frame = frame });
    }

    /// <summary>Writes the "more" records of every window that has run out. Call it now and then (once a second is plenty).</summary>
    public void Sweep(double now, bool all = false)
    {
        lock (_lock)
        {
            foreach (var stats in _stats.Values)
            {
                if (all || now - stats.WindowStart >= WindowSeconds)
                    CloseWindow(stats, now);
            }
        }
    }

    /// <summary>Records after <paramref name="after"/>, oldest first, at most <paramref name="max"/>, optionally only those whose method contains <paramref name="method"/>.</summary>
    public List<TraceRecord> Read(long after, int max, string method = null)
    {
        var list = new List<TraceRecord>();
        lock (_lock)
        {
            int start = (int)((_next - _count) % _ring.Length);
            for (int i = 0; i < _count && list.Count < max; i++)
            {
                var record = _ring[(start + i) % _ring.Length];
                if (record.Seq <= after)
                    continue;
                if (method != null && (record.Method == null || record.Method.IndexOf(method, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                list.Add(record);
            }
        }
        return list;
    }

    /// <summary>The methods called most, by count.</summary>
    public List<TraceStats> Top(int max)
    {
        List<TraceStats> all;
        lock (_lock)
            all = new List<TraceStats>(_stats.Values);
        all.RemoveAll(s => s.Calls == 0);
        all.Sort((a, b) => b.Calls.CompareTo(a.Calls));
        return all.Count > max ? all.GetRange(0, max) : all;
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_ring, 0, _ring.Length);
            _count = 0;
            _stats.Clear();
        }
    }

    private void CloseWindow(TraceStats stats, double now)
    {
        if (stats.Hidden > 0)
        {
            Push(new TraceRecord
            {
                Kind = MoreKind, Method = stats.Method, More = stats.Hidden, Time = now,
                Args = stats.LastParameters != null ? TraceFormat.Args(stats.LastParameters, stats.LastArgs) : null,
                Result = stats.LastParameters != null ? (stats.LastVoid ? "void" : TraceFormat.Value(stats.LastResult)) : null,
            });
        }
        stats.Hidden = 0;
        stats.Shown = 0;
        stats.ShownErrors = 0;
        stats.LastParameters = null;
        stats.LastArgs = null;
        stats.LastResult = null;
        stats.WindowStart = now;
    }

    private void Push(TraceRecord record)
    {
        record.Seq = _next;
        _ring[(int)(_next % _ring.Length)] = record;
        _next++;
        if (_count < _ring.Length)
            _count++;
    }
}
