using System;
using System.Diagnostics;

namespace ObenLoader.Logging;

/// <summary>
/// Named logger. Each mod gets one tagged with its id (a new one per load, so a hot reload starts with a
/// clean slate). It writes at most <see cref="LineLimit"/> lines a second; the rest are dropped and
/// counted in one note, and dropped errors still count in errors.log.
/// </summary>
public sealed partial class ModLogger
{
    // One steady clock for every logger; reading it is safe from any thread.
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private readonly LineLimiter _limiter = new LineLimiter();
    private int _reportPending;

    public string Source { get; }

    public ModLogger(string source)
    {
        Source = source;
    }

    /// <summary>
    /// Lines per second this logger writes before it starts dropping them (default
    /// <see cref="LineLimiter.DefaultPerSecond"/>, at most <see cref="LineLimiter.MaxPerSecond"/>).
    /// </summary>
    public int LineLimit
    {
        get => _limiter.PerSecond;
        set => _limiter.PerSecond = value;
    }

    /// <summary>Lines dropped by the limit since this logger was made.</summary>
    public long DroppedLines => _limiter.TotalDropped;

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warning, message);
    public void Error(string message) => Write(LogLevel.Error, message);
    public void Error(string message, Exception e) => Write(LogLevel.Error, e == null ? message : message + "\n" + e);

    private void Write(LogLevel level, string message)
    {
        if (!_limiter.TryTake(Clock.ElapsedMilliseconds, out int droppedBefore))
        {
            if (level == LogLevel.Error)
                LogHub.CountError(Source, message);
            if (System.Threading.Interlocked.Exchange(ref _reportPending, 1) == 0)
                LogHub.WatchDrops(this);
            return;
        }
        if (droppedBefore > 0)
            WriteDropNote(droppedBefore);
        LogHub.Write(level, Source, message);
    }

    /// <summary>Called by the hub about once a second while lines are being dropped. True when nothing is left to report.</summary>
    internal bool ReportDrops()
    {
        int dropped = _limiter.TakeFinishedDrops(Clock.ElapsedMilliseconds);
        if (dropped > 0)
            WriteDropNote(dropped);
        if (_limiter.PendingDrops > 0)
            return false;
        System.Threading.Interlocked.Exchange(ref _reportPending, 0);
        return true;
    }

    private void WriteDropNote(int dropped) =>
        LogHub.Write(LogLevel.Warning, Source,
            $"{dropped} log line{(dropped == 1 ? " was" : "s were")} dropped: this log takes at most {LineLimit} lines a second (raise Log.LineLimit to allow more).");
}
