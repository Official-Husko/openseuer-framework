using System;
using System.Collections.Generic;

namespace ObenFramework.Items;

/// <summary>
/// Item counts per id, compared against an earlier tally to find what was added and removed. The two
/// dictionaries are reused between checks, so a check that finds nothing allocates nothing. No Unity
/// types, so it is unit tested on its own.
/// </summary>
internal sealed class ItemTally
{
    private static readonly Comparison<KeyValuePair<int, int>> ByKind = Order;

    private Dictionary<int, int> _before = new Dictionary<int, int>();
    private Dictionary<int, int> _now = new Dictionary<int, int>();

    /// <summary>True once <see cref="Commit"/> ran at least once since the last <see cref="Forget"/>.</summary>
    public bool HasBaseline { get; private set; }

    /// <summary>Starts a new count. Call <see cref="Add"/> for every stack, then <see cref="Diff"/> or <see cref="Commit"/>.</summary>
    public void Begin() => _now.Clear();

    public void Add(int itemId, int amount)
    {
        if (itemId < 0 || amount <= 0)
            return;
        _now.TryGetValue(itemId, out int sum);
        _now[itemId] = sum + amount;
    }

    /// <summary>How many of an item the last commit counted.</summary>
    public int Committed(int itemId) => _before.TryGetValue(itemId, out int n) ? n : 0;

    /// <summary>
    /// Writes the differences between the new count and the committed one into <paramref name="into"/>
    /// (cleared first): positive amounts were added, negative ones removed. Ordered removals first, then
    /// additions, each by item id, so the output doesn't depend on dictionary order.
    /// </summary>
    public void Diff(List<KeyValuePair<int, int>> into)
    {
        into.Clear();
        foreach (var pair in _now)
        {
            _before.TryGetValue(pair.Key, out int old);
            if (pair.Value != old)
                into.Add(new KeyValuePair<int, int>(pair.Key, pair.Value - old));
        }
        foreach (var pair in _before)
        {
            if (!_now.ContainsKey(pair.Key))
                into.Add(new KeyValuePair<int, int>(pair.Key, -pair.Value));
        }
        if (into.Count > 1)
            into.Sort(ByKind);
    }

    /// <summary>Makes the new count the one later counts are compared against.</summary>
    public void Commit()
    {
        (_before, _now) = (_now, _before);
        HasBaseline = true;
    }

    /// <summary>Drops the baseline; the next count becomes the baseline without reporting differences.</summary>
    public void Forget()
    {
        _before.Clear();
        _now.Clear();
        HasBaseline = false;
    }

    private static int Order(KeyValuePair<int, int> a, KeyValuePair<int, int> b)
    {
        bool aRemoved = a.Value < 0, bRemoved = b.Value < 0;
        if (aRemoved != bRemoved)
            return aRemoved ? -1 : 1;
        return a.Key.CompareTo(b.Key);
    }
}
