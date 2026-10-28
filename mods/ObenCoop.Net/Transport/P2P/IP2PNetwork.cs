using System;

namespace ObenCoop.Net.Transport.P2P;

/// <summary>
/// A packet network addressed by 64-bit ids, shaped after Steam's peer-to-peer calls
/// (<c>ISteamNetworking</c>: SendP2PPacket, ReadP2PPacket, AcceptP2PSessionWithUser...). The mod
/// implements it on Steamworks; the tests implement it in memory. <see cref="P2PTransport"/> builds a
/// connection-style <see cref="ITransport"/> on top, so nothing above it knows about Steam.
/// Events may be raised from the platform's callback pump (on the main thread, but outside
/// <see cref="ITransport.Poll"/>); the transport queues them.
/// </summary>
public interface IP2PNetwork
{
    /// <summary>This player's id on the network (a SteamID64). 0 when not signed in.</summary>
    ulong LocalId { get; }

    /// <summary>Sends one packet of <paramref name="count"/> bytes from the start of <paramref name="data"/>.</summary>
    bool Send(ulong to, byte[] data, int count, bool reliable);

    /// <summary>
    /// Takes the next waiting packet into <paramref name="buffer"/>. Returns its length, or -1 when none
    /// is waiting. A packet bigger than the buffer is thrown away and reported as length 0.
    /// </summary>
    int Receive(byte[] buffer, out ulong from);

    /// <summary>Lets packets from someone who opened a session with us through.</summary>
    void Accept(ulong peer);

    /// <summary>Ends the session with someone; what they still had on its way is lost.</summary>
    void Close(ulong peer);

    /// <summary>Reliable packets still waiting to go out to someone.</summary>
    int QueuedPackets(ulong peer);

    /// <summary>Raised when someone opens a session with us. Accept it with <see cref="Accept"/>, or ignore it.</summary>
    event Action<ulong> SessionRequested;

    /// <summary>Raised when a session failed (the other side went away or can't be reached). The text says why.</summary>
    event Action<ulong, string> SessionFailed;
}
