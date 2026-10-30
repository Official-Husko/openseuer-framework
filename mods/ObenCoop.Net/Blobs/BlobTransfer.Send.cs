using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Threading.Tasks;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Blobs;

// The sending end: Send, the per-poll pump that fills the window, and the receiver's answers.
public sealed partial class BlobTransfer
{
    private readonly List<BlobStatus> _pumpOutgoing = new List<BlobStatus>();

    // Smallest chunk worth sending, and what a chunk packet needs besides its data.
    private const int MinChunkBytes = 256;
    private const int ChunkOverhead = BlobWire.ChunkHeader + BlobWire.EnvelopeBytes;

    /// <summary>
    /// Sends <paramref name="data"/> to a player on a blob channel. Returns at once with the transfer's
    /// <see cref="BlobStatus"/>: compressing happens on a worker thread and the rest in later polls.
    /// <paramref name="data"/> must not be changed until the transfer has finished. If the transfer
    /// can't even start (not connected, too large, too many at once) the returned status is already
    /// failed, and <paramref name="finished"/> has been called.
    /// </summary>
    /// <param name="target">The receiving player; not the local player.</param>
    /// <param name="channel">Which listener on the receiving side gets it.</param>
    /// <param name="data">The payload, up to <see cref="BlobOptions.MaxBlobBytes"/>.</param>
    /// <param name="name">A short label (up to 100 characters) the receiver sees; optional.</param>
    /// <param name="progress">Called, in a poll, whenever more of the data has been acknowledged.</param>
    /// <param name="finished">Called once, in a poll, when the transfer is done or has failed (check the status).</param>
    /// <param name="timeoutMs">Fail the whole transfer after this long; 0 for no limit besides the stall timeout.</param>
    public BlobStatus Send(CoopPlayer target, byte channel, byte[] data, string name = "",
        Action<BlobStatus> progress = null, Action<BlobStatus> finished = null, int timeoutMs = 0) =>
        Send(target, channel, "", data, name, progress, finished, timeoutMs);

    /// <summary>The same on a named channel (<see cref="ChannelNames"/>): the receiver's listener for that name gets it.</summary>
    /// <exception cref="ArgumentException">The channel name isn't valid, or the label is too long.</exception>
    public BlobStatus Send(CoopPlayer target, string channel, byte[] data, string name = "",
        Action<BlobStatus> progress = null, Action<BlobStatus> finished = null, int timeoutMs = 0) =>
        Send(target, ChannelNames.Key(channel), channel, data, name, progress, finished, timeoutMs);

    private BlobStatus Send(CoopPlayer target, uint key, string channelName, byte[] data, string name,
        Action<BlobStatus> progress, Action<BlobStatus> finished, int timeoutMs)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (name != null && name.Length > MaxNameLength)
            throw new ArgumentException($"A blob name can be at most {MaxNameLength} characters.", nameof(name));

        var options = _session.Options.Blobs;
        var blob = new BlobStatus(this, BlobDirection.Sending, ++_lastId, target, key, channelName, name)
        {
            BytesTotal = data.Length,
            OnProgress = progress,
            OnFinished = finished,
        };

        if (target == null || target.IsLocal || !_session.IsActive || _session.Roster.Find(target.Slot) != target)
        {
            Fail(blob, BlobFailure.NotConnected, "That player isn't in the session.", false);
            return blob;
        }
        if (data.Length > options.MaxBlobBytes)
        {
            Fail(blob, BlobFailure.TooLarge, $"{Size(data.Length)} is over the limit of {Size(options.MaxBlobBytes)} for one transfer.", false);
            return blob;
        }
        if (_outgoing.Count >= options.MaxOutgoing)
        {
            Fail(blob, BlobFailure.Busy, $"Already sending {_outgoing.Count} things; wait for one to finish.", false);
            return blob;
        }

        long now = _session.Options.Clock();
        blob.ChunkSize = Math.Max(MinChunkBytes, Math.Min(options.ChunkBytes, TransportLimits.MaxReliableBytes - ChunkOverhead - 16));
        blob.Window = Math.Max(1, options.WindowChunks);
        blob.LastProgress = now;
        blob.Deadline = timeoutMs > 0 ? now + timeoutMs : 0;
        blob.State = BlobState.Preparing;
        _outgoing.Add(blob);

