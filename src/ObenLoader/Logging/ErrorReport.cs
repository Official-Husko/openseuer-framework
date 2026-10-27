using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ObenLoader.Runtime;
using UnityEngine.SceneManagement;

namespace ObenLoader.Logging;

/// <summary>
/// errors.log: a short report of what went wrong this session, meant to be attached to a bug report.
/// Every error logged by the game or a mod is in it once, with how often it happened, plus problems
/// mods report on purpose (<see cref="Add(string,string,string)"/>), under a header with the game,
/// loader and mod versions. The previous session's report is kept as errors-previous.log. No errors,
/// no file.
/// </summary>
public static partial class ErrorReport
{
    private const int MaxEntries = 200;
    private const int MaxDetailLines = 40;

    /// <summary>One problem in the report. The count and last time go up while the game runs.</summary>
    public sealed class Problem
    {
        public string Source { get; internal set; }
        public string Title { get; internal set; }
        public string Details { get; internal set; }
        public string Scene { get; internal set; }
        public DateTime First { get; internal set; }
        public DateTime Last { get; internal set; }
        public int Count { get; internal set; }

        /// <summary>True when a mod caught and reported it; false for errors that were logged (crashes, exceptions).</summary>
        public bool Caught { get; internal set; }
    }

    private static readonly object Lock = new object();
    private static readonly List<Problem> Entries = new List<Problem>();
    private static readonly Dictionary<string, Problem> ByKey = new Dictionary<string, Problem>();
    private static int _notListed;
    private static bool _dirty;
    private static string _path;

    /// <summary>Where the report is written.</summary>
    public static string FilePath => _path;

    /// <summary>How many different problems were reported this session.</summary>
    public static int Count
    {
        get
        {
            lock (Lock)
                return Entries.Count + _notListed;
        }
    }

    /// <summary>Goes up whenever a new problem is added or the report is cleared (not when a count goes up).</summary>
    public static int Version { get; private set; }

    /// <summary>The problems so far, oldest first. The list is a copy; the problems are live.</summary>
    public static List<Problem> Snapshot()
    {
        lock (Lock)
            return new List<Problem>(Entries);
    }

    /// <summary>The report as text, for copying.</summary>
    public static string Text()
    {
        lock (Lock)
            return Render();
    }

    /// <summary>Empties the report (and errors.log), for example after it was sent.</summary>
    public static void Clear()
    {
        lock (Lock)
        {
            Entries.Clear();
            ByKey.Clear();
            _notListed = 0;
            _dirty = false;
            Version++;
        }
        try
        {
            if (_path != null && File.Exists(_path))
                File.Delete(_path);
        }
        catch (IOException) { /* Rewritten on the next problem anyway. */ }
    }

    /// <summary>
    /// Adds a problem a mod caught to the report. <paramref name="title"/> is one readable line ("Quest
    /// reward failed to load"); <paramref name="details"/> is anything that helps find the cause.
    /// The same problem again only raises its count.
    /// </summary>
    public static void Add(string source, string title, string details = null) => Add(source, title, details, caught: true);

    /// <summary>Same as <see cref="Add(string,string,string)"/> with the exception and its stack added to the details.</summary>
    public static void Add(string source, string title, Exception exception, string details = null) =>
        Add(source, title, details == null ? exception?.ToString() : details + "\n" + exception);

    internal static void Init(string logsDir)
    {
        _path = Path.Combine(logsDir, "errors" + Instance.Suffix + ".log");
        string previous = Path.Combine(logsDir, "errors-previous" + Instance.Suffix + ".log");
        try
        {
            if (File.Exists(previous))
                File.Delete(previous);
            if (File.Exists(_path))
                File.Move(_path, previous);
        }
        catch (IOException)
        {
            // Kept open by another program; it's overwritten on the first error instead.
        }
        LogHub.ErrorLogged += Capture;
    }

    /// <summary>Writes what is left and stops listening. Called when the game closes.</summary>
    internal static void Shutdown()
    {
        LogHub.ErrorLogged -= Capture;
        Flush();
    }

    /// <summary>Rewrites the file if anything changed. The host calls this about once a second.</summary>
    internal static void Flush()
    {
        string text;
        lock (Lock)
        {
            if (!_dirty || _path == null)
                return;
            _dirty = false;
            text = Render();
        }
        try { File.WriteAllText(_path, text, new UTF8Encoding(false)); }
        catch (IOException) { /* Try again on the next flush. */ }
    }

    // Fed by LogHub.ErrorLogged, which fires before Unity's repeat filter so every occurrence counts.
    // The first line of the message is the title.
    private static void Capture(string source, string message)
    {
        if (string.IsNullOrEmpty(message))
            return;
        int newline = message.IndexOf('\n');
        Add(source, newline < 0 ? message : message.Substring(0, newline), newline < 0 ? null : message.Substring(newline + 1), caught: false);
    }

    private static void Add(string source, string title, string details, bool caught)
    {
        string scene = SafeSceneName();
        lock (Lock)
        {
            string key = source + "\n" + title + "\n" + FirstLines(details, 3);
            if (ByKey.TryGetValue(key, out var entry))
            {
                entry.Count++;
                entry.Last = DateTime.Now;
                _dirty = true;
                return;
            }
            if (Entries.Count >= MaxEntries)
            {
                _notListed++;
                _dirty = true;
                return;
            }
            entry = new Problem
            {
                Caught = caught,
                Source = source,
                Title = title,
                Details = Trim(details),
                Scene = scene,
                First = DateTime.Now,
                Last = DateTime.Now,
                Count = 1,
            };
            Entries.Add(entry);
            ByKey[key] = entry;
            _dirty = true;
            Version++;
        }
    }

    // Unity's scene API only works on the main thread; errors can come from others.
    private static string SafeSceneName()
    {
        try { return MainThread.IsCurrent ? SceneManager.GetActiveScene().name : null; }
        catch (Exception) { return null; }
    }
}
