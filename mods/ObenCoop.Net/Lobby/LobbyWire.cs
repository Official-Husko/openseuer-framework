using System;
using ObenCoop.Net.Messaging;

namespace ObenCoop.Net.Lobby;

/// <summary>
/// The two packets of direct-play lobby lookups, sent outside any connection: a query ("who is
/// hosting?"), broadcast on the local network or sent to one address, and the host's reply with its
/// <see cref="LobbyInfo"/>. Both start with four magic bytes so stray datagrams are ignored.
/// </summary>
public static class LobbyWire
{
    private static readonly byte[] QueryMagic = { (byte)'O', (byte)'B', (byte)'C', (byte)'Q' };
    private static readonly byte[] ReplyMagic = { (byte)'O', (byte)'B', (byte)'C', (byte)'R' };
    private const byte Version = 1;

    /// <summary>The query packet. The same bytes every time.</summary>
    public static byte[] Query { get; } = { QueryMagic[0], QueryMagic[1], QueryMagic[2], QueryMagic[3], Version };

    public static bool IsQuery(byte[] data, int offset, int count) =>
        count >= Query.Length && StartsWith(data, offset, QueryMagic);

    public static byte[] Reply(LobbyInfo info)
    {
        var writer = new MessageWriter(160);
        writer.WriteBytes(ReplyMagic, 0, ReplyMagic.Length);
        writer.WriteByte(Version);
        info.Write(writer);
        var bytes = new byte[writer.Length];
        Buffer.BlockCopy(writer.Buffer, 0, bytes, 0, writer.Length);
        return bytes;
    }

    public static bool TryReadReply(byte[] data, int offset, int count, out LobbyInfo info)
    {
        info = null;
        if (count < ReplyMagic.Length + 1 || !StartsWith(data, offset, ReplyMagic))
            return false;
        // The reply's version byte is for later; LobbyInfo carries its own format and the protocol.
        var reader = new MessageReader().Init(data, offset + ReplyMagic.Length + 1, count - ReplyMagic.Length - 1);
        return LobbyInfo.TryRead(reader, out info);
    }

    private static bool StartsWith(byte[] data, int offset, byte[] magic)
    {
        for (int i = 0; i < magic.Length; i++)
        {
            if (data[offset + i] != magic[i])
                return false;
        }
        return true;
    }
}
