using System.Collections.Generic;
using ObenCoop.Net.Blobs;
using Xunit;

namespace ObenCoop.Net.Tests;

public class BlobFairnessTests
{
    [Fact]
    public void ASmallBlobSentAfterABigOneArrivesFirst()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var log = BlobRig.Listen(client, BlobRig.Files);
        var to = BlobRig.Remote(host, client);
        var finishOrder = new List<string>();

        var big = BlobRig.Noise(8 * 1024 * 1024);
        var small = BlobRig.Sample(2 * 1024, 3);
        var bigSend = host.Session.Blobs.Send(to, BlobRig.Files, big, "big", null, s => finishOrder.Add("big"));
        var smallSend = host.Session.Blobs.Send(to, BlobRig.Files, small, "small", null, s => finishOrder.Add("small"));

        Rig.Until(() => bigSend.IsFinished && smallSend.IsFinished && log.Received.Count == 2, "both blobs", host, client);

        Assert.True(smallSend.Succeeded);
        Assert.True(bigSend.Succeeded);
        Assert.Equal("small", log.Received[0].Blob.Name);
        Assert.Equal("big", log.Received[1].Blob.Name);
        Assert.Equal(small, log.Received[0].Data);
        Assert.Equal(big, log.Received[1].Data);
        Assert.Equal(new List<string> { "small", "big" }, finishOrder);
    }

    [Fact]
    public void AStalledTransferToASilentPlayerDoesNotHoldUpAnother()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B");
        host.Options.Blobs.StallTimeoutMs = 1500;
        var logB = BlobRig.Listen(b, BlobRig.Files);

        // A is never polled again, so nothing it is sent is acknowledged.
        var toA = host.Session.Blobs.Send(BlobRig.Remote(host, a), BlobRig.Files, BlobRig.Sample(200 * 1024, 1), "stalled");
        var data = BlobRig.Sample(300 * 1024, 2);
        var toB = host.Session.Blobs.Send(BlobRig.Remote(host, b), BlobRig.Files, data, "other");

        Rig.Until(() => toB.IsFinished && logB.Received.Count == 1, "B's blob", host, b);

        Assert.True(toB.Succeeded);
        Assert.Equal(data, logB.Received[0].Data);
        Assert.False(toA.IsFinished, "A's transfer should still be waiting, not timed out yet");

        Rig.Until(() => toA.IsFinished, "A's transfer times out", host);
        Assert.Equal(BlobFailure.TimedOut, toA.Failure);
    }
}
