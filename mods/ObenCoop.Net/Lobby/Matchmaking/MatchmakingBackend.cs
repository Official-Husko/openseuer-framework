// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using ObenCoop.Net.Transport;
using ObenCoop.Net.Transport.P2P;

namespace ObenCoop.Net.Lobby.Matchmaking;

/// <summary>
/// Steam-style lobbies (<see cref="IMatchmaking"/>) with the session on peer-to-peer networking
/// (<see cref="P2PTransport"/>). Hosting creates a lobby and writes the <see cref="LobbyInfo"/> into
/// its data; only members of that lobby may connect. Joining enters the lobby, waits a moment for its
/// data, checks it says ObenCoop, and hands the owner's id to the session, whose handshake checks
/// the rest. Accepted invites and "Join game" clicks come out as <see cref="JoinRequested"/>.
/// Split in two: this file hosts and joins, Scan lists lobbies.
/// </summary>
public sealed partial class MatchmakingBackend : ILobbyBackend
{
    /// <summary>Lobby data key and value that mark an ObenCoop lobby (lists filter on it).</summary>
    public const string GameKey = "game";
    public const string GameTag = "obencoop";

    /// <summary>Lobby data key holding the <see cref="LobbyInfo"/> as base64.</summary>
    public const string InfoKey = "info";

    private readonly IMatchmaking _matchmaking;
    private readonly P2PTransport _transport;
    private readonly Func<long> _clock;
    private readonly List<ulong> _members = new List<ulong>();

    // While Steam makes the lobby there is no member list yet. Without this the transport would take anyone
    // who knows this player's Steam id; requests are deferred instead and let in once the lobby exists.
    private static readonly Func<ulong, bool> NobodyYet = _ => false;

    private ulong _lobby;
    private bool _hosting;
    private bool _creating;
    private Action<LobbyAddress, string> _openDone;
    private LobbyInfo _openInfo;
    private ulong _entering;
    private Action<LobbyInfo, string, string> _lookupDone;
    private long _verifyUntil;

    public MatchmakingBackend(IMatchmaking matchmaking, P2PTransport transport, Func<long> clock)
    {
        _matchmaking = matchmaking ?? throw new ArgumentNullException(nameof(matchmaking));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _matchmaking.LobbyChanged += OnLobbyChanged;
        _matchmaking.JoinRequested += OnJoinRequested;
    }

    public LobbyKind Kind => LobbyKind.Steam;

    public ITransport Transport => _transport;

    public string Unavailable => _matchmaking.LocalId == 0 ? "Steam isn't signed in." : null;

    /// <summary>Who sees a hosted lobby. Friends only by default, like the reference mod.</summary>
    public LobbyVisibility Visibility { get; set; } = LobbyVisibility.FriendsOnly;

    /// <summary>How long joining waits for a lobby's data to arrive before giving up on it.</summary>
    public int VerifyWaitMs { get; set; } = 3000;

    /// <summary>The lobby this game is in (hosting or joined), 0 when none.</summary>
    public ulong LobbyId => _lobby;

    public event Action<LobbyAddress> JoinRequested;

    public void Open(LobbyInfo info, Action<LobbyAddress, string> done)
    {
        Close();
        _creating = true;
        _openDone = done;
        _openInfo = info;
        _transport.Admit = NobodyYet;
        _matchmaking.CreateLobby(Visibility, Math.Max(1, info.MaxPlayers), OnCreated);
    }

    public void Update(LobbyInfo info)
    {
        if (_hosting && _lobby != 0)
            _matchmaking.SetData(_lobby, InfoKey, info.ToBase64());
    }

    public void Close()
    {
        _creating = false;
        _openDone = null;
        _lookupDone = null;
        if (_entering != 0)
            _matchmaking.LeaveLobby(_entering);
        _entering = 0;
        if (_lobby != 0)
        {
            _matchmaking.LeaveLobby(_lobby);
            _matchmaking.ClearJoinTarget();
        }
        _lobby = 0;
        _hosting = false;
        _transport.Admit = null;
    }

    public void Lookup(LobbyAddress address, Action<LobbyInfo, string, string> done)
    {
        Close();
        ulong lobby = address.LobbyId;
        _entering = lobby;
        _lookupDone = done;
        _matchmaking.JoinLobby(lobby, ok => OnEntered(lobby, ok));
    }

