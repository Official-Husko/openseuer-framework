using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;

namespace ObenCoop.Net.Lobby;

/// <summary>
/// Asks for direct-play lobbies without connecting: broadcasts a query on the local network and
/// sends it to this machine's own ports (a second copy of the game answers there even when
/// broadcasts don't loop back), or asks one address. Hosts answer with their <see cref="LobbyInfo"/>.
/// Uses its own UDP socket on a free port, open only while a scan or lookup runs.
/// </summary>
public sealed class LobbyScanner : IDisposable
{
    private readonly Stopwatch _sent = new Stopwatch();
    private NetManager _manager;
    private EventBasedNetListener _listener;

    /// <summary>Raised from <see cref="Poll"/> for every answer: who answered, what they advertise, milliseconds since the last query.</summary>
    public event Action<IPEndPoint, LobbyInfo, int> Answered;

    public bool IsRunning => _manager != null && _manager.IsRunning;

    /// <summary>Broadcasts the query to <paramref name="portCount"/> ports from <paramref name="firstPort"/> on the network and on this machine.</summary>
    public void Broadcast(int firstPort, int portCount)
    {
        Open();
        var query = LobbyWire.Query;
        for (int port = firstPort; port < firstPort + portCount && port < 65536; port++)
        {
            _manager.SendBroadcast(query, port);
            _manager.SendUnconnectedMessage(query, new IPEndPoint(IPAddress.Loopback, port));
        }
        _sent.Restart();
    }

    /// <summary>Asks one host and returns the end point asked. A name is looked up first (that can take a moment).</summary>
    /// <exception cref="ArgumentException">The host can't be found.</exception>
    public IPEndPoint Ask(string host, int port)
    {
        IPAddress address;
        try
        {
            // IPv4 only, like the transport (an IPv6 answer would be no use to an IPv4 socket).
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                address = IPAddress.Loopback;
            else if (!IPAddress.TryParse(host, out address))
                address = NetUtils.ResolveAddress(host, AddressFamily.InterNetwork);
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Can't find '{host}': {e.Message}", nameof(host));
        }
        if (address == null)
            throw new ArgumentException($"Can't find '{host}'.", nameof(host));

        Open();
        var endPoint = new IPEndPoint(address, port);
        _manager.SendUnconnectedMessage(LobbyWire.Query, endPoint);
        _sent.Restart();
        return endPoint;
    }

    public void Poll() => _manager?.PollEvents();

    public void Stop()
    {
        if (_manager == null)
            return;
        _listener.ClearNetworkReceiveUnconnectedEvent();
        _manager.Stop(true);
        _manager = null;
        _listener = null;
    }

    public void Dispose() => Stop();

    private void Open()
    {
        if (IsRunning)
            return;
        Stop();
        _listener = new EventBasedNetListener();
        _listener.NetworkReceiveUnconnectedEvent += OnUnconnected;
        _manager = new NetManager(_listener)
        {
            UnconnectedMessagesEnabled = true,
            AutoRecycle = true,
            UnsyncedEvents = false,
            IPv6Enabled = false,
        };
        if (!_manager.Start())
        {
            Stop();
            throw new InvalidOperationException("Couldn't open a UDP socket to look for games.");
        }
    }

    private void OnUnconnected(IPEndPoint from, NetPacketReader reader, UnconnectedMessageType type)
    {
        if (!LobbyWire.TryReadReply(reader.RawData, reader.UserDataOffset, reader.UserDataSize, out var info))
            return;
        Answered?.Invoke(from, info, (int)Math.Min(_sent.ElapsedMilliseconds, int.MaxValue));
    }
}