        var level = options.Compression;
        blob.Work = Task.Run(() => Prepare(data, level));
        return blob;
    }

    // Worker thread: the CRC over the original data and the compressed form.
    private static Prepared Prepare(byte[] data, CompressionLevel level)
    {
        var result = new Prepared { Crc = BlobCodec.Crc32(data, 0, data.Length) };
        result.Compressed = BlobCodec.TryCompress(data, level, out result.Wire, out result.Length);
        return result;
    }

    private sealed class Prepared
    {
        public uint Crc;
        public bool Compressed;
        public byte[] Wire;
        public int Length;
    }

    private void PumpOutgoing(long now)
    {
        _pumpOutgoing.Clear();
        _pumpOutgoing.AddRange(_outgoing);
        for (int i = 0; i < _pumpOutgoing.Count; i++)
        {
            var blob = _pumpOutgoing[i];
            if (!blob.IsFinished)
                Advance(blob, now);
        }
    }

    private void Advance(BlobStatus blob, long now)
    {
        if (blob.Deadline > 0 && now > blob.Deadline)
        {
            Fail(blob, BlobFailure.TimedOut, "The transfer took longer than the time allowed.", true);
            return;
        }

        if (blob.State == BlobState.Preparing)
        {
            if (blob.Work.IsCompleted)
                Offer(blob, now);
            return;
        }

        int stall = _session.Options.Blobs.StallTimeoutMs;
        if (stall > 0 && now - blob.LastProgress > stall)
        {
            Fail(blob, BlobFailure.TimedOut,
                $"The other player stopped answering ({Size(blob.BytesDone)} of {Size(blob.BytesTotal)} went through).", true);
            return;
        }
        if (blob.State == BlobState.Transferring)
            SendChunks(blob);
    }

    // The data is ready: tell the receiver what is coming.
    private void Offer(BlobStatus blob, long now)
    {
        var work = (Task<Prepared>)blob.Work;
        blob.Work = null;
        if (work.IsFaulted || work.IsCanceled)
        {
            Fail(blob, BlobFailure.Internal, "Couldn't prepare the data: " + (work.Exception?.GetBaseException().Message ?? "cancelled"), false);
            return;
        }

        var prepared = work.Result;
        blob.Wire = prepared.Wire;
        blob.WireLength = prepared.Length;
        blob.WireBytes = prepared.Length;
        blob.Compressed = prepared.Compressed;
        blob.Crc = prepared.Crc;
        blob.ChunkCount = (prepared.Length + blob.ChunkSize - 1) / blob.ChunkSize;

        var packet = BlobWire.Start(BlobKind.Open, blob.Id);
        BlobWire.WriteOpen(packet, blob, blob.Crc ^ TamperCrc);
        if (!Transmit(blob.Peer, packet))
        {
            Fail(blob, BlobFailure.NotConnected, "Couldn't reach that player.", false);
            return;
        }
        blob.State = BlobState.Offered;
        blob.LastProgress = now;
        _session.Log($"Offered {blob}: {Size(blob.BytesTotal)} as {Size(blob.WireBytes)} in {blob.ChunkCount} chunk(s) to {blob.Peer}.");
    }

    // Fills the window. Chunks go out as ordinary reliable messages, one pooled writer reused each time.
    private void SendChunks(BlobStatus blob)
    {
        while (blob.ChunksSent < blob.ChunkCount && blob.ChunksSent - blob.ChunksDone < blob.Window && _session.HasSendRoom(blob.Peer))
        {
            int offset = blob.ChunksSent * blob.ChunkSize;
            int count = Math.Min(blob.ChunkSize, blob.WireLength - offset);
            var packet = BlobWire.Start(BlobKind.Chunk, blob.Id);
            packet.WriteInt32(blob.ChunksSent);
            int dataAt = packet.Length;
            packet.WriteBytes(blob.Wire, offset, count);
            TamperChunk?.Invoke(packet.Buffer, dataAt, count);
            if (!Transmit(blob.Peer, packet))
                return;
            blob.ChunksSent++;
        }
    }

    private BlobStatus FindOutgoing(CoopPlayer from, int id)
    {
        for (int i = 0; i < _outgoing.Count; i++)
        {
            if (_outgoing[i].Id == id && _outgoing[i].Peer == from)
                return _outgoing[i];
        }
        return null;
    }

    private void OnAck(CoopPlayer sender, int id, MessageReader reader)
    {
        var blob = FindOutgoing(sender, id);
        if (blob == null)
            return;
        int received = reader.ReadInt32();
        bool accepting = blob.State == BlobState.Offered;
        if ((!accepting && blob.State != BlobState.Transferring) || received < blob.ChunksDone || received > blob.ChunksSent || (accepting && received != 0))
        {
            Fail(blob, BlobFailure.Protocol, "The other player sent an acknowledgement that doesn't fit.", true);
            return;
        }

        blob.State = BlobState.Transferring;
        blob.ChunksDone = received;
        blob.LastProgress = _session.Options.Clock();
        blob.BytesDone = Scaled(blob);
        if (received == blob.ChunkCount)
            blob.State = BlobState.Verifying;
        Invoke(blob.OnProgress, blob);
        if (blob.State == BlobState.Transferring)
            SendChunks(blob);
    }

    private void OnDone(CoopPlayer sender, int id)
    {
        var blob = FindOutgoing(sender, id);
        if (blob == null)
            return;
        if (blob.State != BlobState.Verifying)
        {
            Fail(blob, BlobFailure.Protocol, "The other player said it was done before everything was sent.", true);
            return;
        }
        Release(blob);
        blob.BytesDone = blob.BytesTotal;
        blob.State = BlobState.Done;
        _session.Log($"{blob} arrived and checked out.");
        Invoke(blob.OnProgress, blob);
        Invoke(blob.OnFinished, blob);
    }

    private void OnAbortByReceiver(CoopPlayer sender, int id, MessageReader reader)
    {
        var blob = FindOutgoing(sender, id);
        if (blob == null)
            return;
        var failure = ReadFailure(reader, out string text);
        Fail(blob, failure, text, false);
    }

    private static long Scaled(BlobStatus blob) =>
        blob.ChunkCount == 0 ? blob.BytesTotal : blob.BytesTotal * blob.ChunksDone / blob.ChunkCount;

    private static BlobFailure ReadFailure(MessageReader reader, out string text)
    {
        byte code = reader.ReadByte();
        text = reader.ReadString();
        var failure = code >= 1 && code <= (byte)BlobFailure.Internal ? (BlobFailure)code : BlobFailure.Protocol;
        if (text.Length == 0)
            text = "The other player stopped the transfer.";
        return failure;
    }
}
