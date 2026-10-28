using System.IO.Compression;

namespace ObenCoop.Net.Blobs;

/// <summary>
/// Limits and pacing of <see cref="BlobTransfer"/>. Read when a transfer starts (sender) or is
/// offered (receiver), so changing a value affects the next transfer, not the ones running.
/// </summary>
public sealed class BlobOptions
{
    /// <summary>Largest payload, before compression, that is sent or accepted.</summary>
    public int MaxBlobBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>
    /// Bytes of compressed data in one chunk. Chunks travel as ordinary reliable messages, so this
    /// has to leave room for the envelope under <see cref="Transport.TransportLimits.MaxReliableBytes"/>;
    /// the transport splits it into network packets itself.
    /// </summary>
    public int ChunkBytes { get; set; } = 16 * 1024;

    /// <summary>
    /// Chunks a sender may have sent but not yet had acknowledged. Together with the chunk size
    /// this is how much a slow receiver can be handed at once.
    /// </summary>
    public int WindowChunks { get; set; } = 16;

    /// <summary>The receiver acknowledges every this many chunks (and at the end). At most half the sender's window is used.</summary>
    public int AckEveryChunks { get; set; } = 4;

    /// <summary>Transfers this game may be sending at once.</summary>
    public int MaxOutgoing { get; set; } = 16;

    /// <summary>Transfers this game may be receiving at once.</summary>
    public int MaxIncoming { get; set; } = 16;

    /// <summary>
    /// Memory the incoming transfers may hold together (their compressed data and the result).
    /// An offer over it is turned down as busy.
    /// </summary>
    public long MaxIncomingBytes { get; set; } = 96L * 1024 * 1024;

    /// <summary>A transfer that makes no progress for this long fails with a timeout. 0 turns it off.</summary>
    public int StallTimeoutMs { get; set; } = 30000;

    /// <summary>Deflate level for outgoing data. Fastest keeps the sending thread short; the data is a one-off.</summary>
    public CompressionLevel Compression { get; set; } = CompressionLevel.Fastest;
}
