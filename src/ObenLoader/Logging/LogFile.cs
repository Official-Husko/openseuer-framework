using System;
using System.IO;
using System.Text;
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

    private readonly StreamWriter _writer;
    private readonly object _lock = new object();
    private long _written;
    private bool _full;

    public LogFile(string path)
    {
        string previous = Path.Combine(Path.GetDirectoryName(path), "previous" + Instance.Suffix + ".log");
        try
        {
            if (File.Exists(path))
            {
                if (File.Exists(previous))
                    File.Delete(previous);
                File.Move(path, previous);
            }
        }
        catch (IOException)
        {
            // Another process may have it open; just overwrite.
        }

        _writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = false };
    }

    public void Write(in LogEntry entry)
    {
        lock (_lock)
        {
            if (_full)
                return;
            string line = entry.ToString();
            _written += line.Length + 1;
            if (_written > MaxChars)
            {
                // A runaway session would otherwise fill the disk. The in-game buffer and errors.log go on.
                _full = true;
                _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Warning] [ObenLoader] This log reached its size limit ({MaxChars / (1024 * 1024)} MB); nothing more is written to it this session. Recent lines are still in the game (oben logs) and errors in errors.log.");
                _writer.Flush();
                return;
            }
            _writer.WriteLine(line);
            if (entry.Level >= LogLevel.Error)
                _writer.Flush();
        }
    }

    public void Flush()
    {
        lock (_lock)
            _writer.Flush();
    }

    public void Dispose()
    {
        lock (_lock)
            _writer.Dispose();
    }
}
