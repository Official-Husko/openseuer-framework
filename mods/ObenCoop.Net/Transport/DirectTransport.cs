using System;
using System.Collections.Generic;
using System.Text;
using LiteNetLib;

namespace ObenCoop.Net.Transport;

/// <summary>
/// Direct UDP through LiteNetLib. The host listens on a port and clients connect to its address;
/// there is no relay or NAT punch-through, so the host has to forward the port. Reliable packets use
/// LiteNetLib's reliable-ordered delivery, unreliable ones its plain unreliable delivery.
/// LiteNetLib.dll is loaded once and never reloaded, so a hot reload can hand the live NetManager
/// to the next copy of this assembly (<see cref="Detach"/>, <see cref="Adopt"/>).
/// </summary>
public sealed partial class DirectTransport : ITransport
{
    /// <summary>Sent with every connection request; connections with another key are turned away by LiteNetLib.</summary>
    public const string ConnectionKey = "ObenCoop";

    private const int ReasonLimitBytes = 400;

    private readonly Dictionary<int, NetPeer> _peers = new Dictionary<int, NetPeer>();
    private NetManager _manager;
    private EventBasedNetListener _listener;
    private int _maxPeers;

    public bool IsRunning => _manager != null && _manager.IsRunning;

    public event Action<int> PeerConnected;
    public event Action<int, string> PeerDisconnected;
    public event PacketHandler PacketReceived;

    /// <summary>Called when LiteNetLib reports a socket level problem. Optional.</summary>
    public Action<string> Log { get; set; }

    /// <summary>The port a hosted game listens on (0 when not hosting).</summary>
    public int Port => IsRunning ? _manager.LocalPort : 0;

    public TransportStats Stats
    {
        get
        {
            var s = _manager?.Statistics;
            return s == null ? default : new TransportStats(s.BytesSent, s.BytesReceived, s.PacketsSent, s.PacketsReceived, s.PacketLoss);
        }
    }

    public void Host(int port, int maxPeers)
    {
        Stop();
        _maxPeers = maxPeers;
        Create();
        if (!_manager.Start(port))
        {
            Stop();
            throw new InvalidOperationException($"Couldn't listen on UDP port {port}. Is another game or program using it?");
        }
    }

    public void Connect(string host, int port)
    {
        Stop();
        Create();
        if (!_manager.Start())
        {
            Stop();
            throw new InvalidOperationException("Couldn't open a UDP socket.");
        }
        _manager.Connect(host, port, ConnectionKey);
    }

    public void Poll() => _manager?.PollEvents();

    public bool Send(int peer, byte[] data, int offset, int count, DeliveryMode mode)
    {
        if (!_peers.TryGetValue(peer, out var target) || !FitsMode(count, mode))
            return false;
        target.Send(data, offset, count, 0, ToLite(mode));
        return true;
    }

    public int SendToAll(byte[] data, int offset, int count, DeliveryMode mode)
    {
        if (!FitsMode(count, mode))
            return 0;
        var method = ToLite(mode);
        int sent = 0;
        foreach (var pair in _peers)
        {
            pair.Value.Send(data, offset, count, 0, method);
            sent++;
        }
        return sent;
    }

    public void DisconnectPeer(int peer, string reason)
    {
        if (!_peers.TryGetValue(peer, out var target))
            return;
        byte[] bytes = Encoding.UTF8.GetBytes(Truncate(reason));
        _manager.DisconnectPeer(target, bytes);
    }

    public int QueuedPackets(int peer) =>
        _peers.TryGetValue(peer, out var target) ? target.GetPacketsCountInReliableQueue(0, true) : 0;

    public int PingOf(int peer) => _peers.TryGetValue(peer, out var target) ? target.RoundTripTime : -1;

    public void Stop()
    {
        if (_manager == null)
            return;
        Unhook(_listener);
        _manager.Stop(true);
        _manager = null;
        _listener = null;
        _peers.Clear();
    }

