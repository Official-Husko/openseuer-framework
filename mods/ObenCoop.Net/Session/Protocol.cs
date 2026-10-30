namespace ObenCoop.Net.Session;

/// <summary>What both sides have to agree on before they talk.</summary>
public static class Protocol
{
    /// <summary>
    /// Raised whenever the handshake or the packet envelope changes shape. The Hello packet always
    /// starts with its kind byte and this number, so a host can refuse a different version before
    /// trying to read anything else.
    /// </summary>
    public const int Version = 2;

    /// <summary>The default UDP port for a hosted game.</summary>
    public const int DefaultPort = 27890;

    /// <summary>Most players in one session, host included.</summary>
    public const int MaxPlayers = 16;

    /// <summary>Longest player name, in characters. Longer names are cut by the host.</summary>
    public const int MaxNameLength = 24;

    /// <summary>Target slot meaning "everyone except me" in a client's data packet.</summary>
    internal const byte AllOthers = 0xFF;
}

/// <summary>The first byte of every session packet.</summary>
internal enum PacketKind : byte
{
    /// <summary>Client to host: protocol, game version, identity, mod set.</summary>
    Hello = 1,

    /// <summary>Host to client: you are in. Carries the client's slot and the roster.</summary>
    Welcome = 2,

    /// <summary>Host to clients: the full roster, sent whenever it changes.</summary>
    Roster = 3,

    /// <summary>A message for a channel, delivered reliably and in order.</summary>
    Data = 4,

    /// <summary>A message for a channel, delivered unreliably.</summary>
    DataUnreliable = 5,
}
