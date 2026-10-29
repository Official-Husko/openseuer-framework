using System;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using Xunit;

namespace ObenCoop.Net.Tests;

public class RejectionTests
{
    private static SessionEnd JoinExpectingEnd(Node host, Node joiner, params Node[] others)
    {
        joiner.Session.Join(Rig.Address(host));
        var pumps = new System.Collections.Generic.List<object> { host, joiner };
        pumps.AddRange(others);
        Rig.Until(() => joiner.Ends.Count > 0, "joiner is turned away", pumps.ToArray());
        return joiner.Ends[0];
    }

    [Fact]
    public void APeerSendingAnOversizedMessageIsDropped()
    {
        var host = new DirectTransport();
        host.Host(0, 4);
        int received = 0;
        host.PacketReceived += (peer, data) => received++;

        // A bare LiteNetLib client, so ObenCoop's own send limit doesn't stop it.
        var listener = new LiteNetLib.EventBasedNetListener();
        bool dropped = false;
        listener.PeerDisconnectedEvent += (peer, info) => dropped = true;
        var raw = new LiteNetLib.NetManager(listener) { IPv6Enabled = false };
        try
        {
            raw.Start();
            var server = raw.Connect("127.0.0.1", host.Port, DirectTransport.ConnectionKey);
            Action pumpRaw = () => raw.PollEvents();
            Rig.Until(() => server.ConnectionState == LiteNetLib.ConnectionState.Connected, "raw client connected", host, pumpRaw);

            server.Send(new byte[TransportLimits.MaxReliableBytes + 1], LiteNetLib.DeliveryMethod.ReliableOrdered);
            Rig.Until(() => dropped, "raw client dropped", host, pumpRaw);
            Assert.Equal(0, received);
        }
        finally
        {
            raw.Stop();
            host.Stop();
        }
    }

    [Fact]
    public void DifferentGameVersionIsRejectedWithTheReason()
    {
        using var host = Rig.StartHost();
        using var client = new Node("C");
        client.Options.GameVersion = "0.9";

        var end = JoinExpectingEnd(host, client);

        Assert.Equal(EndKind.Rejected, end.Kind);
        Assert.Equal(RejectReason.GameVersion, end.Reject);
        Assert.Contains("1.0", end.Message);
        Assert.Contains("0.9", end.Message);
        Assert.Equal(SessionState.Offline, client.Session.State);
        Assert.Equal(1, host.Session.Roster.Count);
        Assert.Empty(host.Joined);
    }

    [Fact]
    public void DifferentModSetIsRejectedAndNamesTheMods()
    {
        var hostOptions = new SessionOptions
        {
            Mods = new ModSet(new[] { new ModEntry("ObenCoop", "0.2.0"), new ModEntry("Fancy", "1.0.0") }),
        };
        using var host = Rig.StartHost(options: hostOptions);
        using var client = new Node("C");
        client.Options.Mods = new ModSet(new[] { new ModEntry("ObenCoop", "0.1.0"), new ModEntry("Extra", "1.0.0") });

        var end = JoinExpectingEnd(host, client);

        Assert.Equal(RejectReason.ModSet, end.Reject);
        Assert.Contains("Fancy", end.Message);
        Assert.Contains("Extra", end.Message);
        Assert.Contains("ObenCoop (host 0.2.0, you 0.1.0)", end.Message);
    }

    [Fact]
    public void DifferentProtocolVersionIsRejectedBeforeAnythingElseIsRead()
    {
        using var host = Rig.StartHost();
        string reason = null;
        var raw = new DirectTransport();
        raw.PeerConnected += peer =>
        {
            // Only the kind byte and a wrong protocol number: nothing that follows may matter.
            var hello = new MessageWriter();
            hello.WriteByte(1);
            hello.WriteInt32(Protocol.Version + 1);
            raw.Send(peer, hello.Buffer, 0, hello.Length, DeliveryMode.ReliableOrdered);
        };
        raw.PeerDisconnected += (peer, why) => reason = why;
        try
        {
            raw.Connect("127.0.0.1", host.Transport.Port);
            Rig.Until(() => reason != null, "protocol rejection", host, raw);

            Assert.True(RejectText.TryParse(reason, out var code, out string message));
            Assert.Equal(RejectReason.ProtocolVersion, code);
            Assert.Contains("protocol", message);
            Assert.Equal(1, host.Session.Roster.Count);
        }
        finally
        {
            raw.Stop();
        }
    }

    [Fact]
    public void FullGameRejectsTheNextPlayerButKeepsTheOthers()
    {
        using var host = Rig.StartHost(maxPlayers: 2);
        using var first = Rig.JoinedClient(host, "First");
        using var second = new Node("Second");

        var end = JoinExpectingEnd(host, second, first);

        Assert.Equal(RejectReason.Full, end.Reject);
        Assert.Equal(SessionState.Joined, first.Session.State);
        Assert.Equal(2, host.Session.Roster.Count);
    }

    [Fact]
    public void SamePlayerIdTwiceIsRejected()
    {
        using var host = Rig.StartHost();
        using var first = Rig.JoinedClient(host, "First");
        using var twin = new Node("Twin");
        twin.Options.PlayerId = first.Options.PlayerId;

        var end = JoinExpectingEnd(host, twin, first);

        Assert.Equal(RejectReason.DuplicatePlayer, end.Reject);
        Assert.Equal(2, host.Session.Roster.Count);
    }

    [Fact]
    public void ConnectionThatNeverSaysHelloIsDropped()
    {
        var options = new SessionOptions { HandshakeTimeoutMs = 150 };
        using var host = Rig.StartHost(options: options);
        string reason = null;
        var raw = new DirectTransport();
        raw.PeerDisconnected += (peer, why) => reason = why;
        try
        {
            raw.Connect("127.0.0.1", host.Transport.Port);
            Rig.Until(() => reason != null, "silent peer is dropped", host, raw);

            Assert.True(RejectText.TryParse(reason, out var code, out _));
            Assert.Equal(RejectReason.BadHello, code);
        }
        finally
        {
            raw.Stop();
        }
    }

    [Fact]
    public void NobodyListeningEndsWithConnectFailed()
    {
        using var client = new Node("C");
        client.Session.Join("127.0.0.1:1");

        Rig.Until(() => client.Ends.Count > 0, "connect failure", client);

        Assert.Equal(EndKind.ConnectFailed, client.Ends[0].Kind);
        Assert.Equal(SessionState.Offline, client.Session.State);
    }

    [Fact]
    public void BadAddressThrowsAndLeavesTheSessionOffline()
    {
        using var client = new Node("C");
        Assert.Throws<ArgumentException>(() => client.Session.Join("host:notaport"));
        Assert.Equal(SessionState.Offline, client.Session.State);
    }
}
