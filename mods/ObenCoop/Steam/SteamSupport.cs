using System;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Lobby.Matchmaking;
using ObenCoop.Net.Transport.P2P;
using ObenFramework.Game;
using ObenLoader.Logging;

namespace ObenCoop.Steam;

/// <summary>
/// The optional Steam way of playing. Nothing here touches Steam until <see cref="GameSteam"/> says the
/// game's Steam is up; then it adds a Steam lobby backend (Steam lobbies, peer-to-peer networking) next
/// to direct play. When Steam never comes up, is switched off in config.json, or fails to start,
/// direct play simply stays the only way, and nothing is logged as an error more than once.
/// </summary>
internal sealed class SteamSupport : IDisposable
{
    // Steam passes "+connect_lobby <id>" when the game was started from an invite. Handled once per
    // run of the game, not once per load of the mod, so the flag lives in the process, not a static.
    private const string ConnectHandledKey = "ObenCoop.ConnectLineHandled";

    private readonly CoopLobby _lobby;
    private readonly Func<long> _clock;
    private readonly ModLogger _log;
    private SteamP2P _p2p;
    private SteamLobbies _lobbies;
    private MatchmakingBackend _backend;
    private bool _failed;

    public SteamSupport(CoopLobby lobby, Func<long> clock, ModLogger log)
    {
        _lobby = lobby;
        _clock = clock;
        _log = log;
    }

    private LobbyVisibility _visibility = LobbyVisibility.FriendsOnly;

    /// <summary>False keeps Steam from starting (config.json "Steam": false). Once started it stays until the mod unloads.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Who sees the Steam lobbies this game hosts from now on.</summary>
    public LobbyVisibility Visibility
    {
        get => _visibility;
        set
        {
            _visibility = value;
            if (_backend != null)
                _backend.Visibility = value;
        }
    }

    public bool IsActive => _backend != null;

    public P2PTransport Transport { get; private set; }

    /// <summary>The Steam lobby this game is in, or 0.</summary>
    public ulong LobbyId => _backend != null ? _backend.LobbyId : 0;

    /// <summary>The network, for closing a parked connection nobody adopted.</summary>
    public IP2PNetwork Network => _p2p;

    /// <summary>One line for the status text.</summary>
    public string Describe()
    {
        if (!Enabled && !IsActive)
            return "off (config.json)";
        if (_failed)
            return "failed to start, see the log";
        if (IsActive)
            return $"ready{AppText()}, signed in as {GameSteam.LocalName} ({_p2p.LocalId})";
        return GameSteam.Problem ?? "waiting for the game's Steam";
    }

    // Only said when it isn't Obenseuer, which is what everyone expects.
    private static string AppText()
    {
        uint app = GameSteam.AppId;
        return app == GameSteam.GameAppId ? "" : app == GameSteam.SpacewarAppId ? " as Spacewar (480)" : $" as app {app}";
    }

    /// <summary>Call every frame: starts once Steam is up. Cheap when started, off or failed.</summary>
    public void Update()
    {
        if (IsActive || _failed || !Enabled)
            return;
        if (GameSteam.IsReady)
            Start();
    }

    /// <summary>Starts now if Steam is up (a reload that has to pick up a Steam session). False otherwise.</summary>
    public bool TryStartNow()
    {
        Update();
        return IsActive;
    }

    public void Dispose()
    {
        // Callbacks go; lobbies and sessions stay (a reload may carry them over, see SessionKeeper).
        _backend?.Dispose();
        _lobbies?.Dispose();
        _p2p?.Dispose();
        _backend = null;
        _lobbies = null;
        Transport = null;
    }

    private void Start()
    {
        try
        {
            _p2p = new SteamP2P();
            _lobbies = new SteamLobbies();
            Transport = new P2PTransport(_p2p, _clock);
            _backend = new MatchmakingBackend(_lobbies, Transport, _clock) { Visibility = Visibility };
            _lobby.AddBackend(_backend);
            _log.Info($"Steam is up{AppText()}; Steam lobbies are available.");
            HandleConnectLine();
        }
        catch (Exception e)
        {
            _failed = true;
            _log.Error("Steam lobbies couldn't start; direct play still works", e);
            Dispose();
        }
    }

    private void HandleConnectLine()
    {
        var domain = AppDomain.CurrentDomain;
        if (domain.GetData(ConnectHandledKey) != null)
            return;
        domain.SetData(ConnectHandledKey, true);

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == SteamLobbies.ConnectPrefix && _lobbies.RaiseConnect(args[i] + " " + args[i + 1]))
                return;
        }
    }
}
