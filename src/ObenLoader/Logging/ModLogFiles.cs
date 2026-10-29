// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ObenLoader.Mods;

namespace ObenLoader.Logging;

/// <summary>
/// A file per mod next to latest.log: logs/mods/&lt;id&gt;.log holds that mod's own lines with frame numbers
/// (to line them up with an API trace), and &lt;id&gt;.previous.log the previous session's, or the earlier part
/// of a long one once a file passes <see cref="RotateChars"/>. Unity's own lines stay in latest.log and
/// Player.log. A mod author can ask a player for just their mod's file. Thread-safe.
/// </summary>
internal sealed class ModLogFiles : IDisposable
{
    public const long RotateChars = 4L * 1024 * 1024;
    private const int MaxFiles = 64;
    private const int MaxNameLength = 64;
    private const string InvalidChars = "<>:\"/\\|?*";

    private readonly string _dir;
    private readonly string _suffix;
    private readonly object _lock = new object();
    private readonly Dictionary<string, LogWriter> _files = new Dictionary<string, LogWriter>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _names = new Dictionary<string, string>(StringComparer.Ordinal);
    private bool _closed;

    public ModLogFiles(string dir, string suffix)
    {
        _dir = dir;
        _suffix = suffix ?? "";
    }

    public void Write(in LogEntry entry, int frame)
    {
        if (string.IsNullOrEmpty(entry.Source) || entry.Source == "Unity")
            return;
        lock (_lock)
        {
            if (!_names.TryGetValue(entry.Source, out string name))
            {
                name = FileName(entry.Source);
                if (_names.Count < MaxFiles * 4)
                    _names[entry.Source] = name;
            }
            var file = For(name);
            if (file == null)
                return;
            if (file.Length > RotateChars)
                file.StartOver(PathFor(name, previous: true));
            file.WriteLine(Line(entry, frame), entry.Level >= LogLevel.Error);
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            foreach (var file in _files.Values)
                file.Flush();
        }
    }

    /// <summary>The first new write failure of any file, for the hub to report.</summary>
    public string TakeFailure()
    {
        lock (_lock)
        {
            foreach (var file in _files.Values)
            {
                string failure = file.TakeFailure();
                if (failure != null)
                    return failure;
            }
            return null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _closed = true;
            foreach (var file in _files.Values)
                file.Dispose();
            _files.Clear();
        }
    }

    /// <summary>"[12:00:01.250] [frame 812] [Warning] message".</summary>
    public static string Line(in LogEntry entry, int frame) => $"[{entry.Time:HH:mm:ss.fff}] [frame {frame}] [{entry.Level}] {entry.Message}";

    /// <summary>
    /// A file name for a log source: characters Windows refuses become '_', trailing dots and spaces go,
    /// device names ("con", "nul") get a '_' in front, and nothing longer than 64 characters.
    /// </summary>
    public static string FileName(string source)
    {
        var text = new StringBuilder();
        foreach (char c in source ?? "")
        {
            if (text.Length == MaxNameLength)
                break;
            text.Append(c < 32 || InvalidChars.IndexOf(c) >= 0 ? '_' : c);
        }
        string name = text.ToString().Trim().TrimEnd('.', ' ');
        if (name.Length == 0)
            return "unnamed";
        return ModIds.IsReservedName(name) ? "_" + name : name;
    }

    // By file name, so two sources that clean up to the same name share one writer.
    private LogWriter For(string name)
    {
        if (_closed)
            return null;
        if (_files.TryGetValue(name, out var file))
            return file;
        if (_files.Count >= MaxFiles)
            return null;
        try
        {
            Directory.CreateDirectory(_dir);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return null;
        }
        string path = PathFor(name, previous: false);
        LogWriter.Rotate(path, PathFor(name, previous: true));
        file = new LogWriter(path, append: false);
        _files[name] = file;
        return file;
    }

    private string PathFor(string name, bool previous) =>
        Path.Combine(_dir, name + (previous ? ".previous" : "") + _suffix + ".log");
}
