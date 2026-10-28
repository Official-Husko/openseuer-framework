// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Clock;

/// <summary>
/// Actions waiting for a number of in-game minutes, counted down on the game's minute tick. Each entry
/// remembers its owner so everything an owner scheduled can go at once. A tick doesn't allocate. Plain
/// bookkeeping, tested on its own.
/// </summary>
internal sealed class MinuteCountdowns
{
    internal sealed class Entry
    {
        public object Owner;
        public int Left;
        public Action Action;
        public bool Done;
    }

    private readonly List<Entry> _entries = new List<Entry>();
    private readonly List<Entry> _due = new List<Entry>();

    public int Count => _entries.Count;

    public Entry Add(object owner, int minutes, Action action)
    {
        if (minutes < 1)
            throw new ArgumentOutOfRangeException(nameof(minutes), "Wait at least one minute.");
        var entry = new Entry { Owner = owner, Left = minutes, Action = action ?? throw new ArgumentNullException(nameof(action)) };
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Drops one entry. False when it already ran or was dropped.</summary>
    public bool Cancel(Entry entry)
    {
        if (entry == null || entry.Done)
            return false;
        entry.Done = true;
        return _entries.Remove(entry);
    }

    /// <summary>Drops everything one owner scheduled.</summary>
    public int CancelAll(object owner)
    {
        int removed = 0;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (!Equals(_entries[i].Owner, owner))
                continue;
            _entries[i].Done = true;
            _entries.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    public void Clear()
    {
        foreach (var entry in _entries)
            entry.Done = true;
        _entries.Clear();
    }

    /// <summary>
    /// A minute passed: counts every entry down and runs the ones that reached zero, oldest first, each
    /// in its own try/catch. An action may schedule or cancel others; new ones wait for the next minute.
    /// </summary>
    public void Tick(Action<Exception> onError)
    {
        _due.Clear();
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (--entry.Left > 0)
                continue;
            entry.Done = true;
            _entries.RemoveAt(i);
            _due.Add(entry);
        }
        for (int i = _due.Count - 1; i >= 0; i--)
        {
            try { _due[i].Action(); }
            catch (Exception e) { onError?.Invoke(e); }
        }
        _due.Clear();
    }
}
