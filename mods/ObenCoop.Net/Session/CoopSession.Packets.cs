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
        if (State == SessionState.Hosting)
            HostPeerGone(peer, reason);
        else if (State == SessionState.Connecting || State == SessionState.Joined)
            HostConnectionEnded(peer, reason);
    }

    private void OnPacket(int peer, ArraySegment<byte> data)
    {
        if (data.Count == 0 || State == SessionState.Offline)
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
