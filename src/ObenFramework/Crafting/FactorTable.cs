using System;
using System.Collections.Generic;

namespace ObenFramework.Crafting;

/// <summary>
/// Multipliers set by several mods for named targets (a station kind, "*" for all). Each owner has at most
/// one factor per key; the factors of different owners multiply, so two mods that each double a speed give
/// four times the speed. No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class FactorTable
{
    public const string All = "*";

    private readonly List<Entry> _entries = new List<Entry>();

    public bool Any => _entries.Count > 0;

    public int Count => _entries.Count;

    /// <summary>Sets <paramref name="owner"/>'s factor for <paramref name="key"/>, replacing its earlier one.</summary>
    public void Set(object owner, string key, float factor)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("No key given.", nameof(key));

        for (int i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i].Owner, owner) && _entries[i].Key == key)
            {
                _entries[i] = new Entry(owner, key, factor);
                return;
            }
        }
        _entries.Add(new Entry(owner, key, factor));
    }

    /// <summary>Removes one factor. True when there was one.</summary>
    public bool Remove(object owner, string key) =>
        _entries.RemoveAll(e => ReferenceEquals(e.Owner, owner) && e.Key == key) > 0;

    /// <summary>Removes everything an owner set. Returns how many.</summary>
    public int RemoveOwner(object owner) => _entries.RemoveAll(e => ReferenceEquals(e.Owner, owner));

    /// <summary>The product of all factors for one key (1 when there are none).</summary>
    public float Of(string key)
    {
        float product = 1f;
        foreach (var entry in _entries)
        {
            if (entry.Key == key)
                product *= entry.Factor;
        }
        return product;
    }

    /// <summary>
    /// The product of the factors for <see cref="All"/> and for each of the given keys, clamped to
    /// <paramref name="min"/>..<paramref name="max"/>. Null keys are skipped and a key given twice counts once.
    /// </summary>
    public float Combined(float min, float max, string first, string second = null, string third = null)
    {
        if (_entries.Count == 0)
            return Clamp(1f, min, max);

        float product = Of(All);
        if (first != null && first != All)
            product *= Of(first);
        if (second != null && second != All && second != first)
            product *= Of(second);
        if (third != null && third != All && third != first && third != second)
            product *= Of(third);
        return Clamp(product, min, max);
    }

    /// <summary>Owner, key and factor of every entry, for listing.</summary>
    public List<(object Owner, string Key, float Factor)> Entries()
    {
        var list = new List<(object, string, float)>(_entries.Count);
        foreach (var entry in _entries)
            list.Add((entry.Owner, entry.Key, entry.Factor));
        return list;
    }

    public void Clear() => _entries.Clear();

    private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

    private readonly struct Entry
    {
        public Entry(object owner, string key, float factor)
        {
            Owner = owner;
            Key = key;
            Factor = factor;
        }

        public object Owner { get; }

        public string Key { get; }

        public float Factor { get; }
    }
}
