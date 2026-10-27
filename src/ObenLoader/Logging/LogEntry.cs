using System;

namespace ObenLoader.Logging;

/// <summary>One captured log line. Sequence numbers only go up, so readers can ask for "everything after N".</summary>
public readonly struct LogEntry
{
    public readonly long Sequence;
    public readonly DateTime Time;
    public readonly LogLevel Level;
    public readonly string Source;
    public readonly string Message;

    public LogEntry(long sequence, DateTime time, LogLevel level, string source, string message)
    {
        Sequence = sequence;
        Time = time;
        Level = level;
        Source = source;
        Message = message;
    }

    public override string ToString() => $"[{Time:HH:mm:ss.fff}] [{Level}] [{Source}] {Message}";
}
