using System;
using System.Collections.Generic;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Session;

// Flood control in both directions: a per-peer budget of packets handled per poll with the rest
// queued for the next polls (receiving), and a limit on how much may wait to be sent to one peer.
public sealed partial class CoopSession
{
    private readonly InboundQueue _inbound = new InboundQueue();
    private readonly HashSet<int> _backedUp = new HashSet<int>();
    private readonly Action<int, ArraySegment<byte>> _processQueued;

    /// <summary>
    /// Raised when a send to a player was refused because too many packets are already waiting to go
    /// out to them (<see cref="SessionOptions.MaxSendQueue"/>). Once per backed-up spell, not per message.
    /// The text says which player and why, in words a player can read.
    /// </summary>
    public event Action<CoopPlayer, string> SendRefused;

    /// <summary>Messages refused because a peer's send queue was full, since the session was created.</summary>
    public long SendsRefused { get; private set; }

    /// <summary>Packets that were not handled in the poll they arrived in because their peer was over its budget.</summary>
    public long ReceivesDeferred => _inbound.Deferred;

    /// <summary>Packets waiting for a later poll right now.</summary>
    public int ReceivesWaiting => _inbound.Waiting;

    /// <summary>True when the transport still has room to queue more reliable packets for this player.</summary>
    internal bool HasSendRoom(CoopPlayer player)
    {
        int peer = PeerOf(player);
        return peer < 0 || Options.MaxSendQueue <= 0 || _transport.QueuedPackets(peer) < Options.MaxSendQueue;
    }

    private int PeerOf(CoopPlayer player) => State == SessionState.Hosting ? player.Peer : _hostPeer;

    // Sends an envelope to a player (through the host on a client). Reliable packets are refused
    // while the peer's send queue is over its limit.
    private bool SendPacket(CoopPlayer to, MessageWriter envelope, DeliveryMode mode, bool limited)
    {
        int peer = PeerOf(to);
        if (limited && mode == DeliveryMode.ReliableOrdered && Options.MaxSendQueue > 0)
        {
            int queued = _transport.QueuedPackets(peer);
            if (queued >= Options.MaxSendQueue)
            {
                Refuse(to, peer, queued);
                return false;
            }
            _backedUp.Remove(peer);
        }
        return _transport.Send(peer, envelope.Buffer, 0, envelope.Length, mode);
    }

    private void Refuse(CoopPlayer to, int peer, int queued)
    {
        SendsRefused++;
        if (!_backedUp.Add(peer))
            return;
        string who = to.IsHost && !IsHost ? "the host" : to.Name;
        string text = $"Can't send to {who}: {queued} packets are already waiting to go out (limit {Options.MaxSendQueue}). " +
                      "Their connection is too slow or stuck; messages to them are refused until it catches up.";
        Log(text);
        Raise(SendRefused, to, text);
    }

    // A peer's receive queue is full. Unreliable packets are dropped; for reliable ones that would
    // mean losing state, so the connection is closed instead.
    private void InboundOverflow(int peer, ArraySegment<byte> data)
    {
        Router.CountDropped();
        if ((PacketKind)data.Array[data.Offset] == PacketKind.DataUnreliable)
            return;

        const string reason = "Too many messages were waiting to be handled; the connection was closed.";
        Log($"Peer {peer} overflowed the receive queue ({Options.MaxQueuedMessages} packets).");
        if (State == SessionState.Hosting)
            _transport.DisconnectPeer(peer, reason);
        else if (peer == _hostPeer)
            Abort(new SessionEnd(EndKind.ConnectionLost, RejectReason.None, "The host sent more than this game could handle; the connection was closed."));
    }
}