    private void Create()
    {
        _listener = new EventBasedNetListener();
        _manager = new NetManager(_listener)
        {
            ChannelsCount = 1,
            AutoRecycle = true,
            UnsyncedEvents = false,
            // Dual-mode IPv6 sockets misbehave under Wine; plain IPv4 is the safe choice.
            IPv6Enabled = false,
            // Loading a level can keep the main thread busy for a while.
            DisconnectTimeout = 20000,
            UpdateTime = 15,
            EnableStatistics = true,
            // Lobby lookups arrive outside any connection, sent to this address or broadcast on the network.
            UnconnectedMessagesEnabled = true,
            BroadcastReceiveEnabled = true,
        };
        Attach(_listener);
    }

    private void Attach(EventBasedNetListener listener)
    {
        // Whatever an earlier owner hooked up belongs to code that no longer runs.
        Unhook(listener);
        listener.ConnectionRequestEvent += OnConnectionRequest;
        listener.PeerConnectedEvent += OnPeerConnected;
        listener.PeerDisconnectedEvent += OnPeerDisconnected;
        listener.NetworkReceiveEvent += OnReceive;
        listener.NetworkErrorEvent += OnError;
        listener.NetworkReceiveUnconnectedEvent += OnUnconnected;
    }

    private static void Unhook(EventBasedNetListener listener)
    {
        if (listener == null)
            return;
        listener.ClearConnectionRequestEvent();
        listener.ClearPeerConnectedEvent();
        listener.ClearPeerDisconnectedEvent();
        listener.ClearNetworkReceiveEvent();
        listener.ClearNetworkErrorEvent();
        listener.ClearNetworkReceiveUnconnectedEvent();
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        if (_manager.ConnectedPeersCount >= _maxPeers)
        {
            request.Reject(Encoding.UTF8.GetBytes("The game is full."));
            return;
        }
        request.AcceptIfKey(ConnectionKey);
    }

    private void OnPeerConnected(NetPeer peer)
    {
        _peers[peer.Id] = peer;
        PeerConnected?.Invoke(peer.Id);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
    {
        _peers.Remove(peer.Id);
        PeerDisconnected?.Invoke(peer.Id, Describe(info));
    }

    private void OnReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method) =>
        PacketReceived?.Invoke(peer.Id, new ArraySegment<byte>(reader.RawData, reader.UserDataOffset, reader.UserDataSize));

    private void OnError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError error) =>
        Log?.Invoke($"Network error from {endPoint}: {error}");

    private static bool FitsMode(int count, DeliveryMode mode) =>
        count >= 0 && count <= (mode == DeliveryMode.Unreliable ? TransportLimits.MaxUnreliableBytes : TransportLimits.MaxReliableBytes);

    private static DeliveryMethod ToLite(DeliveryMode mode) =>
        mode == DeliveryMode.Unreliable ? DeliveryMethod.Unreliable : DeliveryMethod.ReliableOrdered;

    private static string Truncate(string reason)
    {
        reason = reason ?? "";
        return reason.Length <= ReasonLimitBytes ? reason : reason.Substring(0, ReasonLimitBytes);
    }

    private static string Describe(DisconnectInfo info)
    {
        var extra = info.AdditionalData;
        if (extra != null && extra.AvailableBytes > 0)
            return Encoding.UTF8.GetString(extra.RawData, extra.Position, extra.AvailableBytes);

        switch (info.Reason)
        {
            case DisconnectReason.ConnectionFailed: return "Nobody answered at that address.";
            case DisconnectReason.Timeout: return "The connection timed out.";
            case DisconnectReason.ConnectionRejected: return "The host turned the connection down.";
            case DisconnectReason.RemoteConnectionClose: return "The other side closed the game or left.";
            case DisconnectReason.DisconnectPeerCalled: return "Left the game.";
            case DisconnectReason.HostUnreachable:
            case DisconnectReason.NetworkUnreachable: return "That address can't be reached.";
            default: return info.Reason.ToString();
        }
    }
}
