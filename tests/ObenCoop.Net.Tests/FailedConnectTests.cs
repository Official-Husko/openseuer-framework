using System;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using Xunit;

namespace ObenCoop.Net.Tests;

public class FailedConnectTests
{
    // Starts its socket, then fails the connect, like LiteNetLib with a host name that doesn't resolve.
    private sealed class FailingTransport : ITransport
    {
        public bool IsRunning { get; private set; }
        public event Action<int> PeerConnected { add { } remove { } }
        public event Action<int, string> PeerDisconnected { add { } remove { } }
        public event PacketHandler PacketReceived { add { } remove { } }
        public TransportStats Stats => default;
        public void Host(int port, int maxPeers) => IsRunning = true;
        public void Connect(string host, int port)
        {
            IsRunning = true;
            throw new InvalidOperationException("no such host");
        }
        public void Poll() { }
        public bool Send(int peer, byte[] data, int offset, int count, DeliveryMode mode) => false;
        public int SendToAll(byte[] data, int offset, int count, DeliveryMode mode) => 0;
        public void DisconnectPeer(int peer, string reason) { }
        public int QueuedPackets(int peer) => 0;
        public int PingOf(int peer) => -1;
        public void Stop() => IsRunning = false;
    }

    [Fact]
    public void AFailedConnectStopsTheTransport()
    {
        var transport = new FailingTransport();
        var session = new CoopSession(transport, new SessionOptions { PlayerName = "Guest", GameVersion = "1.0" });

        Assert.Throws<InvalidOperationException>(() => session.Join("127.0.0.1:27999"));
        Assert.Equal(SessionState.Offline, session.State);
        Assert.False(transport.IsRunning);
    }
}
