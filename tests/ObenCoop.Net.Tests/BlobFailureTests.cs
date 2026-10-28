using ObenCoop.Net.Blobs;
using Xunit;

namespace ObenCoop.Net.Tests;

public class BlobFailureTests
{
    [Fact]
    public void AWrongCrcIsCaughtAndNothingIsHandedOver()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var log = BlobRig.Listen(client, BlobRig.Files);
        host.Session.Blobs.TamperCrc = 0xDEADBEEF;

        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Sample(500 * 1024));
        Rig.Until(() => send.IsFinished && log.Failed.Count == 1, "crc failure", host, client);

        Assert.Equal(BlobFailure.Corrupt, send.Failure);
        Assert.Equal(BlobFailure.Corrupt, log.Failed[0].Failure);
        Assert.Contains("CRC-32", log.Failed[0].Message);
        Assert.Contains("CRC-32", send.Message);
        Assert.Empty(log.Received);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADamagedChunkIsCaught(bool compressible)
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var log = BlobRig.Listen(client, BlobRig.Files);
        int chunks = 0;
        host.Session.Blobs.TamperChunk = (buffer, offset, count) =>
        {
            if (chunks++ == 2)
                buffer[offset + count / 2] ^= 0x5A;
        };

        var data = compressible ? BlobRig.Sample(800 * 1024) : BlobRig.Noise(800 * 1024);
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, data);
        Rig.Until(() => send.IsFinished && log.Failed.Count == 1, "damage found", host, client);

        Assert.Equal(BlobFailure.Corrupt, send.Failure);
        Assert.Empty(log.Received);
    }

    [Fact]
    public void AClientLeavingMidTransferFailsBothSides()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        host.Options.Blobs.WindowChunks = 2;
        var log = BlobRig.Listen(client, BlobRig.Files);
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Noise(5 * 1024 * 1024));

        Rig.Until(() => log.Progress.Count > 3, "mid transfer", host, client);
        var receiving = client.Session.Blobs.IncomingCount;
        Assert.Equal(1, receiving);
        client.Session.Leave();

        Assert.Single(log.Failed);
        Assert.Equal(BlobFailure.SessionEnded, log.Failed[0].Failure);
        Rig.Until(() => send.IsFinished, "sender noticed", host, client);

        Assert.Equal(BlobFailure.PeerLeft, send.Failure);
        Assert.Contains("C", send.Message);
        Assert.Equal(0, host.Session.Blobs.OutgoingCount);
        Assert.Equal(0, client.Session.Blobs.IncomingCount);
        Assert.Empty(log.Received);
    }

    [Fact]
    public void TheHostClosingMidTransferFailsBothSides()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        host.Options.Blobs.WindowChunks = 2;
        var log = BlobRig.Listen(client, BlobRig.Files);
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Noise(5 * 1024 * 1024));

        Rig.Until(() => log.Progress.Count > 3, "mid transfer", host, client);
        host.Session.Leave();

        Assert.Equal(BlobFailure.SessionEnded, send.Failure);
        Rig.Until(() => log.Failed.Count == 1, "receiver noticed", host, client);

        Assert.Equal(BlobFailure.SessionEnded, log.Failed[0].Failure);
        Assert.Empty(log.Received);
    }

    [Fact]
    public void ASilentPeerTimesTheTransferOutAndTheOtherSideLearnsOnceItWakesUp()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        host.Options.Blobs.StallTimeoutMs = 300;
        var log = BlobRig.Listen(client, BlobRig.Files);
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Sample(200 * 1024));

        // The client is never polled, so nothing is acknowledged.
        Rig.Until(() => send.IsFinished, "timeout", host);

        Assert.Equal(BlobFailure.TimedOut, send.Failure);
        Rig.Until(() => log.Failed.Count == 1, "client catches up", host, client);
        Assert.Equal(BlobFailure.TimedOut, log.Failed[0].Failure);
        Assert.Empty(log.Received);
    }

    [Fact]
    public void ATotalTimeLimitEndsASlowTransfer()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Sample(200 * 1024), "", null, null, 200);

        Rig.Until(() => send.IsFinished, "deadline", host);

        Assert.Equal(BlobFailure.TimedOut, send.Failure);
    }

    [Fact]
    public void ARegisteredReceiverIsRequired()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");

        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, new byte[100]);
        Rig.Until(() => send.IsFinished, "refusal", host, client);

        Assert.Equal(BlobFailure.NoHandler, send.Failure);
        Assert.Contains("channel 20", send.Message);
    }

    [Fact]
    public void ABlobOverTheLimitIsRefusedBySenderAndReceiver()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        var log = BlobRig.Listen(client, BlobRig.Files);
        int finishedCalls = 0;

        host.Options.Blobs.MaxBlobBytes = 1024;
        var local = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, new byte[2000], "", null, s => finishedCalls++);
        Assert.Equal(BlobFailure.TooLarge, local.Failure);
        Assert.Equal(1, finishedCalls);
        Assert.Equal(0, host.Session.Blobs.OutgoingCount);

        host.Options.Blobs.MaxBlobBytes = 1 << 20;
        client.Options.Blobs.MaxBlobBytes = 1024;
        var remote = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, new byte[2000]);
        Rig.Until(() => remote.IsFinished, "remote refusal", host, client);

        Assert.Equal(BlobFailure.TooLarge, remote.Failure);
        Assert.Empty(log.Failed);
        Assert.Equal(0, client.Session.Blobs.IncomingCount);
    }

    [Fact]
    public void TooManyTransfersAtOnceAreTurnedAwayOnBothSides()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        BlobRig.Listen(client, BlobRig.Files);
        host.Options.Blobs.MaxOutgoing = 1;
        client.Options.Blobs.MaxIncoming = 1;
        var to = BlobRig.Remote(host, client);

        var first = host.Session.Blobs.Send(to, BlobRig.Files, BlobRig.Sample(100 * 1024));
        var second = host.Session.Blobs.Send(to, BlobRig.Files, BlobRig.Sample(100 * 1024));
        Assert.Equal(BlobFailure.Busy, second.Failure);
        Rig.Until(() => first.IsFinished, "first", host, client);
        Assert.True(first.Succeeded);

        host.Options.Blobs.MaxOutgoing = 4;
        var a = host.Session.Blobs.Send(to, BlobRig.Files, BlobRig.Noise(2 * 1024 * 1024));
        var b = host.Session.Blobs.Send(to, BlobRig.Files, BlobRig.Noise(2 * 1024 * 1024));
        Rig.Until(() => a.IsFinished && b.IsFinished, "both settled", host, client);

        Assert.True(a.Succeeded ^ b.Succeeded, "one fits, the other is told the receiver is busy");
        Assert.Equal(BlobFailure.Busy, a.Succeeded ? b.Failure : a.Failure);
    }

    [Fact]
    public void SendingToSomeoneWhoIsNotThereFailsAtOnce()
    {
        using var host = Rig.StartHost();
        var send = host.Session.Blobs.Send(null, BlobRig.Files, new byte[10]);
        Assert.Equal(BlobFailure.NotConnected, send.Failure);

        using var client = Rig.JoinedClient(host, "C");
        var toSelf = host.Session.Blobs.Send(host.Session.Roster.Local, BlobRig.Files, new byte[10]);
        Assert.Equal(BlobFailure.NotConnected, toSelf.Failure);
    }
}
