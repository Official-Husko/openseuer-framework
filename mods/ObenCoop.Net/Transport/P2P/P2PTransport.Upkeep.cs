using System.Collections.Generic;

namespace ObenCoop.Net.Transport.P2P;

// Timers: pings and round trips, silent peers, a connect that never got an answer, and closing
// sessions a little after the goodbye so the goodbye still arrives.
public sealed partial class P2PTransport
{
    // A session request can come in before the lobby says the requester is a member (the two travel
    // separately), so a request that isn't admitted yet waits a few seconds before it is ignored.
    private const int DeferMs = 5000;

    private readonly List<PendingClose> _closing = new List<PendingClose>();
    private readonly List<PendingClose> _deferred = new List<PendingClose>();

    private void Upkeep(long now)
    {
        CloseDue(now);
        if (!_running)
        {
            _deferred.Clear();
            return;
        }
        AcceptDeferred(now);

        if (!_hosting && _hostId != 0 && _peers.Count == 0 && now - _connectStarted > ConnectTimeoutMs)
        {
            ulong host = _hostId;
            _hostId = 0;
            CloseLater(host);
            PeerDisconnected?.Invoke(-1, "The host didn't answer.");
            return;
        }

        for (int i = _peers.Count - 1; i >= 0; i--)
        {
            if (i >= _peers.Count)
                continue;
            var peer = _peers[i];
            if (now - peer.LastHeard > TimeoutMs)
            {
                Remove(peer);
                PeerDisconnected?.Invoke(peer.Peer, "The connection timed out.");
                continue;
            }
            if (now - peer.LastPing >= PingIntervalMs)
            {
                peer.LastPing = now;
                _sendBuffer[0] = P2PWire.Ping;
                WriteInt32(_sendBuffer, 1, (int)now);
                SendRaw(peer.Id, 5, false);
            }
        }
    }

    private void Defer(ulong id)
    {
        for (int i = 0; i < _deferred.Count; i++)
        {
            if (_deferred[i].Id == id)
                return;
        }
        _deferred.Add(new PendingClose { Id = id, At = _clock() + DeferMs });
    }

    private void AcceptDeferred(long now)
    {
        var admit = Admit;
        for (int i = _deferred.Count - 1; i >= 0; i--)
        {
            var waiting = _deferred[i];
            bool admitted = _hosting && (admit == null || admit(waiting.Id));
            if (!admitted && now < waiting.At)
                continue;
            _deferred.RemoveAt(i);
            if (admitted)
                _net.Accept(waiting.Id);
        }
    }

    private void CloseLater(ulong id)
    {
        for (int i = 0; i < _closing.Count; i++)
        {
            if (_closing[i].Id == id)
                return;
        }
        _closing.Add(new PendingClose { Id = id, At = _clock() + CloseDelayMs });
    }

    private void CloseDue(long now)
    {
        for (int i = _closing.Count - 1; i >= 0; i--)
        {
            var close = _closing[i];
            if (now < close.At)
                continue;
            _closing.RemoveAt(i);
            // Someone who reconnected in the meantime (or that this game is connecting to again) keeps their session.
            bool reconnecting = _running && !_hosting && close.Id == _hostId && _peers.Count == 0;
            if (Find(close.Id) == null && !reconnecting)
                _net.Close(close.Id);
        }
    }

    private static void WriteInt32(byte[] buffer, int offset, int value)
    {
        for (int i = 0; i < 4; i++)
            buffer[offset + i] = (byte)(value >> (8 * i));
    }

    private static int ReadInt32(byte[] buffer, int offset) =>
        buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 24);

    private struct PendingClose
    {
        public ulong Id;
        public long At;
    }
}
