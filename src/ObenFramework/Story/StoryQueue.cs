// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Story;

/// <summary>
/// The ordered list behind <see cref="StoryPump"/>: actions wait here until the pump is allowed to run
/// them. Each entry remembers who queued it, so a mod's entries can be dropped when it unloads. Entries
/// queued while the queue runs wait for the next run, so a handler that queues more can't loop forever.
/// No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class StoryQueue
{
    /// <summary>More than this many waiting entries means something queues without the pump running; the oldest go.</summary>
    public const int MaxEntries = 256;

    private struct Entry
    {
        public object Owner;
        public string Name;
        public Action Action;
    }

    private readonly List<Entry> _entries = new List<Entry>(16);
    private readonly List<Entry> _running = new List<Entry>(16);

    public int Count => _entries.Count;

    /// <summary>How many entries were thrown away because the queue was full, since the last <see cref="Clear"/>.</summary>
    public int Dropped { get; private set; }

    public void Add(object owner, string name, Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));
        if (_entries.Count >= MaxEntries)
        {
            _entries.RemoveAt(0);
            Dropped++;
        }
        _entries.Add(new Entry { Owner = owner, Name = name, Action = action });
    }

    /// <summary>Removes everything <paramref name="owner"/> queued. Returns how many entries went.</summary>
    public int RemoveOwner(object owner) => _entries.RemoveAll(e => ReferenceEquals(e.Owner, owner));

    public void Clear()
    {
        _entries.Clear();
        Dropped = 0;
    }

    /// <summary>The names of the waiting entries, oldest first.</summary>
    public List<string> Names()
    {
        var names = new List<string>(_entries.Count);
        foreach (var entry in _entries)
            names.Add(entry.Name);
        return names;
    }

    /// <summary>
    /// Runs every entry that was waiting when the call began, oldest first. <paramref name="failed"/> is told
    /// about each one that throws; the rest still run. Returns how many ran.
    /// </summary>
    public int Run(Action<string, Exception> failed)
    {
        if (_entries.Count == 0)
            return 0;

        _running.Clear();
        _running.AddRange(_entries);
        _entries.Clear();
        for (int i = 0; i < _running.Count; i++)
        {
            try
            {
                _running[i].Action();
            }
            catch (Exception e)
            {
                failed?.Invoke(_running[i].Name, e);
            }
        }
        int ran = _running.Count;
        _running.Clear();
        return ran;
    }
}
