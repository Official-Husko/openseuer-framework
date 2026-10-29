using System;
using System.IO;
using ObenLoader.Runtime;

namespace ObenLoader.Logging;

/// <summary>
/// Buffered log file. Writes are cheap; the buffer is flushed periodically by the host
/// and right away for errors, so a crash still leaves the useful lines on disk.
/// The previous session's file is kept as previous.log. It stops growing at 64 MB.
/// </summary>
internal sealed class LogFile : IDisposable
{
    // Characters, roughly bytes: the lines are almost all ASCII.
    private const long MaxChars = 64L * 1024 * 1024;

    private readonly LogWriter _writer;
    private readonly object _lock = new object();
    private bool _full;

    public LogFile(string path)
    {
        LogWriter.Rotate(path, Path.Combine(Path.GetDirectoryName(path), "previous" + Instance.Suffix + ".log"));
        _writer = new LogWriter(path, append: false);
    }

    public void Write(in LogEntry entry)
    {
        lock (_lock)
        {
            if (_full)
                return;
            string line = entry.ToString();
            if (_writer.Length + line.Length + 1 > MaxChars)
            {
                // A runaway session would otherwise fill the disk. The in-game buffer and errors.log go on.
                _full = true;
                _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Warning] [ObenLoader] This log reached its size limit ({MaxChars / (1024 * 1024)} MB); nothing more is written to it this session. Recent lines are still in the game (oben logs) and errors in errors.log.", flush: true);
                return;
            }
            _writer.WriteLine(line, entry.Level >= LogLevel.Error);
        }
    }

    public void Flush()
    {
        lock (_lock)
            _writer.Flush();
    }

    /// <summary>The first write failure, once, for the hub to report.</summary>
    public string TakeFailure()
    {
        lock (_lock)
            return _writer.TakeFailure();
    }

    public void Dispose()
    {
        lock (_lock)
            _writer.Dispose();
    }
}
