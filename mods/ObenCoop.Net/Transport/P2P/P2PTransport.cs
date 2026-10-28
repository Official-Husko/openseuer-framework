using System;
using System.Collections.Generic;
using System.Globalization;

namespace ObenCoop.Net.Transport.P2P;

/// <summary>
/// Connections over a peer-to-peer packet network (<see cref="IP2PNetwork"/>, Steam in the game).
/// That kind of network has sessions but no connect, accept or disconnect of its own, so this adds a
/// small control layer: a client says hello, the host accepts or turns it away (only players it
/// admits, the members of its lobby, and only while there is room), both ping every second, and
/// leaving sends a goodbye with the reason. Silence for <see cref="TimeoutMs"/> ends a connection.
/// Every packet starts with one byte saying what it is; channel data follows the byte unchanged.
/// Split over several files: this one has state and the ITransport calls, Receive handles incoming
/// packets and events, Upkeep the timers, Handover the hot reload.
/// </summary>
public sealed partial class P2PTransport : ITransport
{
    private readonly IP2PNetwork _net;
    private readonly Func<long> _clock;
    private readonly Dictionary<ulong, P2PPeer> _byId = new Dictionary<ulong, P2PPeer>();
    private readonly List<P2PPeer> _peers = new List<P2PPeer>();
    private readonly byte[] _sendBuffer = new byte[TransportLimits.MaxReliableBytes + 1];

    private bool _running;
    private bool _hosting;
    private int _maxPeers;
    private int _nextPeer = 1;
    private ulong _hostId;
    private long _connectStarted;
    private long _bytesSent, _bytesReceived, _packetsSent, _packetsReceived;

    public P2PTransport(IP2PNetwork net, Func<long> clock)
    {
        _net = net ?? throw new ArgumentNullException(nameof(net));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _net.SessionRequested += OnSessionRequested;
        _net.SessionFailed += OnSessionFailed;
    }

    public bool IsRunning => _running;

    /// <summary>
    /// Who may connect to a hosted game (the lobby's members). Null lets everyone in. Checked when a
    /// session is opened, on hello, and again by <see cref="Recheck"/>.
    /// </summary>
    public Func<ulong, bool> Admit { get; set; }

    /// <summary>A connection with no packet for this long is dropped. 20 s, like direct play: loading a level stalls the game.</summary>
    public int TimeoutMs { get; set; } = 20000;

    public int PingIntervalMs { get; set; } = 1000;

    /// <summary>A client gives up when the host doesn't accept within this time.</summary>
    public int ConnectTimeoutMs { get; set; } = 15000;

    /// <summary>How long a session stays open after a goodbye, so the goodbye still gets there.</summary>
    public int CloseDelayMs { get; set; } = 2000;

    public event Action<int> PeerConnected;
    public event Action<int, string> PeerDisconnected;
    public event PacketHandler PacketReceived;

    public TransportStats Stats => new TransportStats(_bytesSent, _bytesReceived, _packetsSent, _packetsReceived, 0);

    /// <summary>The network id behind a peer (a SteamID64), or 0.</summary>
    public ulong IdOf(int peer)
    {
        var target = Find(peer);
        return target != null ? target.Id : 0;
    }

    public void Host(int port, int maxPeers)
    {
        Stop();
        _running = true;
        _hosting = true;
        _maxPeers = maxPeers;
    }

    /// <summary>Connects to the host whose network id is <paramref name="host"/> (a SteamID64 as text). The port isn't used.</summary>
    /// <exception cref="InvalidOperationException">The text is not an id.</exception>
    public void Connect(string host, int port)
    {
        if (!ulong.TryParse(host, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) || id == 0)
            throw new InvalidOperationException($"'{host}' is not a Steam id.");
        Stop();
        _running = true;
        _hostId = id;
        _connectStarted = _clock();
        SendControl(id, P2PWire.Hello, true);
    }

    public bool Send(int peer, byte[] data, int offset, int count, DeliveryMode mode)
    {
        var target = Find(peer);
        return target != null && SendData(target, data, offset, count, mode);
    }

