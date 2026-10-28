using System;
using System.Net;
using LiteNetLib;

namespace ObenCoop.Net.Transport;

// Packets outside any connection: lobby lookups. The transport doesn't know what they mean; whoever
// advertises the game (the lobby) sets Responder, which turns a query into an answer.
public sealed partial class DirectTransport
{
    // A reply is bigger than a query, so an open port answering everything could be used to flood
    // someone else with spoofed queries. Real lookups come a few at a time.
    private const int MaxRepliesPerSecond = 32;

    private int _replyWindowStart;
    private int _repliesInWindow;

    /// <summary>
    /// Answers packets that arrive outside a connection: gets the bytes (array, offset, count) and
    /// returns the reply, or null for none. Null (the default) ignores them all.
    /// </summary>
    public Func<byte[], int, int, byte[]> Responder { get; set; }

    /// <summary>Unconnected packets answered since the transport was made.</summary>
    public long Answered { get; private set; }

    private void OnUnconnected(IPEndPoint from, NetPacketReader reader, UnconnectedMessageType type)
    {
        var responder = Responder;
        if (responder == null || _manager == null)
            return;

        byte[] reply = responder(reader.RawData, reader.UserDataOffset, reader.UserDataSize);
        if (reply == null)
            return;

        int now = Environment.TickCount;
        if (now - _replyWindowStart >= 1000)
        {
            _replyWindowStart = now;
            _repliesInWindow = 0;
        }
        if (++_repliesInWindow > MaxRepliesPerSecond)
            return;

        _manager.SendUnconnectedMessage(reply, from);
        Answered++;
    }
}
