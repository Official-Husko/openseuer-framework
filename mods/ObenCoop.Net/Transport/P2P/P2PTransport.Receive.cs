using System;
using System.Collections.Generic;
using System.Text;

namespace ObenCoop.Net.Transport.P2P;

/// <summary>The first byte of every packet on a peer-to-peer transport.</summary>
internal static class P2PWire
{
    /// <summary>Client to host: let me in. Followed by the magic bytes.</summary>
    public const byte Hello = 1;

    /// <summary>Host to client: you're in.</summary>
    public const byte Accept = 2;

    /// <summary>Channel data for the session, unchanged after this byte.</summary>
    public const byte Data = 3;

    /// <summary>Goodbye, followed by the reason as UTF-8 (empty: the other side just left).</summary>
    public const byte Bye = 4;

    /// <summary>Followed by the sender's clock (4 bytes); answered with Pong and the same bytes.</summary>
    public const byte Ping = 5;
    public const byte Pong = 6;

    /// <summary>Hello carries these so stray packets from another game on the same channel are ignored.</summary>
    public static readonly byte[] Magic = { (byte)'O', (byte)'B', (byte)'C', (byte)'P' };

    public const string LeftText = "The other side closed the game or left.";
}

// Incoming packets, and the network's own events, which are queued and handled in Poll.
public sealed partial class P2PTransport
{
    // A flood of packets must not hold up the frame forever; what is left waits in the network's queue.
    private const int MaxPacketsPerPoll = 4096;

    private readonly byte[] _receiveBuffer = new byte[TransportLimits.MaxReliableBytes + 1];
    private readonly List<PendingEvent> _events = new List<PendingEvent>();
    private readonly List<PendingEvent> _handling = new List<PendingEvent>();

    /// <summary>Packets thrown away: too big, damaged, or from someone who isn't connected.</summary>
    public long Dropped { get; private set; }

    public void Poll()
    {
        HandleEvents();
        long now = _clock();
        for (int i = 0; i < MaxPacketsPerPoll; i++)
        {
            int count = _net.Receive(_receiveBuffer, out ulong from);
            if (count < 0)
                break;
            if (_running && count > 0)
                HandlePacket(from, count, now);
            else
                Dropped++;
        }
        Upkeep(now);
    }

    /// <summary>
    /// Housekeeping while stopped: closes sessions whose goodbye has had time to go out and throws
    /// away packets nobody waits for. The lobby calls it every frame, since a stopped transport may
    /// no longer be the one the session polls. Does nothing while running.
    /// </summary>
    public void Idle()
    {
        if (!_running)
            Poll();
    }

    private void HandlePacket(ulong from, int count, long now)
    {
        _bytesReceived += count;
        _packetsReceived++;
        var peer = Find(from);
        if (peer != null)
            peer.LastHeard = now;

        switch (_receiveBuffer[0])
        {
            case P2PWire.Data when peer != null:
                PacketReceived?.Invoke(peer.Peer, new ArraySegment<byte>(_receiveBuffer, 1, count - 1));
                break;
            case P2PWire.Hello when _hosting && HasMagic(count):
                HandleHello(from, peer);
                break;
            case P2PWire.Accept when !_hosting && from == _hostId && peer == null:
                peer = Add(from);
                PeerConnected?.Invoke(peer.Peer);
                break;
            case P2PWire.Bye:
                HandleBye(from, peer, count);
                break;
            case P2PWire.Ping when peer != null && count == 5:
                _receiveBuffer[0] = P2PWire.Pong;
                Buffer.BlockCopy(_receiveBuffer, 0, _sendBuffer, 0, 5);
                SendRaw(from, 5, false);
                break;
            case P2PWire.Pong when peer != null && count == 5:
                int sent = ReadInt32(_receiveBuffer, 1);
                peer.RoundTrip = Math.Max(0, (int)now - sent);
                break;
            default:
                Dropped++;
                break;
        }
    }

