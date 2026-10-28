// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenCoop.Net.Session;

namespace ObenCoop.Net.Lobby;

/// <summary>
/// The lobby around a session: how players find a game (lists, codes, invites), check that they can
/// join it (protocol, game version, mods, room) before connecting, and get in. One state machine for
/// every way of playing; a backend per kind (<see cref="DirectLobbyBackend"/>, Steam) does the
/// platform's part, and the session's handshake stays the final gate.
/// Requests are asynchronous: <see cref="Status"/> is Pending until the lobby is open or joined, and
/// a generation counter makes answers to an abandoned request (left, or timed out) harmless.
/// Split over several files: this one has state, backends and session events, Requests has
/// host/join/leave/invite, Persistence carries the lobby over a hot reload.
/// </summary>
public sealed partial class CoopLobby : IDisposable
{
    private readonly Dictionary<LobbyKind, ILobbyBackend> _backends = new Dictionary<LobbyKind, ILobbyBackend>();
    private ILobbyBackend _active;
    private int _generation;
    private long _pendingSince;
    private bool _joining;

    public CoopLobby(CoopSession session, LobbyOptions options = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Options = options ?? new LobbyOptions();
        Browser = new LobbyBrowser(this);
        session.Started += OnSessionStarted;
        session.Ended += OnSessionEnded;
        session.PlayerJoined += OnRosterChanged;
        session.PlayerLeft += OnPlayerLeft;
    }

    public CoopSession Session { get; }

    public LobbyOptions Options { get; }

    public LobbyStatus Status { get; private set; }

    /// <summary>The kind of the current lobby, None when offline.</summary>
    public LobbyKind Kind => _active != null ? _active.Kind : LobbyKind.None;

    /// <summary>Why the last request failed, as a sentence; empty when it didn't.</summary>
    public string Error { get; private set; } = "";

    /// <summary>
    /// When hosting, what this game advertises; when joined or joining, what the host advertised
    /// (null when the host didn't answer the lookup).
    /// </summary>
    public LobbyInfo Info { get; private set; }

    /// <summary>Where the lobby is: what codes and invites point at. Default when offline.</summary>
    public LobbyAddress Address { get; private set; }

    /// <summary>Lists games that can be joined.</summary>
    public LobbyBrowser Browser { get; }

    /// <summary>Raised whenever <see cref="Status"/> changes.</summary>
    public event Action<LobbyStatus> StatusChanged;

    /// <summary>
    /// Raised when the player accepted an invite outside the game (the Steam overlay, a friend's
    /// "Join game"). The mod decides what to do; usually it calls <see cref="Join(LobbyAddress)"/>.
    /// </summary>
    public event Action<LobbyAddress> JoinRequested;

    /// <summary>Adds a way of playing. Dispose the result to take it away again (it leaves its lobby first).</summary>
    /// <exception cref="InvalidOperationException">There is already a backend of that kind.</exception>
    public IDisposable AddBackend(ILobbyBackend backend)
    {
        if (backend == null)
            throw new ArgumentNullException(nameof(backend));
        if (_backends.ContainsKey(backend.Kind))
            throw new InvalidOperationException($"There is already a {backend.Kind} lobby backend.");
        _backends[backend.Kind] = backend;
        backend.JoinRequested += OnJoinRequested;
        return new Removal(this, backend);
    }

    /// <summary>The backend of a kind, or null.</summary>
    public ILobbyBackend Backend(LobbyKind kind) => _backends.TryGetValue(kind, out var backend) ? backend : null;

    /// <summary>Why a kind of lobby can't be used now, as a sentence, or null when it can.</summary>
    public string Unavailable(LobbyKind kind)
    {
        var backend = Backend(kind);
        if (backend == null)
            return kind == LobbyKind.Steam ? "Steam isn't available in this game." : $"{kind} games aren't available.";
        return backend.Unavailable;
    }

    /// <summary>Call once per frame after the session's Poll: drives lookups, scans and timeouts.</summary>
    public void Poll()
    {
        long now = Session.Options.Clock();
        foreach (var backend in _backends.Values)
            backend.Poll(now);
        if (Status == LobbyStatus.Pending && now - _pendingSince > Options.RequestTimeoutMs)
            Fail("The lobby request timed out.");
    }

    public void Dispose()
    {
        Session.Started -= OnSessionStarted;
        Session.Ended -= OnSessionEnded;
        Session.PlayerJoined -= OnRosterChanged;
        Session.PlayerLeft -= OnPlayerLeft;
        foreach (var backend in _backends.Values)
            backend.JoinRequested -= OnJoinRequested;
        _backends.Clear();
        _active = null;
    }

    private void OnSessionStarted()
    {
        if (Status == LobbyStatus.Pending && _joining)
        {
            Session.Log($"Joined the lobby at {Address}.");
            SetStatus(LobbyStatus.Joined);
        }
    }

    private void OnSessionEnded(SessionEnd end)
    {
        if (Status == LobbyStatus.Offline)
            return;
        _generation++;
        CloseActive();
        Error = end.Kind == EndKind.Left ? "" : end.Message;
        SetStatus(LobbyStatus.Offline);
    }

    private void OnRosterChanged(CoopPlayer player)
    {
        if (Status != LobbyStatus.Hosting || Info == null)
            return;
        Info.Players = Session.Roster.Count;
        _active?.Update(Info.Clone());
    }

    private void OnPlayerLeft(CoopPlayer player, string reason) => OnRosterChanged(player);

    private void OnJoinRequested(LobbyAddress address)
    {
        try { JoinRequested?.Invoke(address); }
        catch (Exception e) { Session.Log("A join request handler threw: " + e); }
    }

    // Gives up the current request or lobby: the session goes too. Error is set for the player.
    private void Fail(string error)
    {
        _generation++;
        CloseActive();
        Error = error ?? "";
        Session.Log("Lobby: " + Error);
        SetStatus(LobbyStatus.Offline);
        if (Session.State != SessionState.Offline)
            Session.Leave();
    }

    private bool Refuse(string error)
    {
        Error = error ?? "";
        return false;
    }

    private void CloseActive()
    {
        try { _active?.Close(); }
        catch (Exception e) { Session.Log("Closing the lobby failed: " + e.Message); }
        _active = null;
        Address = default;
    }

    private void SetStatus(LobbyStatus status)
    {
        if (Status == status)
            return;
        Status = status;
        try { StatusChanged?.Invoke(status); }
        catch (Exception e) { Session.Log("A lobby status handler threw: " + e); }
    }

    private sealed class Removal : IDisposable
    {
        private CoopLobby _lobby;
        private readonly ILobbyBackend _backend;

        public Removal(CoopLobby lobby, ILobbyBackend backend)
        {
            _lobby = lobby;
            _backend = backend;
        }

        public void Dispose()
        {
            var lobby = _lobby;
            _lobby = null;
            if (lobby == null || lobby.Backend(_backend.Kind) != _backend)
                return;
            if (lobby._active == _backend)
                lobby.Leave();
            lobby.Browser.Forget(_backend);
            _backend.JoinRequested -= lobby.OnJoinRequested;
            lobby._backends.Remove(_backend.Kind);
        }
    }
}

/// <summary>Limits for <see cref="CoopLobby"/>.</summary>
public sealed class LobbyOptions
{
    /// <summary>A lobby request (create, look up and connect) that takes longer than this is given up.</summary>
    public int RequestTimeoutMs { get; set; } = 20000;

    /// <summary>The port used when an address has none.</summary>
    public int DefaultPort { get; set; } = Protocol.DefaultPort;
}
