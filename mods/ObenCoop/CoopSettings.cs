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
}
