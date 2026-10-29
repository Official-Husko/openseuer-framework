using System;
using System.Collections.Generic;
using System.IO;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenLoader.Logging;

/// <summary>
/// Central log sink. Every <see cref="ModLogger"/> writes here; the hub fans out to the
/// ring buffer, the log file and Unity's own console. Unity's messages come back in too, so
/// the buffer holds everything the game said.
/// </summary>
public static class LogHub
{
    [ThreadStatic] private static bool _forwardingToUnity;

    private static LogFile _file;
    private static ModLogFiles _modFiles;
    private static long _droppedLines;
    private static long _heldBackErrors;

    // Unity repeats some warnings hundreds of times in a row; only the first copy is kept.
    private static readonly object RepeatLock = new object();
    private static string _lastUnityMessage;
    private static int _repeats;

    // Loggers that dropped lines over their rate limit and still owe a note about it.
    private static readonly HashSet<ModLogger> Dropping = new HashSet<ModLogger>();
    private static readonly List<ModLogger> DropsToReport = new List<ModLogger>();

    /// <summary>Recent lines from mods, the loader and Unity itself.</summary>
    public static LogBuffer Buffer { get; } = new LogBuffer(4096);

    /// <summary>Lines below this level stay out of latest.log and the mod files (they're still in the buffer).</summary>
    public static LogLevel MinFileLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Verbose logging (VerboseLog in ObenLoader/settings.json): debug lines reach the files, and every error
    /// and once-only warning is written each time instead of being held back.
    /// </summary>
    public static bool Verbose => MinFileLevel <= LogLevel.Debug;

    /// <summary>The frame number the mod files stamp on each line, copied once a frame by the loader.</summary>
    public static int Frame { get; internal set; }

    /// <summary>Lines every logger dropped over its line limit this session.</summary>
    public static long DroppedLines => System.Threading.Interlocked.Read(ref _droppedLines);

    /// <summary>Errors held back this session because the same one came again or too many came at once.</summary>
    public static long HeldBackErrors => System.Threading.Interlocked.Read(ref _heldBackErrors);

    /// <summary>Raised for every error line, from mods and from Unity, before repeats are filtered.</summary>
    public static event Action<string, string> ErrorLogged;

    /// <summary>
    /// Raised for every line a mod or the loader writes, with its level, source and text, on the thread that
    /// logged it. The API tracer mirrors the log into its trace with it.
    /// </summary>
    public static event Action<LogLevel, string, string> LineWritten;

    internal static void Init(string logsDir)
    {
        _file = new LogFile(Path.Combine(logsDir, "latest" + Instance.Suffix + ".log"));
        _modFiles = new ModLogFiles(Path.Combine(logsDir, "mods"), Instance.Suffix);
        ErrorReport.Init(logsDir);
        Application.logMessageReceivedThreaded += OnUnityLog;
    }

    /// <summary>Stops the file per mod (ModLogFiles in ObenLoader/settings.json); latest.log keeps every line.</summary>
    internal static void StopModFiles()
    {
        var files = _modFiles;
        _modFiles = null;
        files?.Dispose();
    }

    internal static void Flush()
    {
        ReportDrops(all: false);
        _file?.Flush();
        _modFiles?.Flush();
        ErrorReport.Flush();
        ReportFailure(_file?.TakeFailure());
        ReportFailure(_modFiles?.TakeFailure());
    }

    /// <summary>Counts an error line a logger dropped (rate limit, log-once) in errors.log without writing it.</summary>
    internal static void CountError(string source, string message) => RaiseError(source, message);

    internal static void CountDropped() => System.Threading.Interlocked.Increment(ref _droppedLines);

    internal static void CountHeldBack() => System.Threading.Interlocked.Increment(ref _heldBackErrors);

    /// <summary>A logger started dropping lines; its note is written on the next flush if it goes quiet.</summary>
    internal static void WatchDrops(ModLogger logger)
    {
        lock (Dropping)
            Dropping.Add(logger);
    }

    // Main thread only (the host's flush, and the quit). Held under the lock so a logger that starts dropping
    // again while its note is written is watched again rather than lost. At the quit every note is written,
    // also for a second that isn't over yet.
    private static void ReportDrops(bool all)
    {
        lock (Dropping)
        {
            if (Dropping.Count == 0)
                return;
            DropsToReport.AddRange(Dropping);
            try
            {
                foreach (var logger in DropsToReport)
                {
                    if (logger.ReportDrops(all))
                        Dropping.Remove(logger);
                }
            }
            finally
            {
                DropsToReport.Clear();
            }
        }
    }

    private static void ReportFailure(string text)
    {
        if (text != null)
            Write(LogLevel.Warning, "ObenLoader", text);
    }

    internal static void Shutdown()
    {
        ReportDrops(all: true);
        Application.logMessageReceivedThreaded -= OnUnityLog;
        ErrorReport.Shutdown();
        _file?.Dispose();
        _file = null;
        StopModFiles();
    }

    internal static void Write(LogLevel level, string source, string message)
    {
        long sequence = Buffer.Add(level, source, message);
        if (level == LogLevel.Error)
            RaiseError(source, message);
        if (level >= MinFileLevel)
        {
            var entry = new LogEntry(sequence, DateTime.Now, level, source, message);
            _file?.Write(entry);
            _modFiles?.Write(entry, Frame);
        }
        RaiseLine(level, source, message);

        _forwardingToUnity = true;
        try
        {
            string line = "[" + source + "] " + message;
            switch (level)
            {
                case LogLevel.Error: Debug.LogError(line); break;
                case LogLevel.Warning: Debug.LogWarning(line); break;
                default: Debug.Log(line); break;
            }
        }
        finally
        {
            _forwardingToUnity = false;
        }
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        if (_forwardingToUnity)
            return;

        LogLevel level = type switch
        {
            LogType.Error or LogType.Exception or LogType.Assert => LogLevel.Error,
            LogType.Warning => LogLevel.Warning,
            _ => LogLevel.Debug,
        };

        string message = level == LogLevel.Error && !string.IsNullOrEmpty(stackTrace)
            ? condition + "\n" + stackTrace.TrimEnd()
            : condition;

        if (level == LogLevel.Error)
            RaiseError("Unity", message);

        if (IsRepeat(message, out int previousRepeats))
            return;
        if (previousRepeats > 0)
            Buffer.Add(LogLevel.Debug, "Unity", $"(previous message repeated {previousRepeats} more times)");

        long sequence = Buffer.Add(level, "Unity", message);
        if (level >= LogLevel.Warning)
            _file?.Write(new LogEntry(sequence, DateTime.Now, level, "Unity", message));
    }

    private static void RaiseError(string source, string message)
    {
        try { ErrorLogged?.Invoke(source, message); }
        catch { /* A listener failing must never break logging. */ }
    }

    private static void RaiseLine(LogLevel level, string source, string message)
    {
        var listeners = LineWritten;
        if (listeners == null)
            return;
        try { listeners(level, source, message); }
        catch { /* A listener failing must never break logging. */ }
    }

    private static bool IsRepeat(string message, out int previousRepeats)
    {
        lock (RepeatLock)
        {
            if (message == _lastUnityMessage)
            {
                _repeats++;
                previousRepeats = 0;
                return true;
            }
            previousRepeats = _repeats;
            _repeats = 0;
            _lastUnityMessage = message;
            return false;
        }
    }
}
