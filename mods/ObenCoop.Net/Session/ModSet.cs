using System;
using System.Collections.Generic;
using System.Text;
using ObenCoop.Net.Messaging;

namespace ObenCoop.Net.Session;

/// <summary>One mod that has to be the same on every machine.</summary>
public readonly struct ModEntry
{
    public ModEntry(string id, string version)
    {
        Id = id ?? "";
        Version = version ?? "";
    }

    public string Id { get; }
    public string Version { get; }
}

/// <summary>
/// The mods (id and version) a session needs everyone to have. The client sends its set in the
/// handshake and the host compares it to its own, so two games with different mods never end up
/// in one world. The mod decides which of its loaded mods belong here (tool mods usually don't).
/// </summary>
public sealed class ModSet
{
    private const int MaxEntries = 255;

    private readonly ModEntry[] _entries;

    public ModSet(IEnumerable<ModEntry> entries)
    {
        var list = new List<ModEntry>(entries ?? new ModEntry[0]);
        if (list.Count > MaxEntries)
            throw new ArgumentException($"At most {MaxEntries} mods can be in the set.", nameof(entries));
        list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        _entries = list.ToArray();
    }

    public static ModSet Empty { get; } = new ModSet(null);

    /// <summary>Sorted by id.</summary>
    public IReadOnlyList<ModEntry> Entries => _entries;

    /// <summary>
    /// A 32-bit hash of every id and version (FNV-1a over the sorted entries). Two sets with the same
    /// mods have the same fingerprint, so a lobby can advertise it and a joiner can tell "different
    /// mods" before connecting. The handshake still compares the full sets.
    /// </summary>
    public uint Fingerprint
    {
        get
        {
            uint hash = 2166136261;
            foreach (var entry in _entries)
            {
                hash = Mix(hash, entry.Id);
                hash = Mix(hash, entry.Version);
            }
            return hash;
        }
    }

    internal void Write(MessageWriter writer)
    {
        writer.WriteByte((byte)_entries.Length);
        foreach (var entry in _entries)
        {
            writer.WriteString(entry.Id);
            writer.WriteString(entry.Version);
        }
    }

    internal static ModSet Read(MessageReader reader)
    {
        int count = reader.ReadByte();
        var entries = new List<ModEntry>(count);
        for (int i = 0; i < count; i++)
            entries.Add(new ModEntry(reader.ReadString(), reader.ReadString()));
        return new ModSet(entries);
    }

    /// <summary>
    /// Compares a joining game's set (this one) with the host's. Returns null when they match,
    /// otherwise a sentence naming what differs, written from the joining player's point of view.
    /// </summary>
    public string DescribeDifference(ModSet host)
    {
        var missing = new List<string>();
        var extra = new List<string>();
        var different = new List<string>();

        var mine = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
            mine[entry.Id] = entry.Version;

        foreach (var entry in host._entries)
        {
            if (!mine.TryGetValue(entry.Id, out string version))
                missing.Add(entry.Id);
            else if (version != entry.Version)
                different.Add($"{entry.Id} (host {entry.Version}, you {version})");
        }

        var theirs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in host._entries)
            theirs.Add(entry.Id);
        foreach (var entry in _entries)
        {
            if (!theirs.Contains(entry.Id))
                extra.Add(entry.Id);
        }

        if (missing.Count == 0 && extra.Count == 0 && different.Count == 0)
            return null;

        var text = new StringBuilder("Your mods don't match the host's.");
        Append(text, "You are missing", missing);
        Append(text, "The host doesn't have", extra);
        Append(text, "Different versions", different);
        return text.ToString();
    }

    private static uint Mix(uint hash, string text)
    {
        foreach (char c in text)
        {
            hash = (hash ^ (byte)c) * 16777619;
            hash = (hash ^ (byte)(c >> 8)) * 16777619;
        }
        // A separator so ("ab", "c") and ("a", "bc") differ.
        return (hash ^ 0xFF) * 16777619;
    }

    private static void Append(StringBuilder text, string label, List<string> items)
    {
        if (items.Count > 0)
            text.Append(' ').Append(label).Append(": ").Append(string.Join(", ", items)).Append('.');
    }
}
