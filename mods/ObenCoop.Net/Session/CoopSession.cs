using System;
using System.Collections.Generic;
using ObenCoop.Net.Blobs;
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

    private ITransport _transport;
    private readonly List<PendingPeer> _pending = new List<PendingPeer>();

    private int _hostPeer = -1;
    private bool _transportConnected;
    private long _helloSentAt;

    public CoopSession(ITransport transport, SessionOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Hook(transport ?? throw new ArgumentNullException(nameof(transport)));
        _processQueued = ProcessPacket;
        Blobs = new BlobTransfer(this);
    }

    public SessionOptions Options { get; }

    public SessionState State { get; private set; }

    public Roster Roster { get; } = new Roster();

    public MessageRouter Router { get; } = new MessageRouter();

    /// <summary>Sends and receives byte arrays of any size, on channels of their own.</summary>
    public BlobTransfer Blobs { get; }

    public bool IsHost => State == SessionState.Hosting;

    /// <summary>True when messages can be sent: hosting, or joined to a host.</summary>
    public bool IsActive => State == SessionState.Hosting || State == SessionState.Joined;

    public TransportStats Stats => _transport.Stats;

    /// <summary>What the session runs on. Swap it with <see cref="UseTransport"/> while offline.</summary>
    public ITransport Transport => _transport;

    /// <summary>Raised when this game is part of a session: the host is listening, or the host accepted the join.</summary>
    public event Action Started;

    /// <summary>Raised when another player is in the roster, on every game. On joining, once per player already there.</summary>
    public event Action<CoopPlayer> PlayerJoined;

    /// <summary>Raised when another player has gone. The text says why.</summary>
    public event Action<CoopPlayer, string> PlayerLeft;

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
        Roster.Add(new CoopPlayer(CoopPlayer.HostSlot, Options.PlayerId, CleanName(Options.PlayerName), true, -1));
        State = SessionState.Hosting;
        Log($"Hosting on port {port} for up to {max} players.");
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
            // The transport may have started its socket before the connect failed (an address that doesn't resolve).
            _transport.Stop();
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

    /// <summary>
    /// Runs the session on another transport from now on (direct IP or Steam, say). Only while
    /// offline; channel handlers, blob listeners and event subscribers stay as they are.
    /// </summary>
    /// <exception cref="InvalidOperationException">The session is running.</exception>
    public void UseTransport(ITransport transport)
    {
        if (transport == null)
            throw new ArgumentNullException(nameof(transport));
        if (ReferenceEquals(transport, _transport))
            return;
        if (State != SessionState.Offline)
            throw new InvalidOperationException("Leave the session before switching how it connects.");
        Unhook(_transport);
        Hook(transport);
    }

    /// <summary>Call once per frame: delivers received packets and checks timeouts.</summary>
    public void Poll()
    {
        // What the last poll left in the queues goes first, then what the transport has now.
        _inbound.BeginPoll();
        _inbound.Drain(Options.MaxMessagesPerPoll, _processQueued);
        _transport.Poll();
        long now = Options.Clock();
        if (State == SessionState.Hosting)
            CheckPendingTimeouts(now);
        else if (State == SessionState.Connecting && _transportConnected && now - _helloSentAt > Options.WelcomeTimeoutMs)
            Abort(new SessionEnd(EndKind.ConnectFailed, RejectReason.None, "The host didn't answer."));
        if (IsActive)
            Blobs.Poll(now);
    }

    /// <summary>Round trip time to a player in milliseconds, -1 if unknown (clients only know the host).</summary>
    public int PingOf(CoopPlayer player)
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
        _inbound.Clear();
        _backedUp.Clear();
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

    internal void Log(string message) => Options.Log?.Invoke(message);

    private void Hook(ITransport transport)
    {
        _transport = transport;
        transport.PeerConnected += OnPeerConnected;
        transport.PeerDisconnected += OnPeerDisconnected;
        transport.PacketReceived += OnPacket;
    }

    private void Unhook(ITransport transport)
    {
        transport.PeerConnected -= OnPeerConnected;
        transport.PeerDisconnected -= OnPeerDisconnected;
        transport.PacketReceived -= OnPacket;
    }

    // Names end up in rich text (notifications, the console), so no tags, control, hidden or unpaired characters get through.
    internal static string CleanName(string name)
    {
        if (name != null)
        {
            var chars = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '<' || c == '>')
                    chars.Append(c == '<' ? '(' : ')');
                else if (!char.IsControl(c) && !SafeText.IsHidden(c) && !SafeText.IsLoneSurrogate(name, i))
                    chars.Append(c);
            }
            name = chars.ToString().Trim();
        }
        if (string.IsNullOrEmpty(name))
            return "Player";
        return SafeText.Cut(name, Protocol.MaxNameLength);
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
