using System;
using System.Collections.Generic;
using ObenCoop.Net.Blobs;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Tests;

/// <summary>What a blob listener saw, for the tests to look at.</summary>
internal sealed class BlobLog
{
    public List<(CoopPlayer From, byte[] Data, BlobStatus Blob)> Received { get; } = new List<(CoopPlayer, byte[], BlobStatus)>();
    public List<BlobStatus> Failed { get; } = new List<BlobStatus>();
    public List<long> Progress { get; } = new List<long>();
}

internal static class BlobRig
{
    public const byte Files = 20;
    public const byte Saves = 21;

    public static BlobLog Listen(Node node, byte channel, Action<BlobStatus> onProgress = null)
    {
        var log = new BlobLog();
        node.Session.Blobs.Register(channel,
            (from, data, blob) => log.Received.Add((from, data, blob)),
            blob =>
            {
                log.Progress.Add(blob.BytesDone);
                onProgress?.Invoke(blob);
            },
            blob => log.Failed.Add(blob));
        return log;
    }

    /// <summary>Data that compresses a little: the first half is noise, the second a repeating pattern.</summary>
    public static byte[] Sample(int size, int seed = 1)
    {
        var data = new byte[size];
        new Random(seed).NextBytes(data);
        for (int i = size / 2; i < size; i++)
            data[i] = (byte)(i % 251);
        return data;
    }

    /// <summary>Pure noise, which deflate can't shrink.</summary>
    public static byte[] Noise(int size, int seed = 7)
    {
        var data = new byte[size];
        new Random(seed).NextBytes(data);
        return data;
    }

    public static CoopPlayer Remote(Node from, Node to) =>
        from.Session.Roster.Find(to.Session.Roster.Local.Slot);
}
