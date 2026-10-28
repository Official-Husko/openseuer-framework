using ObenCoop.Net.Session;

namespace ObenCoop.Net.Lobby;

/// <summary>Whether this game can join a lobby, from what the lobby advertises.</summary>
public enum LobbyVerdict
{
    Ok,

    /// <summary>A different ObenCoop network protocol: the two can't talk at all.</summary>
    ProtocolVersion,

    /// <summary>A different game version.</summary>
    GameVersion,

    /// <summary>
    /// The mod fingerprints differ. Joining still goes ahead, because the host's handshake then names
    /// exactly which mods are missing or different; a list shows the mark so nobody is surprised.
    /// </summary>
    ModSet,

    Full,

    /// <summary>The host isn't taking new players.</summary>
    Closed,
}

/// <summary>
/// The lobby-level version check, in the same order as the session handshake (protocol, game version,
/// mods, room). It runs before connecting, from the lobby's advertised info, so a mismatch is
/// reported without a connection; the handshake stays the real gate.
/// </summary>
public static class LobbyCheck
{
    public static LobbyVerdict Check(LobbyInfo lobby, SessionOptions local, out string message)
    {
        message = null;
        if (lobby.Unreadable || lobby.Protocol != Protocol.Version)
        {
            message = $"That game runs a different ObenCoop network protocol ({lobby.Protocol}, you have {Protocol.Version}). Both players need the same ObenCoop version.";
            return LobbyVerdict.ProtocolVersion;
        }
        if (lobby.GameVersion != local.GameVersion)
        {
            message = $"That game runs Obenseuer {Shown(lobby.GameVersion)}, you run {local.GameVersion}.";
            return LobbyVerdict.GameVersion;
        }
        if (lobby.IsFull)
        {
            message = "The game is full.";
            return LobbyVerdict.Full;
        }
        if (!lobby.Open)
        {
            message = "The host isn't letting anyone in right now.";
            return LobbyVerdict.Closed;
        }
        var mods = local.Mods ?? ModSet.Empty;
        if (lobby.ModsFingerprint != mods.Fingerprint)
        {
            message = $"That game has different mods ({lobby.ModCount} that count, you have {mods.Entries.Count}).";
            return LobbyVerdict.ModSet;
        }
        return LobbyVerdict.Ok;
    }

    /// <summary>True when a join should go ahead: the lobby fits, or only the mods differ (the host will say how).</summary>
    public static bool CanTry(LobbyVerdict verdict) => verdict == LobbyVerdict.Ok || verdict == LobbyVerdict.ModSet;

    /// <summary>A short mark for lists: empty when fine.</summary>
    public static string Mark(LobbyVerdict verdict) => verdict switch
    {
        LobbyVerdict.Ok => "",
        LobbyVerdict.ProtocolVersion => "other ObenCoop",
        LobbyVerdict.GameVersion => "other game version",
        LobbyVerdict.ModSet => "different mods",
        LobbyVerdict.Full => "full",
        LobbyVerdict.Closed => "closed",
        _ => verdict.ToString(),
    };

    private static string Shown(string text) => string.IsNullOrEmpty(text) ? "(unknown)" : LobbyInfo.Clean(text, 32);
}
