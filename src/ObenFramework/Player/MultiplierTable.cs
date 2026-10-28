using System;
using System.Collections.Generic;

namespace ObenFramework.Player;

/// <summary>One mod's multiplier on one channel, as listed by the APIs built on <see cref="MultiplierTable"/>.</summary>
public sealed class MultiplierEntry
{
    internal MultiplierEntry(string ownerId, int channel, string key, float value)
    {
        OwnerId = ownerId;
        Channel = channel;
        Key = key;
        Value = value;
    }

    public string OwnerId { get; }

    /// <summary>The channel as a number; the API that hands this out says which enum it is.</summary>
    public int Channel { get; }

    /// <summary>Tells apart several multipliers from the same mod; empty when the mod only has one.</summary>
    public string Key { get; }

    public float Value { get; internal set; }
}

/// <summary>
/// Multipliers that several mods set independently on a fixed set of channels. They combine by
/// multiplying, so two mods that each double a value make it four times, and when one goes away the
/// other's stays. The product of each channel is kept up to date on every change, so reading it is an
/// array lookup. No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class MultiplierTable
{
    private readonly List<MultiplierEntry> _entries = new List<MultiplierEntry>();
    private readonly float[] _products;

    /// <param name="channels">How many channels there are (channel numbers run from 0).</param>
    /// <param name="max">Largest value a single multiplier and a channel's product may have.</param>
    public MultiplierTable(int channels, float max)
    {
        if (channels <= 0)
            throw new ArgumentOutOfRangeException(nameof(channels));
        _products = new float[channels];
        Max = max;
        for (int i = 0; i < channels; i++)
            _products[i] = 1f;
    }

    public float Max { get; }

    public int Channels => _products.Length;

    public bool IsEmpty => _entries.Count == 0;

    /// <summary>The multipliers in the order they were first set. A copy, safe to keep.</summary>
    public MultiplierEntry[] Entries => _entries.ToArray();

    /// <summary>All multipliers on a channel multiplied together, capped at <see cref="Max"/>; 1 when none.</summary>
    public float Product(int channel) => _products[channel];

    /// <summary>True for a number from 0 to <see cref="Max"/>.</summary>
    public bool Accepts(float value) => value >= 0f && value <= Max;

    /// <summary>
    /// Sets an owner's multiplier on a channel; the same owner, channel and key again replaces the earlier
    /// value. Returns true when the channel's product changed.
    /// </summary>
    public bool Set(string ownerId, int channel, string key, float value)
    {
        if (string.IsNullOrEmpty(ownerId))
            throw new ArgumentException("An owner id is needed.", nameof(ownerId));
        CheckChannel(channel);
        if (!Accepts(value))
            throw new ArgumentOutOfRangeException(nameof(value), $"A multiplier must be a number from 0 to {Max}.");

        key ??= "";
        var entry = Find(ownerId, channel, key);
        if (entry == null)
            _entries.Add(new MultiplierEntry(ownerId, channel, key, value));
        else
            entry.Value = value;
        return Recompute(channel);
    }

    /// <summary>Takes one multiplier away. False when the owner had none there.</summary>
    public bool Remove(string ownerId, int channel, string key)
    {
        CheckChannel(channel);
        var entry = Find(ownerId, channel, key ?? "");
        if (entry == null || !_entries.Remove(entry))
            return false;
        Recompute(channel);
        return true;
    }

    /// <summary>Takes away everything an owner set. Returns how many multipliers went.</summary>
    public int RemoveOwner(string ownerId)
    {
        int removed = _entries.RemoveAll(e => e.OwnerId == ownerId);
        if (removed > 0)
            RecomputeAll();
        return removed;
    }

    public void Clear()
    {
        _entries.Clear();
        RecomputeAll();
    }

    private MultiplierEntry Find(string ownerId, int channel, string key)
    {
        foreach (var entry in _entries)
        {
            if (entry.Channel == channel && entry.OwnerId == ownerId && entry.Key == key)
                return entry;
        }
        return null;
    }

    private bool Recompute(int channel)
    {
        float product = 1f;
        foreach (var entry in _entries)
        {
            if (entry.Channel == channel)
                product *= entry.Value;
        }
        if (product > Max)
            product = Max;

        bool changed = product != _products[channel];
        _products[channel] = product;
        return changed;
    }

    private void RecomputeAll()
    {
        for (int i = 0; i < _products.Length; i++)
            Recompute(i);
    }

    private void CheckChannel(int channel)
    {
        if (channel < 0 || channel >= _products.Length)
            throw new ArgumentOutOfRangeException(nameof(channel));
    }
}
