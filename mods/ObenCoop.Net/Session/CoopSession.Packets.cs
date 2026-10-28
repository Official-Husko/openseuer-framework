using System;
using ObenCoop.Net.Messaging;

namespace ObenCoop.Net.Session;

public sealed partial class CoopSession
{
    private void OnPeerConnected(int peer)
    {
        if (State == SessionState.Hosting)
        {
            _pending.Add(new PendingPeer(peer, Options.Clock()));
        }
        else if (State == SessionState.Connecting && !_transportConnected)
        {
            _transportConnected = true;
            _hostPeer = peer;
            SendHello();
        }
    }

    private void OnPeerDisconnected(int peer, string reason)
    {
        // Packets that arrived before the disconnect are handled first, budget or not.
        _inbound.Flush(peer, _processQueued);
        _backedUp.Remove(peer);
        if (State == SessionState.Hosting)
            HostPeerGone(peer, reason);
        else if (State == SessionState.Connecting || State == SessionState.Joined)
            HostConnectionEnded(peer, reason);
    }

    // Called by the transport for every packet. Handled at once while the peer has budget left,
    // otherwise queued for a later poll (see InboundQueue).
    private void OnPacket(int peer, ArraySegment<byte> data)
    {
        if (data.Count == 0 || State == SessionState.Offline)
            return;
        if (_inbound.TryAdmit(peer, Options.MaxMessagesPerPoll))
            ProcessPacket(peer, data);
        else if (!_inbound.Enqueue(peer, data, Options.MaxQueuedMessages))
            InboundOverflow(peer, data);
    }

    private void ProcessPacket(int peer, ArraySegment<byte> data)
    {
        if (State == SessionState.Offline)
            return;

        var reader = MessagePool.RentReader().Init(data);
        try
        {
            var kind = (PacketKind)reader.ReadByte();
            if (State == SessionState.Hosting)
                HandleHostPacket(peer, kind, reader);
            else
                HandleClientPacket(peer, kind, reader);
        }
        catch (MessageFormatException e)
        {
            Router.CountDropped();
            Log($"Dropped a damaged packet from peer {peer}: {e.Message}");
        }
        finally
        {
            MessagePool.Return(reader);
        }
    }
}
