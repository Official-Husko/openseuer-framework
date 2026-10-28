using System;
using System.Collections.Generic;
using ObenCoop.Net.Messaging;

namespace ObenCoop.Net.Session;

/// <summary>Who is in the session, ordered by slot. The host and every client keep the same list.</summary>
public sealed class Roster
{
    private readonly List<CoopPlayer> _players = new List<CoopPlayer>();

    public IReadOnlyList<CoopPlayer> Players => _players;

    public int Count => _players.Count;

    /// <summary>This game's own player, or null when no session is running.</summary>
    public CoopPlayer Local { get; private set; }

    /// <summary>The host's player, or null when no session is running.</summary>
    public CoopPlayer Host { get; private set; }

    public CoopPlayer Find(byte slot)
    {
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].Slot == slot)
                return _players[i];
        }
        return null;
    }

    public CoopPlayer Find(Guid id)
    {
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].Id == id)
                return _players[i];
        }
        return null;
    }

    internal CoopPlayer FindByPeer(int peer)
    {
        for (int i = 0; i < _players.Count; i++)
        {
            if (!_players[i].IsLocal && _players[i].Peer == peer)
                return _players[i];
        }
        return null;
    }

    internal bool NameTaken(string name)
    {
        for (int i = 0; i < _players.Count; i++)
        {
            if (string.Equals(_players[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>The lowest slot that is free, or -1 when all <paramref name="maxPlayers"/> are taken.</summary>
    internal int FreeSlot(int maxPlayers)
    {
        for (int slot = 0; slot < maxPlayers; slot++)
        {
            if (Find((byte)slot) == null)
                return slot;
        }
        return -1;
    }

    internal void Add(CoopPlayer player)
    {
        int index = _players.Count;
        while (index > 0 && _players[index - 1].Slot > player.Slot)
            index--;
        _players.Insert(index, player);
        if (player.IsLocal)
            Local = player;
        if (player.IsHost)
            Host = player;
    }

    internal bool Remove(CoopPlayer player)
    {
        if (!_players.Remove(player))
            return false;
        if (player == Local)
            Local = null;
        if (player == Host)
            Host = null;
        return true;
    }

    internal void Clear()
    {
        _players.Clear();
        Local = null;
        Host = null;
    }

    /// <summary>Writes slot, id and name of everyone. This is what the host sends to clients.</summary>
    internal void Write(MessageWriter writer)
    {
        writer.WriteByte((byte)_players.Count);
        foreach (var player in _players)
        {
            writer.WriteByte(player.Slot);
            writer.WriteGuid(player.Id);
            writer.WriteString(player.Name);
        }
    }

    internal static void ReadEntries(MessageReader reader, List<RosterEntry> into)
    {
        into.Clear();
        int count = reader.ReadByte();
        for (int i = 0; i < count; i++)
            into.Add(new RosterEntry(reader.ReadByte(), reader.ReadGuid(), reader.ReadString()));
    }

    internal readonly struct RosterEntry
    {
        public RosterEntry(byte slot, Guid id, string name)
        {
            Slot = slot;
            Id = id;
            Name = name;
        }

        public byte Slot { get; }
        public Guid Id { get; }
        public string Name { get; }
    }
}
