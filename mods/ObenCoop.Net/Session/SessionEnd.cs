namespace ObenCoop.Net.Session;

/// <summary>Why a host turns a joining player away.</summary>
public enum RejectReason : byte
{
    None = 0,

    /// <summary>The two games speak different versions of the net protocol.</summary>
    ProtocolVersion = 1,

    /// <summary>The game versions differ.</summary>
    GameVersion = 2,

    /// <summary>The mods differ.</summary>
    ModSet = 3,

    /// <summary>All slots are taken.</summary>
    Full = 4,

    /// <summary>The same player id is already connected (a stale connection, or a second copy).</summary>
    DuplicatePlayer = 5,

    /// <summary>The Hello was damaged or never came.</summary>
    BadHello = 6,

    /// <summary>The same game version, but different game files (a beta branch, another store's build, a patched game).</summary>
    GameBuild = 7,
}

/// <summary>How a session ended for this game.</summary>
public enum EndKind
{
    /// <summary>This game left or stopped hosting.</summary>
    Left,

    /// <summary>The host refused the join; see <see cref="SessionEnd.Reject"/>.</summary>
    Rejected,

    /// <summary>The connection could not be made (nobody answered, bad address).</summary>
    ConnectFailed,

    /// <summary>The connection dropped or the host closed the game.</summary>
    ConnectionLost,
}

/// <summary>The outcome handed to <see cref="CoopSession.Ended"/>.</summary>
public sealed class SessionEnd
{
    public SessionEnd(EndKind kind, RejectReason reject, string message)
    {
        Kind = kind;
        Reject = reject;
        Message = message ?? "";
    }

    public EndKind Kind { get; }

    public RejectReason Reject { get; }

    /// <summary>A sentence for the player.</summary>
    public string Message { get; }

    public override string ToString() => $"{Kind}: {Message}";
}

/// <summary>What a session is doing.</summary>
public enum SessionState
{
    Offline,
    Hosting,

    /// <summary>Connecting to a host or waiting for its answer to the Hello.</summary>
    Connecting,

    /// <summary>The host accepted this game; messages flow.</summary>
    Joined,
}

/// <summary>A reject reason travels inside the transport's disconnect text, as "code:message".</summary>
public static class RejectText
{
    public static string Encode(RejectReason reason, string message) => (int)reason + ":" + message;

    public static bool TryParse(string text, out RejectReason reason, out string message)
    {
        reason = RejectReason.None;
        message = text;
        if (string.IsNullOrEmpty(text))
            return false;
        int colon = text.IndexOf(':');
        if (colon < 1 || colon > 3 || !int.TryParse(text.Substring(0, colon), out int code) || code < 1 || code > (int)RejectReason.GameBuild)
            return false;
        reason = (RejectReason)code;
        message = text.Substring(colon + 1);
        return true;
    }
}
