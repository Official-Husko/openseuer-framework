using System;
using System.Threading.Tasks;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Blobs;

/// <summary>Which end of a transfer this game is.</summary>
public enum BlobDirection
{
    Sending,
    Receiving,
}

/// <summary>Where a transfer is.</summary>
public enum BlobState
{
    /// <summary>Sending: compressing the data on a worker thread.</summary>
    Preparing,

    /// <summary>Sending: offered to the receiver, waiting for it to accept.</summary>
    Offered,

    /// <summary>Chunks are moving.</summary>
    Transferring,

    /// <summary>Everything arrived; unpacking and checking it (receiver), or waiting for that check (sender).</summary>
    Verifying,

    Done,
    Failed,
}

/// <summary>Why a transfer failed. <see cref="BlobStatus.Message"/> has the sentence for a player.</summary>
public enum BlobFailure
{
    None = 0,

    /// <summary>One side cancelled.</summary>
    Cancelled = 1,

    /// <summary>No progress for too long, or the overall time limit passed.</summary>
    TimedOut = 2,

    /// <summary>The other player left or the connection to them dropped.</summary>
    PeerLeft = 3,

    /// <summary>This game left the session.</summary>
    SessionEnded = 4,

    /// <summary>The receiver has no handler for the channel.</summary>
    NoHandler = 5,

    /// <summary>The payload is over the size limit of this side or the receiver.</summary>
    TooLarge = 6,

    /// <summary>Too many transfers at once, or too much memory held by them.</summary>
    Busy = 7,

    /// <summary>The data arrived damaged: it didn't unpack, or its size or CRC-32 was wrong.</summary>
    Corrupt = 8,

    /// <summary>The other side sent something that doesn't fit the protocol.</summary>
    Protocol = 9,

    /// <summary>Not in a session, or the target isn't a player in it.</summary>
    NotConnected = 10,

    /// <summary>Something failed inside this game (compression, memory).</summary>
    Internal = 11,
}

/// <summary>
/// One transfer, as seen from this game: the object <see cref="BlobTransfer.Send"/> returns and the
/// one receivers get in their callbacks. The same instance is updated as the transfer goes on, so it
/// can be kept and read later. Everything is updated from <see cref="CoopSession.Poll"/>.
/// </summary>
public sealed class BlobStatus
{
    private readonly BlobTransfer _owner;

    internal BlobStatus(BlobTransfer owner, BlobDirection direction, int id, CoopPlayer peer, byte channel, string name)
    {
        _owner = owner;
        Direction = direction;
        Id = id;
        Peer = peer;
        Channel = channel;
        Name = name ?? "";
    }

    /// <summary>The sender's number for the transfer; unique among that sender's transfers.</summary>
    public int Id { get; }

    public BlobDirection Direction { get; }

    /// <summary>The other end: the receiver when sending, the sender when receiving.</summary>
    public CoopPlayer Peer { get; }

    /// <summary>The blob channel the data is for.</summary>
    public byte Channel { get; }

    /// <summary>A label the sender chose, for the receiver to tell blobs on one channel apart.</summary>
    public string Name { get; }

    /// <summary>Size of the data itself, before compression.</summary>
    public long BytesTotal { get; internal set; }

    /// <summary>
    /// How much of <see cref="BytesTotal"/> has been acknowledged (sending) or received (receiving),
    /// in proportion to the chunks, so it reaches the total exactly when the last chunk does.
    /// </summary>
    public long BytesDone { get; internal set; }

    /// <summary>Size of what actually travels: the compressed data.</summary>
    public long WireBytes { get; internal set; }

    public BlobState State { get; internal set; }

    public BlobFailure Failure { get; internal set; }

    /// <summary>For a failed transfer, a sentence saying what went wrong.</summary>
    public string Message { get; internal set; } = "";

    /// <summary>0 to 1.</summary>
    public float Progress => BytesTotal <= 0 ? (State == BlobState.Done ? 1f : 0f) : (float)BytesDone / BytesTotal;

    public bool IsFinished => State == BlobState.Done || State == BlobState.Failed;

    public bool Succeeded => State == BlobState.Done;

    /// <summary>Stops the transfer on both sides. False when it had already finished.</summary>
    public bool Cancel() => _owner.Cancel(this);

    public override string ToString() => $"blob {Id} ({Direction}, {State}, {BytesDone}/{BytesTotal})";

    // Sender: the bytes that travel, and how far they have got.
    internal byte[] Wire;
    internal int WireLength;
    internal bool Compressed;
    internal uint Crc;
    internal int ChunkCount;
    internal int ChunkSize;
    internal int Window;
    internal int AckEvery;
    internal int ChunksSent;

    // Both: chunks acknowledged (sender) or received (receiver).
    internal int ChunksDone;
    internal long LastProgress;
    internal long Deadline;

    // Worker thread job: compressing (sender) or unpacking and checking (receiver).
    internal Task Work;
    internal long Reserved;

    // Sender: the caller's callbacks. Receiver: the listener that takes the blob.
    internal Action<BlobStatus> OnProgress;
    internal Action<BlobStatus> OnFinished;
    internal BlobListener Listener;
}
