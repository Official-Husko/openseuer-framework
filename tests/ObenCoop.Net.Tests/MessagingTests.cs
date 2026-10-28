using System.Collections.Generic;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using Xunit;

namespace ObenCoop.Net.Tests;

public class MessagingTests
{
    private const byte Numbers = ChannelIds.FirstFree;
    private const byte Other = ChannelIds.FirstFree + 1;

    private static List<(Player From, int Value)> Collect(Node node, byte channel)
    {
        var received = new List<(Player, int)>();
        node.Session.Router.Register(channel, (from, reader) => received.Add((from, reader.ReadInt32())));
        return received;
    }

    private static void SendNumber(Node node, byte channel, int value, DeliveryMode mode = DeliveryMode.ReliableOrdered, Player to = null)
    {
        var writer = MessagePool.RentWriter();
        writer.WriteInt32(value);
        if (to != null)
            node.Session.SendTo(to, channel, writer, mode);
        else
            node.Session.Broadcast(channel, writer, mode);
        MessagePool.Return(writer);
    }

    [Fact]
    public void ReliableMessagesArriveOnceAndInOrderBothWays()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var atHost = Collect(host, Numbers);
        var atClient = Collect(client, Numbers);

        for (int i = 0; i < 300; i++)
        {
            SendNumber(client, Numbers, i);
            SendNumber(host, Numbers, 1000 + i);
        }
        Rig.Until(() => atHost.Count == 300 && atClient.Count == 300, "all messages", host, client);
        Rig.Settle(100, host, client);

        Assert.Equal(300, atHost.Count);
        Assert.Equal(300, atClient.Count);
        for (int i = 0; i < 300; i++)
        {
            Assert.Equal(i, atHost[i].Value);
            Assert.Equal(1000 + i, atClient[i].Value);
        }
        Assert.True(atHost[0].From.Slot == 1 && !atHost[0].From.IsHost);
        Assert.True(atClient[0].From.IsHost);
    }

    [Fact]
    public void ChannelsAreSeparate()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var first = Collect(host, Numbers);
        var second = Collect(host, Other);

        SendNumber(client, Numbers, 1);
        SendNumber(client, Other, 2);
        SendNumber(client, 200, 3);
        Rig.Until(() => first.Count == 1 && second.Count == 1, "both channels", host, client);
        Rig.Settle(50, host, client);

        Assert.Equal(1, first[0].Value);
        Assert.Equal(2, second[0].Value);
        Assert.Equal(1, host.Session.Router.Dropped);
    }

    [Fact]
    public void ClientMessagesAreRelayedToTheOtherClientWithTheRealSender()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        Rig.Until(() => a.Session.Roster.Count == 3, "roster settled", host, a, b);
        var atHost = Collect(host, Numbers);
        var atA = Collect(a, Numbers);
        var atB = Collect(b, Numbers);

        SendNumber(a, Numbers, 42);
        Rig.Until(() => atHost.Count == 1 && atB.Count == 1, "broadcast relayed", host, a, b);
        Rig.Settle(50, host, a, b);

        Assert.Equal("A", atHost[0].From.Name);
        Assert.Equal("A", atB[0].From.Name);
        Assert.Equal(1, atB[0].From.Slot);
        Assert.Empty(atA);
    }

    [Fact]
    public void DirectedMessagesReachOnlyTheirTarget()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        Rig.Until(() => a.Session.Roster.Count == 3, "roster settled", host, a, b);
        var atHost = Collect(host, Numbers);
        var atA = Collect(a, Numbers);
        var atB = Collect(b, Numbers);

        SendNumber(a, Numbers, 7, to: a.Session.Roster.Find(2));
        SendNumber(host, Numbers, 8, to: host.Session.Roster.Find(1));
        Rig.Until(() => atB.Count == 1 && atA.Count == 1, "directed messages", host, a, b);
        Rig.Settle(50, host, a, b);

        Assert.Equal(7, atB[0].Value);
        Assert.Equal("A", atB[0].From.Name);
        Assert.Equal(8, atA[0].Value);
        Assert.True(atA[0].From.IsHost);
        Assert.Empty(atHost);
    }

    [Fact]
    public void SendToHostSkipsTheOtherClients()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        Rig.Until(() => a.Session.Roster.Count == 3, "roster settled", host, a, b);
        var atHost = Collect(host, Numbers);
        var atB = Collect(b, Numbers);

        var writer = MessagePool.RentWriter();
        writer.WriteInt32(5);
        Assert.True(a.Session.SendToHost(Numbers, writer));
        Assert.False(host.Session.SendToHost(Numbers, writer));
        MessagePool.Return(writer);
        Rig.Until(() => atHost.Count == 1, "host got it", host, a, b);
        Rig.Settle(50, host, a, b);

        Assert.Empty(atB);
    }

    [Fact]
    public void UnreliableMessagesGetThroughOnLoopback()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var atHost = Collect(host, Numbers);

        for (int i = 0; i < 20; i++)
            SendNumber(client, Numbers, i, DeliveryMode.Unreliable);
        Rig.Until(() => atHost.Count > 0, "an unreliable message", host, client);
    }

    [Fact]
    public void OversizedUnreliableMessageIsRefusedNotSent()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var writer = MessagePool.RentWriter();
        writer.WriteBytes(new byte[TransportLimits.MaxUnreliableBytes + 1], 0, TransportLimits.MaxUnreliableBytes + 1);

        Assert.False(client.Session.Broadcast(Numbers, writer, DeliveryMode.Unreliable));
        Assert.True(client.Session.Broadcast(Numbers, writer, DeliveryMode.ReliableOrdered));
        MessagePool.Return(writer);
    }

    [Fact]
    public void SendingWhileOfflineReturnsFalse()
    {
        using var node = new Node("Nobody");
        var writer = MessagePool.RentWriter();
        Assert.False(node.Session.Broadcast(Numbers, writer));
        Assert.False(node.Session.SendToHost(Numbers, writer));
        MessagePool.Return(writer);
    }

    [Fact]
    public void ThrowingHandlerIsReportedAndDoesNotStopLaterMessages()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var errors = new List<byte>();
        host.Session.Router.Error += (channel, e) => errors.Add(channel);
        int calls = 0;
        host.Session.Router.Register(Numbers, (from, reader) =>
        {
            calls++;
            if (reader.ReadInt32() == 1)
                throw new System.InvalidOperationException("boom");
        });

        SendNumber(client, Numbers, 1);
        SendNumber(client, Numbers, 2);
        Rig.Until(() => calls == 2, "both handled", host, client);

        Assert.Equal(new[] { Numbers }, errors);
    }
}
