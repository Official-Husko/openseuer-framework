using System;
using System.Collections.Generic;
using ObenCoop.Net.Blobs;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

public class NamedChannelTests
{
    private const string Doors = "mymod.doors";

    private static List<(CoopPlayer From, int Value)> Collect(Node node, string channel)
    {
        var received = new List<(CoopPlayer, int)>();
        node.Session.Router.Register(channel, (from, reader) => received.Add((from, reader.ReadInt32())));
        return received;
    }

    private static string[] Seen(List<(CoopPlayer From, int Value)> received)
    {
        var seen = received.ConvertAll(r => r.From.Name + ":" + r.Value).ToArray();
        Array.Sort(seen, StringComparer.Ordinal);
        return seen;
    }

    private static MessageWriter Number(int value)
    {
        var writer = MessagePool.RentWriter();
        writer.WriteInt32(value);
        return writer;
    }

    [Fact]
    public void KeysAreStableAndClearOfNumberedChannels()
    {
        // Pinned: both ends work the key out by themselves, so it may never change between builds.
        Assert.Equal(2352634628u, ChannelNames.Key(Doors));
        Assert.Equal(ChannelNames.Key(Doors), ChannelNames.Key("mymod." + "doors"));
        Assert.NotEqual(ChannelNames.Key(Doors), ChannelNames.Key("mymod.Doors"));
        for (int i = 0; i < 2000; i++)
            Assert.True(ChannelNames.Key("c" + i) >= 256);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("two words")]
    [InlineData("tab\tname")]
    public void BadNamesAreRefused(string name)
    {
        Assert.Throws<ArgumentException>(() => ChannelNames.Key(name));
    }

    [Fact]
    public void LongNamesAreRefused()
    {
        ChannelNames.Key(new string('a', ChannelNames.MaxLength));
        Assert.Throws<ArgumentException>(() => ChannelNames.Key(new string('a', ChannelNames.MaxLength + 1)));
    }

    [Fact]
    public void MessagesArriveByNameEitherWayAndThroughTheHost()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        var atHost = Collect(host, Doors);
        var atA = Collect(a, Doors);
        var atB = Collect(b, Doors);

        var one = Number(1);
        Assert.True(host.Session.Broadcast(Doors, one));
        var two = Number(2);
        Assert.True(a.Session.Broadcast(Doors, two));
        var three = Number(3);
        Assert.True(b.Session.SendTo(BlobRig.Remote(b, a), Doors, three));
        var four = Number(4);
        Assert.True(b.Session.SendToHost(Doors, four));
        foreach (var w in new[] { one, two, three, four })
            MessagePool.Return(w);

        Rig.Until(() => atHost.Count == 2 && atA.Count == 2 && atB.Count == 2, "every message", host, a, b);
        Rig.Settle(100, host, a, b);

        // Messages from different players come over different connections, so only who sent what is fixed.
        Assert.Equal(new[] { "A:2", "B:4" }, Seen(atHost));
        Assert.Equal(new[] { "B:3", "Host:1" }, Seen(atA));
        Assert.Equal(new[] { "A:2", "Host:1" }, Seen(atB));
    }

    [Fact]
    public void ANameNobodyListensOnIsDroppedQuietly()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var errors = new List<Exception>();
        client.Session.Router.Error += (channel, e) => errors.Add(e);
        var atClient = Collect(client, Doors);
        long dropped = client.Session.Router.Dropped;

        var lost = Number(5);
        host.Session.Broadcast("othermod.lights", lost);
        var kept = Number(6);
        host.Session.Broadcast(Doors, kept);
        MessagePool.Return(lost);
        MessagePool.Return(kept);

        Rig.Until(() => atClient.Count == 1, "the known channel's message", host, client);
        Assert.Equal(6, atClient[0].Value);
        Assert.Equal(dropped + 1, client.Session.Router.Dropped);
        Assert.Empty(errors);
    }

    [Fact]
    public void AHandlerThatThrowsIsReportedWithItsName()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var errors = new List<Exception>();
        client.Session.Router.Error += (channel, e) => errors.Add(e);
        client.Session.Router.Register(Doors, (from, reader) => throw new InvalidOperationException("broken"));

        var writer = Number(7);
        host.Session.Broadcast(Doors, writer);
        MessagePool.Return(writer);

        Rig.Until(() => errors.Count == 1, "the handler error", host, client);
        Assert.Contains(Doors, errors[0].Message);
    }

    [Fact]
    public void ANameHasOneHandlerUntilItIsDisposed()
    {
        using var host = Rig.StartHost();
        var first = host.Session.Router.Register(Doors, (from, reader) => { });
        Assert.True(host.Session.Router.IsRegistered(Doors));
        Assert.Throws<InvalidOperationException>(() => host.Session.Router.Register(Doors, (from, reader) => { }));

        first.Dispose();
        Assert.False(host.Session.Router.IsRegistered(Doors));
        using var second = host.Session.Router.Register(Doors, (from, reader) => { });
        // Disposing the old registration again doesn't take the new one away.
        first.Dispose();
        Assert.True(host.Session.Router.IsRegistered(Doors));
    }

    [Fact]
    public void BlobsTravelOnNamedChannelsApartFromNumberedOnes()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var named = new List<(byte[] Data, BlobStatus Blob)>();
        client.Session.Blobs.Register("mymod.save", (from, data, blob) => named.Add((data, blob)));
        var numbered = BlobRig.Listen(client, BlobRig.Files);

        var data = BlobRig.Sample(100 * 1024);
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), "mymod.save", data, "slot 1");
        Rig.Until(() => send.IsFinished && named.Count == 1, "the named blob", host, client);

        Assert.True(send.Succeeded);
        Assert.Equal(data, named[0].Data);
        Assert.Equal("mymod.save", named[0].Blob.ChannelName);
        Assert.Equal("slot 1", named[0].Blob.Name);
        Assert.Equal(0, named[0].Blob.Channel);
        Assert.Empty(numbered.Received);
    }

    [Fact]
    public void ABlobOnANameNobodyListensOnIsRefused()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");

        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), "nobody.here", new byte[2000]);
        Rig.Until(() => send.IsFinished, "the refusal", host, client);

        Assert.Equal(BlobFailure.NoHandler, send.Failure);
        Assert.Contains("mod", send.Message);
    }
}
