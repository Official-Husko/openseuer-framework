using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Blobs;

// The receiving end: checks an offer, collects chunks in order, acknowledges in windows, then
// unpacks and checks the whole payload on a worker thread and hands it to the listener.
public sealed partial class BlobTransfer
{
    private readonly List<BlobStatus> _pumpIncoming = new List<BlobStatus>();

    // Chunk counts beyond this can't come from a payload under any sane size limit.
    private const int MaxChunkCount = 1 << 20;

    private void OnOpen(CoopPlayer sender, int id, MessageReader reader)
    {
        byte channel = reader.ReadByte();
        byte flags = reader.ReadByte();
        int rawLength = reader.ReadInt32();
        int wireLength = reader.ReadInt32();
        int chunkSize = reader.ReadInt32();
        int window = reader.ReadInt32();
        uint crc = reader.ReadUInt32();
        string name = reader.ReadString();

        long key = Key(sender, id);
        if (_incoming.ContainsKey(key))
            return;

        var options = _session.Options.Blobs;
        bool compressed = (flags & BlobWire.FlagCompressed) != 0;
        var listener = _listeners[channel];

        if (listener == null)
        {
            Refuse(sender, id, BlobFailure.NoHandler, $"Nothing on the other side is listening for blob channel {channel}.");
            return;
        }
        if (rawLength < 0 || wireLength < 0 || window < 1 || chunkSize < 1 || chunkSize > TransportLimits.MaxReliableBytes - ChunkOverhead
            || (!compressed && wireLength != rawLength) || (compressed && wireLength > rawLength + rawLength / 8 + 1024)
            || (chunkSize > 0 && (wireLength + (long)chunkSize - 1) / chunkSize > MaxChunkCount))
        {
            Refuse(sender, id, BlobFailure.Protocol, "The offer doesn't make sense.");
            return;
        }
        if (rawLength > options.MaxBlobBytes)
        {
            Refuse(sender, id, BlobFailure.TooLarge, $"{Size(rawLength)} is over the limit of {Size(options.MaxBlobBytes)} the receiver accepts.");
            return;
        }

        // The compressed bytes are held while they arrive; unpacking needs a second array of the full size.
        long reserve = compressed ? (long)wireLength + rawLength : rawLength;
        if (_incoming.Count >= options.MaxIncoming || _incomingBytes + reserve > options.MaxIncomingBytes)
        {
            Refuse(sender, id, BlobFailure.Busy, "The receiver is busy with other transfers; try again in a moment.");
            return;
        }

        var blob = new BlobStatus(this, BlobDirection.Receiving, id, sender, channel, name)
        {
            BytesTotal = rawLength,
            WireBytes = wireLength,
            Compressed = compressed,
            Crc = crc,
            ChunkSize = chunkSize,
            ChunkCount = (int)((wireLength + (long)chunkSize - 1) / chunkSize),
            Window = window,
            WireLength = wireLength,
            Wire = new byte[wireLength],
            Reserved = reserve,
            Listener = listener,
            State = BlobState.Transferring,
            LastProgress = _session.Options.Clock(),
        };
        blob.AckEvery = Math.Max(1, Math.Min(options.AckEveryChunks, window / 2));
        _incoming[key] = blob;
        _incomingBytes += reserve;
        _session.Log($"Accepted {blob} from {sender}: {Size(rawLength)} as {Size(wireLength)} in {blob.ChunkCount} chunk(s).");

        SendAck(blob);
        Invoke(listener.Progress, blob);
        if (blob.ChunkCount == 0)
            BeginVerify(blob);
    }

    private void OnChunk(CoopPlayer sender, int id, MessageReader reader)
    {
        if (!_incoming.TryGetValue(Key(sender, id), out var blob) || blob.State != BlobState.Transferring)
            return;

        int index = reader.ReadInt32();
        int length = reader.Remaining;
        int offset = index * blob.ChunkSize;
        int expected = index == blob.ChunkCount - 1 ? blob.WireLength - offset : blob.ChunkSize;
        if (index != blob.ChunksDone || index >= blob.ChunkCount || length != expected)
        {
            Fail(blob, BlobFailure.Protocol, $"Chunk {index} came with {length} bytes when chunk {blob.ChunksDone} of {expected} bytes was due.", true);
            return;
        }

        reader.ReadBytes(blob.Wire, offset, length);
        blob.ChunksDone++;
        blob.BytesDone = Scaled(blob);
        blob.LastProgress = _session.Options.Clock();
        if (blob.ChunksDone % blob.AckEvery == 0 || blob.ChunksDone == blob.ChunkCount)
            SendAck(blob);
        Invoke(blob.Listener.Progress, blob);
        if (blob.ChunksDone == blob.ChunkCount && !blob.IsFinished)
            BeginVerify(blob);
    }