    private void HandleHello(ulong from, P2PPeer known)
    {
        if (known != null)
        {
            // A repeated hello (the client didn't hear the accept in time); answer again.
            SendControl(from, P2PWire.Accept, true);
            return;
        }
        var admit = Admit;
        if (admit != null && !admit(from))
        {
            SendBye(from, "You are not in this game's lobby.");
            CloseLater(from);
            return;
        }
        if (_peers.Count >= _maxPeers)
        {
            SendBye(from, "The game is full.");
            CloseLater(from);
            return;
        }
        var peer = Add(from);
        SendControl(from, P2PWire.Accept, true);
        PeerConnected?.Invoke(peer.Peer);
    }

    private void HandleBye(ulong from, P2PPeer peer, int count)
    {
        string reason = count > 1 ? Encoding.UTF8.GetString(_receiveBuffer, 1, count - 1) : P2PWire.LeftText;
        if (peer != null)
        {
            Remove(peer);
            PeerDisconnected?.Invoke(peer.Peer, reason);
        }
        else if (!_hosting && from == _hostId)
        {
            // Turned away before being let in.
            _hostId = 0;
            CloseLater(from);
            PeerDisconnected?.Invoke(-1, reason);
        }
    }

    private bool HasMagic(int count)
    {
        if (count < 1 + P2PWire.Magic.Length)
            return false;
        for (int i = 0; i < P2PWire.Magic.Length; i++)
        {
            if (_receiveBuffer[1 + i] != P2PWire.Magic[i])
                return false;
        }
        return true;
    }

    private void SendControl(ulong to, byte kind, bool reliable)
    {
        _sendBuffer[0] = kind;
        int count = 1;
        if (kind == P2PWire.Hello)
        {
            Buffer.BlockCopy(P2PWire.Magic, 0, _sendBuffer, 1, P2PWire.Magic.Length);
            count += P2PWire.Magic.Length;
        }
        SendRaw(to, count, reliable);
    }

    private void SendBye(ulong to, string reason)
    {
        _sendBuffer[0] = P2PWire.Bye;
        int count = 1;
        if (!string.IsNullOrEmpty(reason))
        {
            if (reason.Length > 400)
                reason = reason.Substring(0, 400);
            count += Encoding.UTF8.GetBytes(reason, 0, reason.Length, _sendBuffer, 1);
        }
        SendRaw(to, count, true);
    }

    // The network's callbacks come from its own pump; they are queued and handled at the start of Poll.
    private void OnSessionRequested(ulong id) => Queue(EventKind.Requested, id, 0, null);

    private void OnSessionFailed(ulong id, string reason) => Queue(EventKind.Failed, id, 0, reason);

    private void Queue(EventKind kind, ulong id, int peer, string text) =>
        _events.Add(new PendingEvent { Kind = kind, Id = id, Peer = peer, Text = text });

    private void HandleEvents()
    {
        if (_events.Count == 0)
            return;
        _handling.Clear();
        _handling.AddRange(_events);
        _events.Clear();
        foreach (var e in _handling)
        {
            switch (e.Kind)
            {
                case EventKind.Requested:
                    var admit = Admit;
                    if (_running && ((_hosting && (admit == null || admit(e.Id))) || e.Id == _hostId))
                        _net.Accept(e.Id);
                    else if (_running && _hosting)
                        Defer(e.Id);
                    break;
                case EventKind.Failed:
                    Lost(e.Id, string.IsNullOrEmpty(e.Text) ? "The connection failed." : e.Text);
                    break;
                case EventKind.Disconnected:
                    PeerDisconnected?.Invoke(e.Peer, e.Text);
                    break;
            }
        }
        _handling.Clear();
    }

    // The network gave up on someone: a connected peer is gone, a pending connect failed.
    private void Lost(ulong id, string reason)
    {
        var peer = Find(id);
        if (peer != null)
        {
            Remove(peer);
            PeerDisconnected?.Invoke(peer.Peer, reason);
        }
        else if (_running && !_hosting && id == _hostId)
        {
            _hostId = 0;
            PeerDisconnected?.Invoke(-1, reason);
        }
    }

    private enum EventKind : byte
    {
        Requested,
        Failed,
        Disconnected,
    }

    private struct PendingEvent
    {
        public EventKind Kind;
        public ulong Id;
        public int Peer;
        public string Text;
    }
}
