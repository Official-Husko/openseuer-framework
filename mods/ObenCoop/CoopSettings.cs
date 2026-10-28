using System.Collections.Generic;
using ObenCoop.Net.Session;

namespace ObenCoop;

/// <summary>Stored in Mods/ObenCoop/config.json.</summary>
public sealed class CoopSettings
{
    /// <summary>What the other players see. Empty uses "Player" (or "Player 2" in a second test copy).</summary>
    public string PlayerName = "";

    /// <summary>The UDP port a hosted game listens on. The host has to forward it on their router.</summary>
    public int Port = Protocol.DefaultPort;

    /// <summary>Players in a hosted game, the host included.</summary>
    public int MaxPlayers = 4;

    /// <summary>
    /// Mods left out of the "same mods" check. Tools like the dev bridge don't change the game for
    /// anyone else, so they shouldn't stop a join.
    /// </summary>
    public List<string> IgnoredMods = new List<string> { "DevBridge", "ScriptRunner" };

    /// <summary>The name a hosted game shows in lobby lists. Empty uses "&lt;your name&gt;'s game".</summary>
    public string LobbyName = "";

    /// <summary>
    /// Your public IPv4 address, for join codes that work over the internet. Empty puts this machine's
    /// address on the local network in codes. "coop invite &lt;ip&gt;" does the same for one code.
    /// </summary>
    public string PublicAddress = "";

    /// <summary>How many ports, from Port up, a lobby list asks (so a game on the next port is found too).</summary>
    public int ScanPorts = 4;

    /// <summary>Offer Steam lobbies when the game's Steam is running. Direct play works either way.</summary>
    public bool Steam = true;

    /// <summary>Who sees a hosted Steam lobby: "friends", "public" or "private" (invite only).</summary>
    public string SteamLobbyType = "friends";

    /// <summary>Accepting a Steam invite (or "Join game" on a friend) joins at once.</summary>
    public bool JoinInvites = true;

    /// <summary>Share where you are with the other players (presence). Others can't see you without it.</summary>
    public bool SharePresence = true;
}
