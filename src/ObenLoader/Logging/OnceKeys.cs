using System;
using System.Collections.Generic;

namespace ObenLoader.Logging;

/// <summary>
/// The keys a logger has already reported once. Capped so a key built from changing text can't grow it
/// forever: when full it starts over, and a repeat may then be logged one more time. Thread-safe.
/// </summary>
public sealed class OnceKeys
{
    public const int DefaultCapacity = 1024;

    private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);
    private readonly int _capacity;

    public OnceKeys(int capacity = DefaultCapacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    public int Count
    {
        get
        {
            lock (_keys)
                return _keys.Count;
        }
    }

    /// <summary>True the first time <paramref name="key"/> comes up, false for every repeat.</summary>
    public bool First(string key)
    {
        key ??= "";
        lock (_keys)
        {
            if (_keys.Count >= _capacity && !_keys.Contains(key))
                _keys.Clear();
            return _keys.Add(key);
        }
    }

    /// <summary>Forgets every key starting with <paramref name="prefix"/>, or all of them for null.</summary>
    public void Forget(string prefix = null)
    {
        lock (_keys)
        {
            if (string.IsNullOrEmpty(prefix))
                _keys.Clear();
            else
                _keys.RemoveWhere(k => k.StartsWith(prefix, StringComparison.Ordinal));
        }
    }
}
