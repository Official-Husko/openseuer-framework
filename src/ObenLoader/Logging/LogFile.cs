using System;
using System.IO;
using System.Text;
using ObenLoader.Runtime;

namespace ObenLoader.Logging;

/// <summary>
/// Buffered log file. Writes are cheap; the buffer is flushed periodically by the host
/// and right away for errors, so a crash still leaves the useful lines on disk.
/// The previous session's file is kept as previous.log.
/// </summary>
internal sealed class LogFile : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new object();

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
            _writer.WriteLine(entry.ToString());
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