    public bool ShowInviteDialog() => _lobby != 0 && _matchmaking.ShowInviteDialog(_lobby);

    public bool Resume(LobbyAddress address, LobbyInfo info, bool hosting)
    {
        if (address.LobbyId == 0)
            return false;
        _lobby = address.LobbyId;
        _hosting = hosting;
        _transport.Admit = hosting ? IsMember : (Func<ulong, bool>)null;
        _matchmaking.SetJoinTarget(_lobby);
        return true;
    }

    public void Poll(long now)
    {
        _transport.Idle();
        if (_lookupDone != null && _entering != 0 && _verifyUntil > 0)
            Verify(now);
        PollScan(now);
    }

    public void Dispose()
    {
        _matchmaking.LobbyChanged -= OnLobbyChanged;
        _matchmaking.JoinRequested -= OnJoinRequested;
    }

    private void OnCreated(ulong lobby)
    {
        if (!_creating)
        {
            // Given up (left, timed out) while Steam was still making it.
            if (lobby != 0)
                _matchmaking.LeaveLobby(lobby);
            return;
        }
        var done = _openDone;
        _creating = false;
        _openDone = null;
        if (lobby == 0)
        {
            done?.Invoke(default, "Steam couldn't create the lobby.");
            return;
        }

        _lobby = lobby;
        _hosting = true;
        _matchmaking.SetData(lobby, GameKey, GameTag);
        _matchmaking.SetData(lobby, InfoKey, _openInfo.ToBase64());
        _matchmaking.SetJoinTarget(lobby);
        _transport.Admit = IsMember;
        done?.Invoke(LobbyAddress.Steam(lobby), null);
    }

    private void OnEntered(ulong lobby, bool ok)
    {
        if (_lookupDone == null || lobby != _entering)
        {
            if (ok)
                _matchmaking.LeaveLobby(lobby);
            return;
        }
        if (!ok)
        {
            _entering = 0;
            Finish(null, null, "Couldn't join that Steam lobby. It may be gone or full, or only for the host's friends.");
            return;
        }
        // The lobby's data can arrive a little after entering; Verify waits for it.
        _verifyUntil = _clock() + VerifyWaitMs;
        Verify(_clock());
    }

    private void Verify(long now)
    {
        ulong lobby = _entering;
        string tag = _matchmaking.GetData(lobby, GameKey) ?? "";
        string infoText = _matchmaking.GetData(lobby, InfoKey) ?? "";
        ulong owner = _matchmaking.Owner(lobby);
        bool foreign = tag.Length > 0 && tag != GameTag;
        bool incomplete = tag.Length == 0 || infoText.Length == 0 || owner == 0;
        if (incomplete && !foreign && now < _verifyUntil)
            return;

        _entering = 0;
        _verifyUntil = 0;
        if (foreign || incomplete || !LobbyInfo.TryFromBase64(infoText, out var info))
        {
            _matchmaking.LeaveLobby(lobby);
            Finish(null, null, foreign ? "That Steam lobby isn't an ObenCoop game." : "That Steam lobby doesn't say what game it is.");
            return;
        }

        _lobby = lobby;
        _hosting = false;
        _matchmaking.SetJoinTarget(lobby);
        Finish(info, owner.ToString(CultureInfo.InvariantCulture), null);
    }

    private void Finish(LobbyInfo info, string target, string error)
    {
        var done = _lookupDone;
        _lookupDone = null;
        done?.Invoke(info, target, error);
    }

    private bool IsMember(ulong id)
    {
        if (_lobby == 0)
            return false;
        _matchmaking.Members(_lobby, _members);
        return _members.Contains(id);
    }

    private void OnLobbyChanged(ulong lobby)
    {
        if (_lookupDone != null && lobby == _entering && _verifyUntil > 0)
            Verify(_clock());
        if (_hosting && lobby == _lobby)
            _transport.Recheck("Left the lobby.");
        OnScanData(lobby);
    }

    private void OnJoinRequested(ulong lobby)
    {
        if (lobby != 0)
            JoinRequested?.Invoke(LobbyAddress.Steam(lobby));
    }
}