    public int SendToAll(byte[] data, int offset, int count, DeliveryMode mode)
    {
        int sent = 0;
        for (int i = 0; i < _peers.Count; i++)
        {
            if (SendData(_peers[i], data, offset, count, mode))
                sent++;
        }
        return sent;
    }

    public void DisconnectPeer(int peer, string reason)
    {
        var target = Find(peer);
        if (target == null)
            return;
        SendBye(target.Id, reason);
        Remove(target);
        // Like a socket transport, the side that hangs up hears about it too, from the next Poll.
        Queue(EventKind.Disconnected, target.Id, target.Peer, string.IsNullOrEmpty(reason) ? "Left the game." : reason);
    }

    public int QueuedPackets(int peer)
    {
        var target = Find(peer);
        return target != null ? _net.QueuedPackets(target.Id) : 0;
    }

    public int PingOf(int peer)
    {
        var target = Find(peer);
        return target != null ? target.RoundTrip : -1;
    }

    /// <summary>Says goodbye to everyone and stops. No events are raised. Safe to call twice.</summary>
    public void Stop()
    {
        if (!_running)
            return;
        for (int i = 0; i < _peers.Count; i++)
        {
            SendBye(_peers[i].Id, "");
            CloseLater(_peers[i].Id);
        }
        if (_hostId != 0 && Find(_hostId) == null)
        {
            SendBye(_hostId, "");
            CloseLater(_hostId);
        }
        _peers.Clear();
        _byId.Clear();
        _events.Clear();
        _running = false;
        _hosting = false;
        _hostId = 0;
    }

    /// <summary>Drops connected players that <see cref="Admit"/> no longer lets in (they left the lobby).</summary>
    public void Recheck(string reason)
    {
        var admit = Admit;
        if (admit == null || !_hosting)
            return;
        for (int i = _peers.Count - 1; i >= 0; i--)
        {
            if (i < _peers.Count && !admit(_peers[i].Id))
                DisconnectPeer(_peers[i].Peer, reason);
        }
    }

    private bool SendData(P2PPeer target, byte[] data, int offset, int count, DeliveryMode mode)
    {
        bool reliable = mode == DeliveryMode.ReliableOrdered;
        int limit = reliable ? TransportLimits.MaxReliableBytes : TransportLimits.MaxUnreliableBytes;
        if (count < 0 || count > limit)
            return false;
        // Steam's unreliable limit is 1200 bytes with the type byte; the rare packet at the very edge goes reliable.
        if (!reliable && count + 1 > TransportLimits.MaxUnreliableBytes)
            reliable = true;
        _sendBuffer[0] = P2PWire.Data;
        Buffer.BlockCopy(data, offset, _sendBuffer, 1, count);
        return SendRaw(target.Id, count + 1, reliable);
    }

    private bool SendRaw(ulong to, int count, bool reliable)
    {
        if (!_net.Send(to, _sendBuffer, count, reliable))
            return false;
        _bytesSent += count;
        _packetsSent++;
        return true;
    }

    private P2PPeer Find(int peer)
    {
        for (int i = 0; i < _peers.Count; i++)
        {
            if (_peers[i].Peer == peer)
                return _peers[i];
        }
        return null;
    }

    private P2PPeer Find(ulong id) => _byId.TryGetValue(id, out var peer) ? peer : null;

    private P2PPeer Add(ulong id)
    {
        var peer = new P2PPeer(id, _nextPeer++, _clock());
        _peers.Add(peer);
        _byId[id] = peer;
        return peer;
    }

    private void Remove(P2PPeer peer)
    {
        _peers.Remove(peer);
        _byId.Remove(peer.Id);
        CloseLater(peer.Id);
    }

    private sealed class P2PPeer
    {
        public P2PPeer(ulong id, int peer, long now)
        {
            Id = id;
            Peer = peer;
            LastHeard = now;
            LastPing = now;
        }

        public ulong Id { get; }
        public int Peer { get; }
        public long LastHeard { get; set; }
        public long LastPing { get; set; }
        public int RoundTrip { get; set; } = -1;
    }
}
