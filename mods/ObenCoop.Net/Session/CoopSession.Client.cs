using System;
using System.Collections.Generic;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Session;

// A client's side: says Hello, takes the slot and roster from the Welcome, follows roster changes.
public sealed partial class CoopSession
{
    private readonly List<Roster.RosterEntry> _entries = new List<Roster.RosterEntry>();
    private readonly List<Player> _gone = new List<Player>();

    private void SendHello()
    {
        var writer = MessagePool.RentWriter();
        try
        {
            writer.WriteByte((byte)PacketKind.Hello);
            writer.WriteInt32(Protocol.Version);
            writer.WriteString(Options.GameVersion);
            writer.WriteGuid(Options.PlayerId);
            writer.WriteString(CleanName(Options.PlayerName));
            (Options.Mods ?? ModSet.Empty).Write(writer);
            _helloSentAt = Options.Clock();
            _transport.Send(_hostPeer, writer.Buffer, 0, writer.Length, DeliveryMode.ReliableOrdered);
        }
        finally
        {
            MessagePool.Return(writer);
        }
    }

    private void HandleClientPacket(int peer, PacketKind kind, MessageReader reader)
    {
        if (peer != _hostPeer)
            return;

        switch (kind)
        {
            case PacketKind.Welcome when State == SessionState.Connecting:
                HandleWelcome(reader);
                break;
            case PacketKind.Roster when State == SessionState.Joined:
                Roster.ReadEntries(reader, _entries);
                ApplyRoster();
                break;
            case PacketKind.Data when State == SessionState.Joined:
                HandleHostData(reader);
                break;
            case PacketKind.DataUnreliable when State == SessionState.Joined:
                HandleHostData(reader);
                break;
            default:
                Router.CountDropped();
                break;
        }
    }

    private void HandleWelcome(MessageReader reader)
    {
        int protocol = reader.ReadInt32();
        if (protocol != Protocol.Version)
        {
            Abort(new SessionEnd(EndKind.Rejected, RejectReason.ProtocolVersion, "The host runs a different network protocol."));
            return;
        }
        byte mySlot = reader.ReadByte();
        Roster.ReadEntries(reader, _entries);

        foreach (var entry in _entries)
            Roster.Add(new Player(entry.Slot, entry.Id, entry.Name, entry.Slot == mySlot, -1));
        if (Roster.Local == null || Roster.Host == null)
        {
            Abort(new SessionEnd(EndKind.Rejected, RejectReason.BadHello, "The host sent a roster without you in it."));
            return;
        }

        State = SessionState.Joined;
        Log($"Joined as {Roster.Local} with {Roster.Count - 1} other player(s).");
        Raise(Started);
        foreach (var player in new List<Player>(Roster.Players))
        {
            if (!player.IsLocal)
                Raise(PlayerJoined, player);
        }
    }

    // Brings the local roster in line with the entries just received and raises the differences.
    private void ApplyRoster()
    {
        _gone.Clear();
        foreach (var player in Roster.Players)
        {
            if (!player.IsLocal && !InEntries(player))
                _gone.Add(player);
        }

        foreach (var player in _gone)
            Roster.Remove(player);
        foreach (var player in _gone)
            Raise(PlayerLeft, player, "Left the game.");

        foreach (var entry in _entries)
        {
            if (Roster.Find(entry.Slot) != null)
                continue;
            var player = new Player(entry.Slot, entry.Id, entry.Name, false, -1);
            Roster.Add(player);
            Raise(PlayerJoined, player);
        }
    }

    private bool InEntries(Player player)
    {
        foreach (var entry in _entries)
        {
            if (entry.Slot == player.Slot && entry.Id == player.Id)
                return true;
        }
        return false;
    }

    private void HostConnectionEnded(int peer, string reason)
    {
        // Only the host's connection matters to a client. Before PeerConnected the id isn't known yet.
        if (_transportConnected && peer != _hostPeer)
            return;

        bool wasJoined = State == SessionState.Joined;
        _transport.Stop();
        if (RejectText.TryParse(reason, out var reject, out string message))
            End(new SessionEnd(EndKind.Rejected, reject, message));
        else if (!_transportConnected)
            End(new SessionEnd(EndKind.ConnectFailed, RejectReason.None, reason));
        else
            End(new SessionEnd(EndKind.ConnectionLost, RejectReason.None, wasJoined ? reason : "The connection closed before joining: " + reason));
    }
}
