// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ObenLoader.Logging;

/// <summary>
/// One log file that copes with the disk hiccuping: when a write fails it lets go of the file and tries
/// again (appending) <see cref="RetryMs"/> later, instead of going quiet for the rest of the session. The
/// first failure is kept for the hub to report once. Not thread-safe; the owner locks around it.
/// </summary>
internal sealed class LogWriter : IDisposable
{
    public const long RetryMs = 5000;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private readonly string _path;
    private readonly long _retryMs;
    private StreamWriter _writer;
    private long _retryAt;
    private bool _reported;
    private string _failure;

    public LogWriter(string path, bool append, long retryMs = RetryMs)
    {
        _path = path;
        _retryMs = retryMs;
        Open(append);
    }

    /// <summary>Characters in the current file (about bytes: the lines are almost all ASCII).</summary>
    public long Length { get; private set; }

    /// <summary>Writes one line; false when the file can't be written right now (the line is lost).</summary>
    public bool WriteLine(string line, bool flush)
    {
        if (_writer == null)
        {
            if (Clock.ElapsedMilliseconds < _retryAt)
                return false;
            Open(append: true);
            if (_writer == null)
                return false;
        }
        try
        {
            _writer.WriteLine(line);
            Length += line.Length + 1;
            if (flush)
                _writer.Flush();
            return true;
        }
        catch (Exception e) when (IsFileProblem(e))
        {
            Fail(e);
            return false;
        }
    }

    public void Flush()
    {
        if (_writer == null)
            return;
        try { _writer.Flush(); }
        catch (Exception e) when (IsFileProblem(e)) { Fail(e); }
    }

    /// <summary>Moves the file to <paramref name="previousPath"/> and starts an empty one.</summary>
    public void StartOver(string previousPath)
    {
        Close();
        Rotate(_path, previousPath);
        Open(append: false);
    }

    /// <summary>The first failure as a line for the log, once; null when there is nothing (new) to report.</summary>
    public string TakeFailure()
    {
        string failure = _failure;
        _failure = null;
        return failure;
    }

    public void Dispose() => Close();

    /// <summary>Moves <paramref name="path"/> to <paramref name="previousPath"/>, replacing it. Leaves both alone when it can't.</summary>
    public static void Rotate(string path, string previousPath)
    {
        try
        {
            if (!File.Exists(path))
                return;
            if (File.Exists(previousPath))
                File.Delete(previousPath);
            File.Move(path, previousPath);
        }
        catch (Exception e) when (IsFileProblem(e))
        {
            // Another process may have it open; the file is then written over or appended to.
        }
    }

    private void Open(bool append)
    {
        try
        {
            _writer = new StreamWriter(_path, append, new UTF8Encoding(false)) { AutoFlush = false };
            Length = append ? _writer.BaseStream.Length : 0;
        }
        catch (Exception e) when (IsFileProblem(e))
        {
            Fail(e);
        }
    }

    private void Close()
    {
        try { _writer?.Dispose(); }
        catch (Exception e) when (IsFileProblem(e)) { }
        _writer = null;
    }

    private void Fail(Exception e)
    {
        Close();
        _retryAt = Clock.ElapsedMilliseconds + _retryMs;
        if (_reported)
            return;
        _reported = true;
        _failure = $"{Path.GetFileName(_path)} couldn't be written ({e.Message}); trying again every {RetryMs / 1000} s. Recent lines are still in the game (oben logs).";
    }

    private static bool IsFileProblem(Exception e) =>
        e is IOException || e is UnauthorizedAccessException || e is ObjectDisposedException || e is NotSupportedException;
}
