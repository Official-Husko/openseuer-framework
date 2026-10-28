using System;
using System.Collections.Generic;

namespace ObenLoader.Logging;

/// <summary>
/// The last text logged under each key, for lines that should only be written when the value changed
/// (a state that is checked every frame, say). Capped like <see cref="OnceKeys"/>. Thread-safe.
/// </summary>
public sealed class ChangeFilter
{
    private readonly Dictionary<string, string> _last = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly int _capacity;

    public ChangeFilter(int capacity = OnceKeys.DefaultCapacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    /// <summary>True when <paramref name="value"/> differs from what was last seen under <paramref name="key"/> (or it's new).</summary>
    public bool Changed(string key, string value)
    {
        key ??= "";
        lock (_last)
        {
            if (_last.TryGetValue(key, out string previous))
            {
                if (string.Equals(previous, value, StringComparison.Ordinal))
                    return false;
            }
            else if (_last.Count >= _capacity)
            {
                _last.Clear();
            }
            _last[key] = value;
            return true;
        }
    }

    /// <summary>Forgets one key, or every key for null, so the next value is logged again.</summary>
    public void Forget(string key = null)
    {
        lock (_last)
        {
            if (key == null)
                _last.Clear();
            else
                _last.Remove(key);
        }
    }
}
