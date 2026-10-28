using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Lobby;

/// <summary>
/// Where a lobby is: a host and port for direct play, or a Steam lobby id. Players type it as an
/// address (<c>192.168.1.20</c>, <c>host.example:27890</c>), a join code, or <c>steam:&lt;id&gt;</c>.
/// </summary>
public readonly struct LobbyAddress : IEquatable<LobbyAddress>
{
    private const string SteamPrefix = "steam:";

    // Steam ids of lobbies (and people) are far above this; a plain number that big is never a host name.
    private const ulong SmallestSteamId = 1UL << 52;

    private LobbyAddress(LobbyKind kind, string host, int port, ulong lobbyId)
    {
        Kind = kind;
        Host = host;
        Port = port;
        LobbyId = lobbyId;
    }

    public static LobbyAddress Direct(string host, int port) => new LobbyAddress(LobbyKind.Direct, host ?? "", port, 0);

    public static LobbyAddress Steam(ulong lobbyId) => new LobbyAddress(LobbyKind.Steam, "", 0, lobbyId);

    /// <summary>None for the default value.</summary>
    public LobbyKind Kind { get; }

    /// <summary>Host name or IP (direct only).</summary>
    public string Host { get; }

    /// <summary>UDP port (direct only).</summary>
    public int Port { get; }

    /// <summary>Steam lobby id (Steam only).</summary>
    public ulong LobbyId { get; }

    public bool IsValid => Kind != LobbyKind.None;

    /// <summary>The join code for this address: always for Steam, for direct only when the host is an IPv4 address.</summary>
    public bool TryGetJoinCode(out string code)
    {
        code = null;
        if (Kind == LobbyKind.Steam)
            code = JoinCode.ForSteamLobby(LobbyId);
        else if (Kind == LobbyKind.Direct && IsIPv4(Host) && Port > 0 && Port < 65536)
            code = JoinCode.ForAddress(Host, Port);
        return code != null;
    }

    /// <summary>
    /// Reads what a player typed: a join code, <c>steam:&lt;id&gt;</c> (or a bare Steam id), or an address with an
    /// optional port. On failure <paramref name="problem"/> says what is wrong.
    /// </summary>
    public static bool TryParse(string text, int defaultPort, out LobbyAddress address, out string problem)
    {
        address = default;
        problem = null;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            problem = "Give an address (ip or ip:port), a join code or a Steam lobby id.";
            return false;
        }

        if (text.StartsWith(SteamPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (ulong.TryParse(text.Substring(SteamPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out ulong id) && id != 0)
            {
                address = Steam(id);
                return true;
            }
            problem = $"'{text}' is not a Steam lobby id.";
            return false;
        }

        if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong number) && number >= SmallestSteamId)
        {
            address = Steam(number);
            return true;
        }

        // An address has dots or a colon; a code never does.
        if (text.IndexOf('.') < 0 && text.IndexOf(':') < 0 && JoinCode.LooksLikeCode(text))
        {
            if (JoinCode.TryDecode(text, out address))
                return true;
            problem = $"'{text}' looks like a join code but has a typo in it (its check doesn't match).";
            return false;
        }

        if (CoopSession.TryParseAddress(text, defaultPort, out string host, out int port))
        {
            address = Direct(host, port);
            return true;
        }
        problem = $"'{text}' is not an address. Use ip, ip:port, a join code or steam:<lobby id>.";
        return false;
    }

    /// <summary>What <see cref="CoopSession.Join"/> takes for a direct address.</summary>
    public override string ToString() => Kind switch
    {
        LobbyKind.Steam => SteamPrefix + LobbyId.ToString(CultureInfo.InvariantCulture),
        LobbyKind.Direct => Host + ":" + Port.ToString(CultureInfo.InvariantCulture),
        _ => "(none)",
    };

    public bool Equals(LobbyAddress other) =>
        Kind == other.Kind && Port == other.Port && LobbyId == other.LobbyId && string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object obj) => obj is LobbyAddress other && Equals(other);

    public override int GetHashCode() => ((int)Kind * 397) ^ Port ^ LobbyId.GetHashCode() ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Host ?? "");

    /// <summary>A dotted IPv4 address (four numbers; IPAddress alone would also take "1234").</summary>
    public static bool IsIPv4(string host)
    {
        if (string.IsNullOrEmpty(host))
            return false;
        int dots = 0;
        foreach (char c in host)
        {
            if (c == '.')
                dots++;
            else if (c < '0' || c > '9')
                return false;
        }
        return dots == 3 && IPAddress.TryParse(host, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork;
    }
}
