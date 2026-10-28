using System;
using System.Collections.Generic;
using ObenLoader.Runtime;

namespace ObenFramework.Env;

/// <summary>One mod's value for one key, as listed by the override APIs.</summary>
public sealed class OwnedValue<T>
{
    internal OwnedValue(ITracker owner, string ownerId, string key, T value)
    {
        Owner = owner;
        OwnerId = ownerId;
        Key = key;
        Value = value;
    }

    internal ITracker Owner { get; }

    /// <summary>The id of the mod that set it.</summary>
    public string OwnerId { get; }

    /// <summary>What the value is for (a flag name, a zone id); empty for single-value overrides.</summary>
    public string Key { get; }

    public T Value { get; internal set; }
}

/// <summary>
/// Values that several mods set on the same keys, where the newest one wins and the older ones come back
/// when it goes away. Every owner is tracked once: when it unloads, its values are removed and
/// <see cref="Released"/> is called for each key it held, so the caller can put the world back. No Unity
/// types, so it is unit tested on its own.
/// </summary>
internal sealed class OwnedValues<T>
{
    private readonly List<OwnedValue<T>> _entries = new List<OwnedValue<T>>();
    private readonly HashSet<ITracker> _tracked = new HashSet<ITracker>();

    /// <summary>Called with each key an unloading owner held, after its values are gone.</summary>
    public Action<string> Released { get; set; }

    public bool IsEmpty => _entries.Count == 0;

    public int Count => _entries.Count;

    /// <summary>Every value in the order they were set, oldest first. A copy.</summary>
    public List<OwnedValue<T>> Entries => new List<OwnedValue<T>>(_entries);

    /// <summary>Sets <paramref name="owner"/>'s value for <paramref name="key"/>; it becomes the newest for that key.</summary>
    public void Set(ITracker owner, string ownerId, string key, T value)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        key ??= "";
        int index = IndexOf(owner, key);
        if (index >= 0)
            _entries.RemoveAt(index);
        _entries.Add(new OwnedValue<T>(owner, ownerId ?? "", key, value));
        if (_tracked.Add(owner))
            owner.Track(Disposable.Create(() => Forget(owner)));
    }

    /// <summary>Removes <paramref name="owner"/>'s value for <paramref name="key"/>. True when there was one.</summary>
    public bool Remove(ITracker owner, string key)
    {
        int index = IndexOf(owner, key ?? "");
        if (index < 0)
            return false;
        _entries.RemoveAt(index);
        return true;
    }

    /// <summary>Removes every value of <paramref name="owner"/> and returns the keys it held.</summary>
    public List<string> RemoveOwner(ITracker owner)
    {
        var keys = new List<string>();
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_entries[i].Owner, owner))
                continue;
            if (!keys.Contains(_entries[i].Key))
                keys.Add(_entries[i].Key);
            _entries.RemoveAt(i);
        }
        return keys;
    }

    /// <summary>The newest value for <paramref name="key"/>.</summary>
    public bool TryGet(string key, out T value)
    {
        key ??= "";
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Key == key)
            {
                value = _entries[i].Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    public bool Has(string key) => TryGet(key, out _);

    /// <summary>
    /// The newest non-null result of <paramref name="field"/> over the values for <paramref name="key"/>. Lets
    /// values with optional parts (a sky override that only sets fog) combine: each part comes from the newest
    /// value that sets it.
    /// </summary>
    public TField? Pick<TField>(string key, Func<T, TField?> field) where TField : struct
    {
        key ??= "";
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Key != key)
                continue;
            var picked = field(_entries[i].Value);
            if (picked.HasValue)
                return picked;
        }
        return null;
    }

    /// <summary>Every key that has at least one value, in the order first set.</summary>
    public List<string> Keys()
    {
        var keys = new List<string>();
        foreach (var entry in _entries)
        {
            if (!keys.Contains(entry.Key))
                keys.Add(entry.Key);
        }
        return keys;
    }

    /// <summary>Drops everything without calling <see cref="Released"/>. For a framework unload, which restores state itself.</summary>
    public void Clear()
    {
        _entries.Clear();
        _tracked.Clear();
    }

    private void Forget(ITracker owner)
    {
        _tracked.Remove(owner);
        var keys = RemoveOwner(owner);
        var released = Released;
        if (released == null)
            return;
        foreach (string key in keys)
            released(key);
    }

    private int IndexOf(ITracker owner, string key)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i].Owner, owner) && _entries[i].Key == key)
                return i;
        }
        return -1;
    }
}
