using System.Collections.Generic;
using System.IO;
using ObenCoop.Net.Blobs;
using Xunit;

namespace ObenCoop.Net.Tests;

public class BlobTransferTests
{
    [Fact]
    public void FiveMegabytesArriveIntactWithProgressOnBothSides()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var log = BlobRig.Listen(client, BlobRig.Files);
        var data = BlobRig.Sample(5 * 1024 * 1024);

        var sentProgress = new List<long>();
        BlobStatus finished = null;
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, data, "world",
            s => sentProgress.Add(s.BytesDone), s => finished = s);
        Assert.Equal(BlobState.Preparing, send.State);

        Rig.Until(() => finished != null && log.Received.Count == 1, "5 MB blob", host, client);

        Assert.True(finished.Succeeded);
        Assert.Equal(1f, finished.Progress);
        Assert.Equal(data, log.Received[0].Data);
        Assert.True(log.Received[0].From.IsHost);
        Assert.Equal("world", log.Received[0].Blob.Name);
        Assert.Equal(data.Length, log.Received[0].Blob.BytesTotal);
        Assert.True(log.Received[0].Blob.Succeeded);
        Assert.True(send.WireBytes < data.Length, "the repeating half should have compressed");

        // Progress only moves forward and ends at the total, on both sides.
        Assert.True(sentProgress.Count > 10);
        Assert.Equal(data.Length, sentProgress[sentProgress.Count - 1]);
        Assert.Equal(data.Length, log.Progress[log.Progress.Count - 1]);
        for (int i = 1; i < log.Progress.Count; i++)
            Assert.True(log.Progress[i] >= log.Progress[i - 1]);

        Assert.Equal(0, host.Session.Blobs.OutgoingCount);
        Assert.Equal(0, client.Session.Blobs.IncomingCount);
    }

    [Fact]
    public void IncompressibleDataTravelsAsItIsAndClientsCanSendToTheHost()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var log = BlobRig.Listen(host, BlobRig.Saves);
        var data = BlobRig.Noise(300 * 1024);

        var send = client.Session.Blobs.Send(client.Session.Roster.Host, BlobRig.Saves, data);
        Rig.Until(() => send.IsFinished && log.Received.Count == 1, "noise blob", host, client);

        Assert.True(send.Succeeded);
        Assert.Equal(data.Length, send.WireBytes);
        Assert.Equal(data, log.Received[0].Data);
        Assert.Equal("C", log.Received[0].From.Name);
    }

    [Fact]
    public void EmptyAndTinyBlobsWork()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var log = BlobRig.Listen(client, BlobRig.Files);
        var to = BlobRig.Remote(host, client);

        var empty = host.Session.Blobs.Send(to, BlobRig.Files, new byte[0]);
        var one = host.Session.Blobs.Send(to, BlobRig.Files, new byte[] { 42 });
        Rig.Until(() => empty.IsFinished && one.IsFinished && log.Received.Count == 2, "tiny blobs", host, client);

        Assert.True(empty.Succeeded && one.Succeeded);
        Assert.Empty(log.Received[0].Data);
        Assert.Equal(new byte[] { 42 }, log.Received[1].Data);
    }

    [Fact]
    public void TwoTransfersRunAtOnceInBothDirections()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var atClient = BlobRig.Listen(client, BlobRig.Files);
        var atHost = BlobRig.Listen(host, BlobRig.Files);
        var big = BlobRig.Sample(2 * 1024 * 1024, 1);
        var other = BlobRig.Sample(900 * 1024, 2);
        var back = BlobRig.Sample(1200 * 1024, 3);

        var a = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, big, "big");
        var b = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, other, "other");
        var c = client.Session.Blobs.Send(client.Session.Roster.Host, BlobRig.Files, back, "back");
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(2, host.Session.Blobs.OutgoingCount);

        Rig.Until(() => atClient.Received.Count == 2 && atHost.Received.Count == 1 && a.IsFinished && b.IsFinished && c.IsFinished, "all three", host, client);

        Assert.True(a.Succeeded && b.Succeeded && c.Succeeded);
        foreach (var (_, bytes, blob) in atClient.Received)
            Assert.Equal(blob.Name == "big" ? big : other, bytes);
        Assert.Equal(back, atHost.Received[0].Data);
        Assert.Equal(new HashSet<string> { "big", "other" }, new HashSet<string> { atClient.Received[0].Blob.Name, atClient.Received[1].Blob.Name });
    }

    [Fact]
    public void ABlobFromOneClientIsRelayedToAnother()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        Rig.Until(() => a.Session.Roster.Count == 3, "roster settled", host, a, b);
        var log = BlobRig.Listen(b, BlobRig.Files);
        var data = BlobRig.Sample(600 * 1024);

        var send = a.Session.Blobs.Send(a.Session.Roster.Find(b.Session.Roster.Local.Slot), BlobRig.Files, data);
        Rig.Until(() => send.IsFinished && log.Received.Count == 1, "relayed blob", host, a, b);

        Assert.True(send.Succeeded);
        Assert.Equal(data, log.Received[0].Data);
        Assert.Equal("A", log.Received[0].From.Name);
    }

    [Fact]
    public void SenderCancelFailsBothSides()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        host.Options.Blobs.WindowChunks = 2;
        var log = BlobRig.Listen(client, BlobRig.Files);
        BlobStatus finished = null;
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Noise(4 * 1024 * 1024), "x", null, s => finished = s);

        Rig.Until(() => log.Progress.Count > 3, "some chunks", host, client);
        Assert.True(send.Cancel());
        Assert.False(send.Cancel());
        Assert.Equal(BlobState.Failed, send.State);
        Assert.Equal(BlobFailure.Cancelled, send.Failure);
        Assert.Same(send, finished);

        Rig.Until(() => log.Failed.Count == 1, "receiver told", host, client);
        Rig.Settle(100, host, client);

        Assert.Equal(BlobFailure.Cancelled, log.Failed[0].Failure);
        Assert.Empty(log.Received);
        Assert.Equal(0, client.Session.Blobs.IncomingCount);
        Assert.Equal(0, host.Session.Blobs.OutgoingCount);
    }

    [Fact]
    public void ReceiverCanCancelFromItsOwnProgressCallback()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        host.Options.Blobs.WindowChunks = 2;
        var log = BlobRig.Listen(client, BlobRig.Files, blob =>
        {
            if (blob.BytesDone > 0)
                blob.Cancel();
        });
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Noise(3 * 1024 * 1024));

        Rig.Until(() => send.IsFinished && log.Failed.Count == 1, "both cancelled", host, client);

        Assert.Equal(BlobFailure.Cancelled, send.Failure);
        Assert.Equal(BlobFailure.Cancelled, log.Failed[0].Failure);
        Assert.Empty(log.Received);
    }

    [Fact]
    public void DisposingTheListenerCancelsBlobsOnItsChannel()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        host.Options.Blobs.WindowChunks = 2;
        var received = new List<byte[]>();
        var registration = client.Session.Blobs.Register(BlobRig.Files, (from, data, blob) => received.Add(data));
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Noise(3 * 1024 * 1024));

        Rig.Until(() => client.Session.Blobs.IncomingCount == 1, "arriving", host, client);
        registration.Dispose();
        Rig.Until(() => send.IsFinished, "sender told", host, client);

        Assert.Equal(BlobFailure.NoHandler, send.Failure);
        Assert.Empty(received);
        Assert.False(client.Session.Blobs.IsRegistered(BlobRig.Files));
    }

    [Fact]
    public void ARegisteredChannelCannotBeTakenTwice()
    {
        using var node = new Node("Solo");
        node.Session.Blobs.Register(BlobRig.Files, (from, data, blob) => { });
        Assert.Throws<System.InvalidOperationException>(() => node.Session.Blobs.Register(BlobRig.Files, (from, data, blob) => { }));
    }

    [Fact]
    public void Crc32MatchesTheStandardCheckValue()
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes("123456789");
        Assert.Equal(0xCBF43926u, BlobCodec.Crc32(bytes, 0, bytes.Length));
    }

    [Fact]
    public void InflateRefusesDataOfTheWrongLength()
    {
        var data = BlobRig.Sample(10000);
        Assert.True(BlobCodec.TryCompress(data, System.IO.Compression.CompressionLevel.Fastest, out var wire, out int length));

        var exact = new byte[data.Length];
        BlobCodec.Inflate(wire, length, exact);
        Assert.Equal(data, exact);
        Assert.Throws<InvalidDataException>(() => BlobCodec.Inflate(wire, length, new byte[data.Length + 1]));
        Assert.Throws<InvalidDataException>(() => BlobCodec.Inflate(wire, length, new byte[data.Length - 1]));
    }
}
