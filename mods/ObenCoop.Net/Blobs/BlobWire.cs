using ObenCoop.Net.Messaging;

namespace ObenCoop.Net.Blobs;

// Wire format of blob packets. They travel as ordinary reliable messages on ChannelIds.Blob, so the
// session envelope already says who sent them. Every packet starts with a kind byte and the
// transfer id (the sender's number). Changing any of this changes Protocol.Version.
//
//   Open            sender to receiver  channel, flags, size, wire size, chunk size, window, crc, name
//   Chunk           sender to receiver  index, bytes
//   Ack             receiver to sender  chunks received so far (0 means "accepted, go ahead")
//   Done            receiver to sender  the data arrived and checked out
//   AbortBySender   sender to receiver  failure code, text
//   AbortByReceiver receiver to sender  failure code, text (also a refusal of an Open)
internal enum BlobKind : byte
{
    Open = 1,
    Chunk = 2,
    Ack = 3,
    Done = 4,
    AbortBySender = 5,
    AbortByReceiver = 6,
}

internal static class BlobWire
{
    /// <summary>Flag in Open: the chunks hold deflated data; otherwise they hold the data as it is.</summary>
    public const byte FlagCompressed = 1;

    /// <summary>Bytes before the data in a Chunk packet: kind, id, index.</summary>
    public const int ChunkHeader = 1 + 4 + 4;

    /// <summary>Bytes the session adds around a message: kind, channel, slot.</summary>
    public const int EnvelopeBytes = 3;

    public static MessageWriter Start(BlobKind kind, int id)
    {
        var writer = MessagePool.RentWriter();
        writer.WriteByte((byte)kind);
        writer.WriteInt32(id);
        return writer;
    }

    public static void WriteOpen(MessageWriter w, BlobStatus s, uint crc)
    {
        w.WriteByte(s.Channel);
        w.WriteByte(s.Compressed ? FlagCompressed : (byte)0);
        w.WriteInt32((int)s.BytesTotal);
        w.WriteInt32(s.WireLength);
        w.WriteInt32(s.ChunkSize);
        w.WriteInt32(s.Window);
        w.WriteUInt32(crc);
        w.WriteString(s.Name);
    }
}
