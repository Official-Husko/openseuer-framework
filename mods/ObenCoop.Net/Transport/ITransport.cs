using System;

namespace ObenCoop.Net.Transport;

/// <summary>Called for every received packet. The segment is only valid during the call; copy what you keep.</summary>
public delegate void PacketHandler(int peer, ArraySegment<byte> data);

/// <summary>
/// Moves bytes between this game and the others. Direct UDP (<see cref="DirectTransport"/>) comes
/// first; a Steam transport can slot in behind the same interface later. A transport knows nothing
/// about players or channels: peers are plain ints it chooses, and it only has to deliver whole
/// packets, reliable ones in order. All events are raised from <see cref="Poll"/> on the caller's
/// thread, never from a socket thread.
/// </summary>
public interface ITransport
{
    /// <summary>True while hosting or connected (or connecting).</summary>
    bool IsRunning { get; }

    /// <summary>Raised when a connection is up: on the host for each new client, on a client once for the host.</summary>
    event Action<int> PeerConnected;

    /// <summary>Raised when a connection ended. The text says why, in words a player can read.</summary>
    event Action<int, string> PeerDisconnected;

    event PacketHandler PacketReceived;

    /// <summary>Starts listening. <paramref name="maxPeers"/> counts the others, not this game.</summary>
    /// <exception cref="InvalidOperationException">The port couldn't be opened.</exception>
    void Host(int port, int maxPeers);

    /// <summary>Starts connecting. Success shows up as <see cref="PeerConnected"/>, failure as <see cref="PeerDisconnected"/>.</summary>
    void Connect(string host, int port);

    /// <summary>Delivers queued events. Call once per frame.</summary>
    void Poll();

    /// <summary>Sends one packet. False when the peer is unknown or the packet is over the size limit.</summary>
    bool Send(int peer, byte[] data, int offset, int count, DeliveryMode mode);

    /// <summary>Sends to every connected peer. Returns how many it went to.</summary>
    int SendToAll(byte[] data, int offset, int count, DeliveryMode mode);

    /// <summary>Closes one connection; the other side sees <paramref name="reason"/>.</summary>
    void DisconnectPeer(int peer, string reason);

    /// <summary>Round trip in milliseconds, or -1 for an unknown peer.</summary>
    int PingOf(int peer);

    TransportStats Stats { get; }

    /// <summary>Closes everything and stops listening. Safe to call twice.</summary>
    void Stop();
}
