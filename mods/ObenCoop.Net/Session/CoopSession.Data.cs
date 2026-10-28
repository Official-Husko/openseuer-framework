using System;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Session;

// Channel messages. Wire format of a data packet: kind (Data or DataUnreliable), channel, slot, payload.
// From a client the slot is the target (a player's slot, or AllOthers); from the host it is the sender.
public sealed partial class CoopSession
{
    /// <summary>
    /// Sends a message to every other player. On a client the host passes it on. The writer holds
    /// only the payload; it is copied, so it can be returned to the pool right after.
    /// </summary>
    /// <returns>False when not in a session or the message is too big for its delivery mode.</returns>
    public bool Broadcast(byte channel, MessageWriter payload, DeliveryMode mode = DeliveryMode.ReliableOrdered)
    {
        if (State == SessionState.Hosting)
            return SendFromHost(null, channel, payload, mode, true);
        return State == SessionState.Joined && SendToHostPeer(Protocol.AllOthers, channel, payload, mode, true);
    }

    /// <summary>Sends a message to one player. On a client the host passes it on unless the target is the host.</summary>
    public bool SendTo(CoopPlayer target, byte channel, MessageWriter payload, DeliveryMode mode = DeliveryMode.ReliableOrdered) =>
        SendTo(target, channel, payload, mode, true);

    // The blob layer sends its small control packets with limited=false: they are bounded by the
    // number of transfers, and losing an ack to a full queue would stall a transfer for nothing.
    internal bool SendTo(CoopPlayer target, byte channel, MessageWriter payload, DeliveryMode mode, bool limited)
    {
        if (target == null || target.IsLocal || Roster.Find(target.Slot) != target)
            return false;
        if (State == SessionState.Hosting)
            return SendFromHost(target, channel, payload, mode, limited);
        return State == SessionState.Joined && SendToHostPeer(target.Slot, channel, payload, mode, limited);
    }

    /// <summary>Sends a message to the host only. False on the host itself.</summary>
    public bool SendToHost(byte channel, MessageWriter payload, DeliveryMode mode = DeliveryMode.ReliableOrdered) =>
        State == SessionState.Joined && SendToHostPeer(CoopPlayer.HostSlot, channel, payload, mode, true);

    // Host: to one player or, with null, to everyone else. The slot byte says who sent it (the host).
    private bool SendFromHost(CoopPlayer only, byte channel, MessageWriter payload, DeliveryMode mode, bool limited)
    {
        var envelope = Envelope(mode, channel, CoopPlayer.HostSlot, payload.Buffer, 0, payload.Length);
        try
        {
            bool sent = false;
            var players = Roster.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player.IsLocal || (only != null && player != only))
                    continue;
                sent |= SendPacket(player, envelope, mode, limited);
            }
            return sent;
        }
        finally
        {
            MessagePool.Return(envelope);
        }
    }

    private bool SendToHostPeer(byte target, byte channel, MessageWriter payload, DeliveryMode mode, bool limited)
    {
        var envelope = Envelope(mode, channel, target, payload.Buffer, 0, payload.Length);
        try
        {
            return SendPacket(Roster.Host, envelope, mode, limited);
        }
        finally
        {
            MessagePool.Return(envelope);
        }
    }

    private static MessageWriter Envelope(DeliveryMode mode, byte channel, byte slot, byte[] payload, int offset, int count)
    {
        var writer = MessagePool.RentWriter();
        writer.WriteByte((byte)(mode == DeliveryMode.Unreliable ? PacketKind.DataUnreliable : PacketKind.Data));
        writer.WriteByte(channel);
        writer.WriteByte(slot);
        writer.WriteBytes(payload, offset, count);
        return writer;
    }

    // Host receiving from a client: deliver here, pass on to the target(s).
    private void HandleClientData(int peer, MessageReader reader, DeliveryMode mode)
    {
        var sender = Roster.FindByPeer(peer);
        if (sender == null)
        {
            Router.CountDropped();
            return;
        }

        byte channel = reader.ReadByte();
        byte target = reader.ReadByte();
        var payload = reader.ReadRest();

        if (target == Protocol.AllOthers || target == CoopPlayer.HostSlot)
            DeliverLocally(channel, sender, payload);
        if (target == CoopPlayer.HostSlot)
            return;

        var envelope = Envelope(mode, channel, sender.Slot, payload.Array, payload.Offset, payload.Count);
        try
        {
            var players = Roster.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player.IsLocal || player == sender || (target != Protocol.AllOthers && player.Slot != target))
                    continue;
                SendPacket(player, envelope, mode, true);
            }
        }
        finally
        {
            MessagePool.Return(envelope);
        }
    }

    // Client receiving from the host: the slot byte names the original sender.
    private void HandleHostData(MessageReader reader)
    {
        byte channel = reader.ReadByte();
        var sender = Roster.Find(reader.ReadByte());
        if (sender == null)
        {
            Router.CountDropped();
            return;
        }
        Router.Dispatch(channel, sender, reader);
    }

    private void DeliverLocally(byte channel, CoopPlayer sender, ArraySegment<byte> payload)
    {
        var reader = MessagePool.RentReader().Init(payload);
        try
        {
            Router.Dispatch(channel, sender, reader);
        }
        finally
        {
            MessagePool.Return(reader);
        }
    }
}
