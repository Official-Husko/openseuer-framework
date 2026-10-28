using System;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Session;

// The host's side: checks the Hello, hands out slots, keeps the roster, relays data.
public sealed partial class CoopSession
{
    private void HandleHostPacket(int peer, PacketKind kind, MessageReader reader)
    {
        switch (kind)
        {
            case PacketKind.Hello:
                HandleHello(peer, reader);
                break;
            case PacketKind.Data:
                HandleClientData(peer, reader, DeliveryMode.ReliableOrdered);
                break;
            case PacketKind.DataUnreliable:
                HandleClientData(peer, reader, DeliveryMode.Unreliable);
                break;
            default:
                Router.CountDropped();
                break;
        }
    }

    private void HandleHello(int peer, MessageReader reader)
    {
        // A second Hello from a player who is already in is a bug or an attack; ignore it.
        if (Roster.FindByPeer(peer) != null)
            return;

        int protocol = reader.ReadInt32();
        if (protocol != Protocol.Version)
        {
            Reject(peer, RejectReason.ProtocolVersion,
                $"Different network protocol: the host runs {Protocol.Version}, you run {protocol}. Both need the same ObenCoop version.");
            return;
        }

        string gameVersion;
        Guid id;
        string name;
        ModSet mods;
        try
        {
            gameVersion = reader.ReadString();
            id = reader.ReadGuid();
            name = reader.ReadString();
            mods = ModSet.Read(reader);
        }
        catch (MessageFormatException)
        {
            Reject(peer, RejectReason.BadHello, "The join request was damaged.");
            return;
        }

        if (gameVersion != Options.GameVersion)
        {
            Reject(peer, RejectReason.GameVersion,
                $"Different game versions: the host runs {Options.GameVersion}, you run {gameVersion}.");
            return;
        }

        string difference = mods.DescribeDifference(Options.Mods ?? ModSet.Empty);
        if (difference != null)
        {
            Reject(peer, RejectReason.ModSet, difference);
            return;
        }

        if (Roster.Find(id) != null)
        {
            Reject(peer, RejectReason.DuplicatePlayer, "You are already connected to this game. If you just dropped out, try again in a few seconds.");
            return;
        }

        int slot = Roster.FreeSlot(Options.MaxPlayers);
        if (slot < 0)
        {
            Reject(peer, RejectReason.Full, "The game is full.");
            return;
        }

        var player = new Player((byte)slot, id, UniqueName(CleanName(name)), false, peer);
        RemovePending(peer);
        Roster.Add(player);
        SendWelcome(player);
        BroadcastRoster(player);
        Log($"{player} joined from peer {peer}.");
        Raise(PlayerJoined, player);
    }

    private void SendWelcome(Player player)
    {
        var writer = MessagePool.RentWriter();
        try
        {
            writer.WriteByte((byte)PacketKind.Welcome);
            writer.WriteInt32(Protocol.Version);
            writer.WriteByte(player.Slot);
            Roster.Write(writer);
            _transport.Send(player.Peer, writer.Buffer, 0, writer.Length, DeliveryMode.ReliableOrdered);
        }
        finally
        {
            MessagePool.Return(writer);
        }
    }

    // Sends the roster to everyone except one player (the one who just got a Welcome with it).
    private void BroadcastRoster(Player except)
    {
        var writer = MessagePool.RentWriter();
        try
        {
            writer.WriteByte((byte)PacketKind.Roster);
            Roster.Write(writer);
            var players = Roster.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].IsLocal && players[i] != except)
                    _transport.Send(players[i].Peer, writer.Buffer, 0, writer.Length, DeliveryMode.ReliableOrdered);
            }
        }
        finally
        {
            MessagePool.Return(writer);
        }
    }

    private void Reject(int peer, RejectReason reason, string message)
    {
        RemovePending(peer);
        Log($"Turned peer {peer} away ({reason}): {message}");
        _transport.DisconnectPeer(peer, RejectText.Encode(reason, message));
    }

    private void HostPeerGone(int peer, string reason)
    {
        RemovePending(peer);
        var player = Roster.FindByPeer(peer);
        if (player == null)
            return;
        Roster.Remove(player);
        BroadcastRoster(null);
        Log($"{player} left: {reason}");
        Raise(PlayerLeft, player, reason);
    }

    private void CheckPendingTimeouts(long now)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (i < _pending.Count && now - _pending[i].Since > Options.HandshakeTimeoutMs)
                Reject(_pending[i].Peer, RejectReason.BadHello, "No join request arrived in time.");
        }
    }

    private void RemovePending(int peer)
    {
        for (int i = 0; i < _pending.Count; i++)
        {
            if (_pending[i].Peer == peer)
            {
                _pending.RemoveAt(i);
                return;
            }
        }
    }

    private void CloseHostPeers(string reason)
    {
        foreach (var player in Roster.Players)
        {
            if (!player.IsLocal)
                _transport.DisconnectPeer(player.Peer, reason);
        }
    }

    // Two players can't share a name; the later one gets a number.
    private string UniqueName(string name)
    {
        if (!Roster.NameTaken(name))
            return name;
        for (int n = 2; ; n++)
        {
            string candidate = name.Length + 3 > Protocol.MaxNameLength
                ? name.Substring(0, Protocol.MaxNameLength - 3) + " " + n
                : name + " " + n;
            if (!Roster.NameTaken(candidate))
                return candidate;
        }
    }
}
