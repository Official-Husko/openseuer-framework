using System;
using System.Collections.Generic;

namespace ObenLoader.Logging;

/// <summary>Fixed-size, thread-safe ring buffer of recent log entries.</summary>
public sealed class LogBuffer
{
    private readonly LogEntry[] _items;
    private readonly object _lock = new object();
    private long _next;

    public LogBuffer(int capacity)
    {
        _items = new LogEntry[capacity];
    }

    public long LastSequence
    {
        get { lock (_lock) return _next - 1; }
    }

    /// <summary>Adds a line and returns its sequence number.</summary>
    public long Add(LogLevel level, string source, string message)
    {
        lock (_lock)
        {
            long sequence = _next++;
            _items[sequence % _items.Length] = new LogEntry(sequence, DateTime.Now, level, source, message);
            return sequence;
        }
    }

    /// <summary>Copies entries newer than <paramref name="afterSequence"/> (at most <paramref name="max"/>, newest kept).</summary>
    public List<LogEntry> Read(long afterSequence, int max, LogLevel minLevel = LogLevel.Debug)
    {
        var result = new List<LogEntry>(Math.Min(max, 64));
        lock (_lock)
        {
            long oldest = Math.Max(0, _next - _items.Length);
            long start = Math.Max(oldest, afterSequence + 1);
            for (long i = _next - 1; i >= start && result.Count < max; i--)
            {
                var entry = _items[i % _items.Length];
                if (entry.Level >= minLevel)
                    result.Add(entry);
            }
        }
        result.Reverse();
        return result;
    }
}
