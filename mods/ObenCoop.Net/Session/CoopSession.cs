using System;
using System.Collections.Generic;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Session;

/// <summary>
/// One co-op session on top of a transport: the host listens, clients join, a handshake checks that
/// both sides run the same protocol, game version and mods, the host hands out slots and keeps the
/// roster, and messages travel on channels. Clients only talk to the host; the host passes on what
/// the others need (a star, not a mesh). Everything is driven by <see cref="Poll"/> and every event
/// is raised from it, on the caller's thread.
/// Split over several files: this one has state and the public lifecycle, Host and Client handle the
/// two roles' packets, Data sends and receives channel messages, Persistence carries a session over a
/// hot reload.
/// </summary>
public sealed partial class CoopSession
{
    // A few connections beyond the player limit so that a joiner who finds the game full still gets
    // the "full" answer instead of a bare refusal.
    private const int PendingSlack = 4;

    private readonly ITransport _transport;
    private readonly List<PendingPeer> _pending = new List<PendingPeer>();

    private int _hostPeer = -1;
    private bool _transportConnected;
    private long _helloSentAt;

    public CoopSession(ITransport transport, SessionOptions options)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _transport.PeerConnected += OnPeerConnected;
        _transport.PeerDisconnected += OnPeerDisconnected;
        _transport.PacketReceived += OnPacket;
    }

    public SessionOptions Options { get; }

    public SessionState State { get; private set; }

    public Roster Roster { get; } = new Roster();

    public MessageRouter Router { get; } = new MessageRouter();

    public bool IsHost => State == SessionState.Hosting;

    /// <summary>True when messages can be sent: hosting, or joined to a host.</summary>
    public bool IsActive => State == SessionState.Hosting || State == SessionState.Joined;

    public TransportStats Stats => _transport.Stats;

    /// <summary>Raised when this game is part of a session: the host is listening, or the host accepted the join.</summary>
    public event Action Started;

    /// <summary>Raised when another player is in the roster, on every game. On joining, once per player already there.</summary>
    public event Action<Player> PlayerJoined;

    /// <summary>Raised when another player has gone. The text says why.</summary>
    public event Action<Player, string> PlayerLeft;

    /// <summary>Raised once when the session ends for this game, for any reason, including <see cref="Leave"/>.</summary>
    public event Action<SessionEnd> Ended;

    /// <summary>Starts hosting on a UDP port. The local player takes slot 0.</summary>
    /// <exception cref="InvalidOperationException">The port couldn't be opened.</exception>
    public void Host(int port)
    {
        int max = Options.MaxPlayers;
        if (max < 2 || max > Protocol.MaxPlayers)
            throw new InvalidOperationException($"Max players must be between 2 and {Protocol.MaxPlayers}.");

        Leave();
        _transport.Host(port, max - 1 + PendingSlack);
        Roster.Add(new Player(Player.HostSlot, Options.PlayerId, CleanName(Options.PlayerName), true, -1));
        State = SessionState.Hosting;
        Log($"Hosting on UDP port {port} for up to {max} players.");
        Raise(Started);
    }

    /// <summary>
    /// Starts joining a host given as "address" or "address:port". Returns at once; the outcome is
    /// <see cref="Started"/> or <see cref="Ended"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The address can't be read.</exception>
    public void Join(string address)
    {
        if (!TryParseAddress(address, Protocol.DefaultPort, out string host, out int port))
            throw new ArgumentException($"'{address}' is not an address. Use ip or ip:port.", nameof(address));

        Leave();
        State = SessionState.Connecting;
        _hostPeer = -1;
        _transportConnected = false;
        _helloSentAt = Options.Clock();
        Log($"Connecting to {host}:{port}.");
        try
        {
            _transport.Connect(host, port);
        }
        catch
        {
            State = SessionState.Offline;
            throw;
        }
    }

    /// <summary>Leaves the session, or stops hosting. Does nothing when offline.</summary>
    public void Leave()
    {
        if (State == SessionState.Offline)
            return;
        if (State == SessionState.Hosting)
            CloseHostPeers("The host closed the game.");
        _transport.Stop();
        End(new SessionEnd(EndKind.Left, RejectReason.None, "You left the game."));
    }

    /// <summary>Call once per frame: delivers received packets and checks timeouts.</summary>
    public void Poll()
    {
        _transport.Poll();
        long now = Options.Clock();
        if (State == SessionState.Hosting)
            CheckPendingTimeouts(now);
        else if (State == SessionState.Connecting && _transportConnected && now - _helloSentAt > Options.WelcomeTimeoutMs)
            Abort(new SessionEnd(EndKind.ConnectFailed, RejectReason.None, "The host didn't answer."));
    }

    /// <summary>Round trip time to a player in milliseconds, -1 if unknown (clients only know the host).</summary>
    public int PingOf(Player player)
    {
        if (player == null || player.IsLocal)
            return -1;
        int peer = IsHost ? player.Peer : player.IsHost ? _hostPeer : -1;
        return peer < 0 ? -1 : _transport.PingOf(peer);
    }

    /// <summary>Parses "address" or "address:port".</summary>
    public static bool TryParseAddress(string text, int defaultPort, out string host, out int port)
    {
        host = null;
        port = defaultPort;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text))
            return false;
        int colon = text.LastIndexOf(':');
        if (colon < 0)
        {
            host = text;
            return true;
        }
        host = text.Substring(0, colon);
        return host.Length > 0 && int.TryParse(text.Substring(colon + 1), out port) && port > 0 && port < 65536;
    }

    private void End(SessionEnd end)
    {
        State = SessionState.Offline;
        _hostPeer = -1;
        _transportConnected = false;
        _pending.Clear();
        Roster.Clear();
        Log("Session ended: " + end);
        try { Ended?.Invoke(end); }
        catch (Exception e) { Log("A handler threw: " + e); }
    }

    // Stops the transport and ends the session in one go, for failures found by the session itself.
    private void Abort(SessionEnd end)
    {
        _transport.Stop();
        End(end);
    }

    private void Log(string message) => Options.Log?.Invoke(message);

    private static string CleanName(string name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
            return "Player";
        return name.Length > Protocol.MaxNameLength ? name.Substring(0, Protocol.MaxNameLength) : name;
    }

    private readonly struct PendingPeer
    {
        public PendingPeer(int peer, long since)
        {
            Peer = peer;
            Since = since;
        }

        public int Peer { get; }
        public long Since { get; }
    }
}
