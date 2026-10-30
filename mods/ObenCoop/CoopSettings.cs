using System.Collections.Generic;
using System.ComponentModel;
using ObenCoop.Net.Session;
using ObenUIKit.Menu.Settings;

namespace ObenCoop;

/// <summary>Stored in Mods/ObenCoop/config.json.</summary>
public sealed class CoopSettings
{
    /// <summary>What the other players see. Empty uses "Player" (or "Player 2" in a second test copy).</summary>
    [Description("The name other players see. Empty uses \"Player\", or \"Player 2\" in a second test copy.")]
    public string PlayerName = "";

    /// <summary>The UDP port a hosted game listens on. The host has to forward it on their router.</summary>
    [Description("The UDP port a hosted game listens on. The host needs to forward it on their router.")]
    public int Port = Protocol.DefaultPort;

    /// <summary>Players in a hosted game, the host included.</summary>
    [Description("The most players in a hosted game, the host included.")]
    public int MaxPlayers = 4;

    /// <summary>
    /// Mods left out of the "same mods" check. Tools like the dev bridge don't change the game for
    /// anyone else, so they shouldn't stop a join.
    /// </summary>
    [Description("Mods left out of the same-mods check when joining. Tools like the dev bridge don't change the game for others, so they don't block a join.")]
    public List<string> IgnoredMods = new List<string> { "DevBridge", "ScriptRunner" };

    /// <summary>The name a hosted game shows in lobby lists. Empty uses "&lt;your name&gt;'s game".</summary>
    [Description("The name a hosted game shows in lobby lists. Empty names it after you, like \"Anna's game\".")]
    public string LobbyName = "";

    /// <summary>
    /// Your public IPv4 address, for join codes that work over the internet. Empty puts this machine's
    /// address on the local network in codes. "coop invite &lt;ip&gt;" does the same for one code.
    /// </summary>
    [Description("Your public IPv4 address, for join codes that work over the internet. Empty puts this machine's local network address in codes. The coop invite command does the same for one code.")]
    public string PublicAddress = "";

    /// <summary>How many ports, from Port up, a lobby list asks (so a game on the next port is found too).</summary>
    [Description("How many ports, starting at Port, a lobby list checks. This also finds games on the next ports.")]
    public int ScanPorts = 4;

    /// <summary>Offer Steam lobbies when the game's Steam is running. Direct play works either way.</summary>
    [Description("Offer Steam lobbies when the game's Steam is running. Direct play works either way.")]
    public bool Steam = true;

    /// <summary>
    /// Which Steam app the game starts as: "obenseuer", or "spacewar" (Valve's test app 480, which every
    /// Steam account has: copies from GOG and other stores can play over Steam, and testing needs no
    /// second copy of the game). Everyone in a game needs the same. Takes effect when the game starts.
    /// </summary>
    [Setting("Steam app", Choices = new[] { "obenseuer", "spacewar" },
        Description = "Which Steam app the game runs as: obenseuer, or spacewar (Valve's free test app 480, so copies from GOG and other stores can play over Steam). Everyone needs the same. While on spacewar, Steam shows you in Spacewar and Obenseuer's achievements and play time don't count. Takes effect when the game starts.")]
    public string SteamApp = "obenseuer";

    /// <summary>Who sees a hosted Steam lobby: "friends", "public" or "private" (invite only).</summary>
    [Setting("Steam lobby type", Choices = new[] { "friends", "public", "private" },
        Description = "Who can see a hosted Steam lobby: friends, public or private (invite only).")]
    public string SteamLobbyType = "friends";

    /// <summary>Accepting a Steam invite (or "Join game" on a friend) joins at once.</summary>
    [Description("Accepting a Steam invite, or Join game on a friend, joins the game at once.")]
    public bool JoinInvites = true;

    /// <summary>
    /// Streamer mode: join codes, addresses and Steam ids show as stars in co-op's notices, console answers
    /// and window. Copy join code still copies the real code, and the window's Show reveals it.
    /// </summary>
    [Setting("Hide join codes",
        Description = "Streamer mode: join codes, IP addresses and Steam ids show as stars on screen. Copy join code in the Co-op window still copies the real code, and Show there reveals it until the window closes.")]
    public bool HideJoinCodes = true;

    /// <summary>Share where you are with the other players (presence). Others can't see you without it.</summary>
    [Description("Share where you are with the other players. Others can't see you without it.")]
    public bool SharePresence = true;
}
