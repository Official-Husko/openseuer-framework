using System;
using System.Diagnostics;

namespace ObenLoader.Logging;

/// <summary>
/// Named logger. Each mod gets one tagged with its id (a new one per load, so a hot reload starts with a
/// clean slate). It writes at most <see cref="LineLimit"/> lines a second; the rest are dropped and
/// counted in one note. Errors are also kept readable when something fails over and over: the same error
/// once in 10 seconds and at most 20 errors in 10 seconds (<see cref="ErrorThrottle"/>), again with a
/// note, unless verbose logging is on. Dropped and held back errors still count in errors.log.
/// </summary>
public sealed partial class ModLogger
{
    // One steady clock for every logger; reading it is safe from any thread.
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private readonly LineLimiter _limiter = new LineLimiter();
    private readonly ErrorThrottle _errors = new ErrorThrottle();
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

    /// <summary>Errors held back since this logger was made (repeats and bursts, see the class summary).</summary>
    public long HeldBackErrors => _errors.TotalHeldBack;

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Info, message);
    public void Warn(string message) => Write(LogLevel.Warning, message);
    public void Error(string message) => Write(LogLevel.Error, message, "message " + message);

    // The same exception from the same place counts as a repeat, whatever its message says.
    public void Error(string message, Exception e)
    {
        if (e == null)
            Error(message);
        else
            Write(LogLevel.Error, message + "\n" + e, "exception " + e.GetType().FullName + " " + FirstFrame(e));
    }

    private void Write(LogLevel level, string message, string errorKey = null)
    {
        long now = Clock.ElapsedMilliseconds;
        if (errorKey != null && !LogHub.Verbose)
        {
            if (!_errors.TryWrite(errorKey, now, out int heldBefore))
            {
                LogHub.CountError(Source, message);
                LogHub.CountHeldBack();
                WatchForNotes();
                return;
            }
            if (heldBefore > 0)
                WriteHeldBackNote(heldBefore);
        }
        if (!_limiter.TryTake(now, out int droppedBefore))
        {
            if (level == LogLevel.Error)
                LogHub.CountError(Source, message);
            LogHub.CountDropped();
            WatchForNotes();
            return;
        }
        if (droppedBefore > 0)
            WriteDropNote(droppedBefore);
        LogHub.Write(level, Source, message);
    }

    /// <summary>
    /// Called by the hub about once a second while lines are dropped or errors held back, and at the quit
    /// with <paramref name="all"/> set. True when nothing is left to report.
    /// </summary>
    internal bool ReportDrops(bool all = false)
    {
        // A time past every window makes the limiters hand over what the running one holds too.
        long now = Clock.ElapsedMilliseconds + (all ? 60_000 : 0);
        int dropped = _limiter.TakeFinishedDrops(now);
        if (dropped > 0)
            WriteDropNote(dropped);
        int held = _errors.TakeFinishedHeldBack(now);
        if (held > 0)
            WriteHeldBackNote(held);
        if (_limiter.PendingDrops > 0 || _errors.PendingHeldBack > 0)
            return false;
        System.Threading.Interlocked.Exchange(ref _reportPending, 0);
        return true;
    }

    private void WatchForNotes()
    {
        if (System.Threading.Interlocked.Exchange(ref _reportPending, 1) == 0)
            LogHub.WatchDrops(this);
    }

    private void WriteDropNote(int dropped) =>
        LogHub.Write(LogLevel.Warning, Source,
            $"{dropped} log line{(dropped == 1 ? " was" : "s were")} dropped: this log takes at most {LineLimit} lines a second (raise Log.LineLimit to allow more).");

    private void WriteHeldBackNote(int held) =>
        LogHub.Write(LogLevel.Warning, Source,
            $"{held} error{(held == 1 ? " was" : "s were")} held back: the same error is written once in {ErrorThrottle.RepeatMs / 1000} seconds and at most {ErrorThrottle.MaxPerWindow} errors in {ErrorThrottle.WindowMs / 1000} seconds. All of them count in errors.log; VerboseLog writes every one.");

    private static string FirstFrame(Exception e)
    {
        string trace = e.StackTrace ?? "";
        int end = trace.IndexOf('\n');
        return (end < 0 ? trace : trace.Substring(0, end)).Trim();
    }
}