    private void OnAbortBySender(CoopPlayer sender, int id, MessageReader reader)
    {
        if (!_incoming.TryGetValue(Key(sender, id), out var blob))
            return;
        var failure = ReadFailure(reader, out string text);
        Fail(blob, failure, text, false);
    }

    private void SendAck(BlobStatus blob)
    {
        var packet = BlobWire.Start(BlobKind.Ack, blob.Id);
        packet.WriteInt32(blob.ChunksDone);
        Transmit(blob.Peer, packet);
    }

    private void Refuse(CoopPlayer sender, int id, BlobFailure failure, string text)
    {
        _session.Log($"Refused blob {id} from {sender} ({failure}): {text}");
        var packet = BlobWire.Start(BlobKind.AbortByReceiver, id);
        packet.WriteByte((byte)failure);
        packet.WriteString(Trim(text));
        Transmit(sender, packet);
    }

    // All chunks are in: unpack and check on a worker thread; PumpIncoming picks up the result.
    private void BeginVerify(BlobStatus blob)
    {
        blob.State = BlobState.Verifying;
        byte[] wire = blob.Wire;
        int wireLength = blob.WireLength;
        int rawLength = (int)blob.BytesTotal;
        bool compressed = blob.Compressed;
        uint crc = blob.Crc;
        blob.Work = Task.Run(() => Verify(wire, wireLength, rawLength, compressed, crc));
    }

    private static Verified Verify(byte[] wire, int wireLength, int rawLength, bool compressed, uint crc)
    {
        try
        {
            byte[] data = wire;
            if (compressed)
            {
                data = new byte[rawLength];
                BlobCodec.Inflate(wire, wireLength, data);
            }
            uint actual = BlobCodec.Crc32(data, 0, rawLength);
            if (actual != crc)
                return new Verified { Error = $"The data arrived damaged: its CRC-32 is {actual:X8}, it should be {crc:X8}." };
            return new Verified { Data = data };
        }
        catch (Exception e) when (e is InvalidDataException || e is IOException || e is OutOfMemoryException)
        {
            return new Verified { Error = "The data arrived damaged and could not be unpacked: " + e.Message };
        }
    }

    private sealed class Verified
    {
        public byte[] Data;
        public string Error;
    }

    private void PumpIncoming(long now)
    {
        _pumpIncoming.Clear();
        _pumpIncoming.AddRange(_incoming.Values);
        int stall = _session.Options.Blobs.StallTimeoutMs;
        for (int i = 0; i < _pumpIncoming.Count; i++)
        {
            var blob = _pumpIncoming[i];
            if (blob.IsFinished)
                continue;
            if (blob.State == BlobState.Verifying)
            {
                if (blob.Work.IsCompleted)
                    Finish(blob);
            }
            else if (stall > 0 && now - blob.LastProgress > stall)
            {
                Fail(blob, BlobFailure.TimedOut,
                    $"The other player stopped sending ({Size(blob.BytesDone)} of {Size(blob.BytesTotal)} arrived).", true);
            }
        }
    }

    private void Finish(BlobStatus blob)
    {
        var work = (Task<Verified>)blob.Work;
        blob.Work = null;
        if (work.IsFaulted || work.IsCanceled)
        {
            Fail(blob, BlobFailure.Internal, "Couldn't unpack the data: " + (work.Exception?.GetBaseException().Message ?? "cancelled"), true);
            return;
        }
        var verified = work.Result;
        if (verified.Error != null)
        {
            Fail(blob, BlobFailure.Corrupt, verified.Error, true);
            return;
        }

        var listener = blob.Listener;
        Release(blob);
        blob.BytesDone = blob.BytesTotal;
        blob.State = BlobState.Done;
        Transmit(blob.Peer, BlobWire.Start(BlobKind.Done, blob.Id));
        _session.Log($"{blob} from {blob.Peer} arrived and checked out.");
        try
        {
            listener.Received(blob.Peer, verified.Data, blob);
        }
        catch (Exception e)
        {
            _session.Log("A blob handler threw: " + e);
        }
    }
}
