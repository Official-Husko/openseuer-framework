using System;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Lobby;

// What the player asks for: host, join, leave, invite.
public sealed partial class CoopLobby
{
    /// <summary>
    /// Hosts a game and opens a lobby for it. Returns false with <see cref="Error"/> set when that
    /// can't start; otherwise the lobby is Pending until the backend has opened it (at once for
    /// direct play), then Hosting.
    /// </summary>
    public bool Host(LobbyKind kind, string name, int port)
    {
        if (Status == LobbyStatus.Pending)
            return Refuse("A lobby request is already running. Wait for it, or leave first.");
        string unavailable = Unavailable(kind);
        if (unavailable != null)
            return Refuse(unavailable);
        if (Session.State != SessionState.Offline)
            return Refuse(Session.IsHost ? "You are already hosting. Leave first (coop leave)." : "You are in a game. Leave it first (coop leave).");

        var backend = Backend(kind);
        Session.UseTransport(backend.Transport);
        try
        {
            Session.Host(port);
        }
        catch (InvalidOperationException e)
        {
            return Refuse(e.Message);
        }

        // Port 0 means "any free port"; the lobby has to advertise the real one.
        if (port == 0 && backend.Transport is DirectTransport direct)
            port = direct.Port;

        _active = backend;
        int generation = ++_generation;
        _joining = false;
        _pendingSince = Session.Options.Clock();
        Error = "";
        Info = BuildInfo(name, port);
        SetStatus(LobbyStatus.Pending);
        backend.Open(Info.Clone(), (address, error) => OnOpened(generation, address, error));
        return true;
    }

    /// <summary>Joins what a player typed: an address, a join code or a Steam lobby id.</summary>
    public bool Join(string text)
    {
        if (!LobbyAddress.TryParse(text, Options.DefaultPort, out var address, out string problem))
            return Refuse(problem);
        return Join(address);
    }

    /// <summary>
    /// Looks a lobby up, checks it against this game, and joins it. Returns false with
    /// <see cref="Error"/> set when that can't start; otherwise the lobby is Pending until the host
    /// let this game in (Joined) or something failed (Offline, with <see cref="Error"/>).
    /// Leaves the current game first, unless this game hosts one.
    /// </summary>
    public bool Join(LobbyAddress address)
    {
        if (!address.IsValid)
            return Refuse("That is not a lobby address.");
        if (Status == LobbyStatus.Pending)
            return Refuse("A lobby request is already running. Wait for it, or leave first.");
        string unavailable = Unavailable(address.Kind);
        if (unavailable != null)
            return Refuse(unavailable);
        if (Session.IsHost)
            return Refuse("You are hosting. Leave first (coop leave) to join another game.");
        if (Status != LobbyStatus.Offline || Session.State != SessionState.Offline)
            Leave();

        var backend = Backend(address.Kind);
        _active = backend;
        int generation = ++_generation;
        _joining = true;
        _pendingSince = Session.Options.Clock();
        Error = "";
        Info = null;
        Address = address;
        SetStatus(LobbyStatus.Pending);
        backend.Lookup(address, (info, target, error) => OnLookedUp(generation, backend, info, target, error));
        return true;
    }

    /// <summary>Leaves the lobby and the game, or stops hosting. Any request still running is dropped.</summary>
    public void Leave()
    {
        _generation++;
        CloseActive();
        Info = null;
        Error = "";
        SetStatus(LobbyStatus.Offline);
        if (Session.State != SessionState.Offline)
            Session.Leave();
    }

    /// <summary>
    /// What to give a friend so they can join: the join code (and, for Steam, the overlay's invite
    /// dialog). <paramref name="publicHost"/> replaces this machine's address in a direct-play code,
    /// for players outside the local network.
    /// </summary>
    public LobbyInvite Invite(string publicHost = null)
    {
        if (Status != LobbyStatus.Hosting && Status != LobbyStatus.Joined)
            return LobbyInvite.Failed("Host or join a game first.");

        var address = Address;
        if (address.Kind == LobbyKind.Direct && !string.IsNullOrWhiteSpace(publicHost))
        {
            publicHost = publicHost.Trim();
            if (!LobbyAddress.IsIPv4(publicHost))
                return LobbyInvite.Failed($"'{publicHost}' is not an IPv4 address; codes can only hold one. Others can still join with: coop join {publicHost}:{address.Port}");
            address = LobbyAddress.Direct(publicHost, address.Port);
        }

        bool overlay = false;
        if (address.Kind == LobbyKind.Steam && _active != null)
            overlay = _active.ShowInviteDialog();
        address.TryGetJoinCode(out string code);
        return new LobbyInvite(address, code, overlay);
    }

    private LobbyInfo BuildInfo(string name, int port)
    {
        var options = Session.Options;
        var mods = options.Mods ?? ModSet.Empty;
        var random = new Random();
        return new LobbyInfo
        {
            Id = ((long)random.Next() << 32) | (uint)random.Next(),
            Protocol = Protocol.Version,
            GameVersion = options.GameVersion ?? "",
            ModsFingerprint = mods.Fingerprint,
            ModCount = mods.Entries.Count,
            Name = LobbyInfo.Clean(name, LobbyInfo.MaxNameLength),
            HostName = Session.Roster.Local != null ? Session.Roster.Local.Name : options.PlayerName,
            Players = Session.Roster.Count,
            MaxPlayers = options.MaxPlayers,
            Open = true,
            Port = port,
        };
    }

    private void OnOpened(int generation, LobbyAddress address, string error)
    {
        if (generation != _generation)
            return;
        if (error != null)
        {
            Fail(error);
            return;
        }
        Address = address;
        Session.Log($"Lobby open at {address}.");
        SetStatus(LobbyStatus.Hosting);
    }

    private void OnLookedUp(int generation, ILobbyBackend backend, LobbyInfo info, string target, string error)
    {
        if (generation != _generation)
            return;
        if (error != null)
        {
            Fail(error);
            return;
        }

        if (info != null)
        {
            var verdict = LobbyCheck.Check(info, Session.Options, out string problem);
            if (!LobbyCheck.CanTry(verdict))
            {
                Fail(problem);
                return;
            }
            Info = info;
        }
        else
        {
            Session.Log($"No lobby answer from {Address}; connecting anyway, the host's handshake will check.");
        }

        try
        {
            Session.UseTransport(backend.Transport);
            Session.Join(target);
        }
        catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
        {
            Fail(e.Message);
        }
    }
}

/// <summary>What <see cref="CoopLobby.Invite"/> came up with.</summary>
public sealed class LobbyInvite
{
    internal LobbyInvite(LobbyAddress address, string code, bool overlayOpened)
    {
        Address = address;
        Code = code;
        OverlayOpened = overlayOpened;
    }

    private LobbyInvite(string problem)
    {
        Problem = problem;
    }

    internal static LobbyInvite Failed(string problem) => new LobbyInvite(problem);

    public bool Succeeded => Problem == null;

    /// <summary>What the code points at.</summary>
    public LobbyAddress Address { get; }

    /// <summary>The join code, or null when the address can't be put in one (a host name).</summary>
    public string Code { get; }

    /// <summary>The Steam overlay's invite dialog was opened.</summary>
    public bool OverlayOpened { get; }

    /// <summary>Why there is no invite, or null.</summary>
    public string Problem { get; }
}
