using System;
using ObenCoop.Net.Blobs;
using Xunit;

namespace ObenCoop.Net.Tests;

public class BlobAllocationTests
{
    [Fact]
    public void ChunksAreSentAndReceivedWithoutPerChunkAllocations()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        BlobRig.Listen(client, BlobRig.Files);
        var send = host.Session.Blobs.Send(BlobRig.Remote(host, client), BlobRig.Files, BlobRig.Noise(8 * 1024 * 1024));

        // Everything runs on this thread, so the thread's allocation counter covers both ends,
        // the transport included. Counting starts once the offer was accepted and chunks flow.
        long before = 0;
        bool counting = false;
        Rig.Until(() =>
        {
            if (!counting && send.State == BlobState.Transferring)
            {
                counting = true;
                before = GC.GetAllocatedBytesForCurrentThread();
            }
            return send.IsFinished;
        }, "8 MB blob", host, client);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(send.Succeeded);
        long chunks = (send.WireBytes + 16 * 1024 - 1) / (16 * 1024);
        Assert.True(chunks > 400);
        // A 16 KB chunk is 12 network packets; what is left is the transport's own bookkeeping.
        Assert.True(allocated / chunks < 1024, $"{allocated / chunks} bytes allocated per chunk");
    }
}
