namespace ObenCoop.Net.Lobby;

/// <summary>How players find a game and get into it.</summary>
public enum LobbyKind : byte
{
    None = 0,

    /// <summary>Direct UDP: an address and port, join codes, and a lobby list for the local network.</summary>
    Direct = 1,

    /// <summary>Steam lobbies and Steam's peer-to-peer networking. Only when the game's Steam is up.</summary>
    Steam = 2,
}

/// <summary>Where this game is with its lobby.</summary>
public enum LobbyStatus
{
    Offline,

    /// <summary>Creating a lobby, or looking one up and connecting to it.</summary>
    Pending,
    Hosting,
    Joined,
}
