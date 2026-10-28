using System;

namespace ObenCoop.Net.Session;

/// <summary>
/// One person in the session. The slot is what the rest of the mod uses to point at a player: the
/// host is always slot 0 and the others get the lowest free slot when they join. The id is the
/// player's persistent GUID (the same on every join, from their identity file), so a profile or
/// save can be kept per person; the slot can differ from one session to the next.
/// </summary>
public sealed class Player
{
    /// <summary>The host's slot.</summary>
    public const byte HostSlot = 0;

    internal Player(byte slot, Guid id, string name, bool isLocal, int peer)
    {
        Slot = slot;
        Id = id;
        Name = name;
        IsLocal = isLocal;
        Peer = peer;
    }

    public byte Slot { get; }

    public Guid Id { get; }

    public string Name { get; }

    public bool IsHost => Slot == HostSlot;

    public bool IsLocal { get; }

    /// <summary>The transport peer this player's packets come through (host side only; -1 elsewhere).</summary>
    internal int Peer { get; }

    public override string ToString() => $"{Name} (slot {Slot})";
}
