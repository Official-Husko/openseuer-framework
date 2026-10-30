using System;

namespace ObenCoop.Net.Messaging;

/// <summary>
/// Channels named by a string, for mods: "mymod.doors" instead of a byte that two mods could both
/// pick. A message on a named channel travels on <see cref="ChannelIds.Named"/> with the name's key
/// (a 32-bit hash) in front, so the name itself never goes over the wire; blobs carry the key in their
/// offer. Both sides work the key out from the name, so they only have to agree on the name. Keys are
/// never below 256, which keeps them apart from numbered blob channels.
/// </summary>
public static class ChannelNames
{
    public const int MaxLength = 64;

    /// <summary>The key a name travels as.</summary>
    /// <exception cref="ArgumentException">The name is empty, too long or has spaces or control characters.</exception>
    public static uint Key(string name)
    {
        Check(name);
        // FNV-1a over the UTF-16 code units: the same on every machine and every .NET.
        uint hash = 2166136261;
        foreach (char c in name)
        {
            hash = (hash ^ (byte)c) * 16777619;
            hash = (hash ^ (byte)(c >> 8)) * 16777619;
        }
        return hash < 256 ? hash + 256 : hash;
    }

    /// <exception cref="ArgumentException">The name is empty, too long or has spaces or control characters.</exception>
    public static void Check(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("A channel name can't be empty.", nameof(name));
        if (name.Length > MaxLength)
            throw new ArgumentException($"A channel name can be at most {MaxLength} characters: '{name}'.", nameof(name));
        foreach (char c in name)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
                throw new ArgumentException($"A channel name can't have spaces or control characters: '{name}'.", nameof(name));
        }
    }
}
