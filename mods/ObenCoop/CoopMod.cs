using System;
using System.IO;
using ObenCoop.Chat;
using ObenCoop.Commands;
using ObenCoop.Identity;
using ObenCoop.Lobby;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Lobby.Matchmaking;
using ObenCoop.Net.Presence;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using ObenCoop.Presence;
using ObenCoop.Sessions;
using ObenCoop.Steam;
using ObenFramework.Console;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenCoop;

/// <summary>
/// ObenCoop. Owns one co-op session with its lobby (direct play always, Steam lobbies when the
/// game's Steam is up), presence and chat, and pumps the network in OnUpdate. Nothing else touches
/// game state yet. The session and lobby survive a hot reload of the mod (see <see cref="SessionKeeper"/>).
/// </summary>
public sealed partial class CoopMod : ObenMod
{
    private const int ErrorLogIntervalMs = 5000;

    private ConfigFile<CoopSettings> _settings;
    private int _lastPollErrorTick;
    private bool _pollErrorLogged;

    internal CoopSession Session { get; private set; }

    internal CoopLobby Lobby { get; private set; }

    internal DirectTransport Direct { get; private set; }

    internal DirectLobbyBackend DirectLobby { get; private set; }

    internal SteamSupport Steam { get; private set; }

    internal CoopPresence Presence { get; private set; }

    internal PresencePublisher Publisher { get; private set; }

    internal CoopChat Chat { get; private set; }

    internal LobbyNotices Notices { get; private set; }

    internal CoopSettings Settings => _settings.Value;

    public override void OnLoad()
    {
        _settings = LoadConfig<CoopSettings>();
        if (!File.Exists(_settings.Path))
            _settings.Save();

        var options = new SessionOptions
        {
            PlayerId = CoopIdentity.LoadOrCreate(PathTo(CoopIdentity.FileName(Instance.Suffix)), Log),
            GameVersion = Application.version,
            Log = message => Log.Info(message),
        };
        Direct = new DirectTransport { Log = message => Log.Warn(message) };
        Session = new CoopSession(Direct, options);
        Lobby = new CoopLobby(Session);
        DirectLobby = new DirectLobbyBackend(Direct, options.Clock);
        Lobby.AddBackend(DirectLobby);
        Steam = new SteamSupport(Lobby, options.Clock, Log);
        Presence = new CoopPresence(Session);
        Publisher = new PresencePublisher(Presence);
        RefreshOptions();

        Notices = new LobbyNotices(this);
        Chat = new CoopChat(this, Session, Log);

        if (SessionKeeper.TryResume(this, Log))
            CoopNotices.Show(Log, "Co-op", "The session carried on after a mod reload.");

        GameConsole.Register(this, CoopCommands.Name, CoopCommands.Usage, args => CoopCommands.Run(this, args));
        Ui.CoopMenu.Init(this);
    }

    public override void OnUpdate()
    {
        try
        {
            Session.Poll();
            Lobby.Poll();
            Steam.Update();
            if (Settings.SharePresence && Session.IsActive)
                Publisher.Update();
            Presence.Tick();
            _pollErrorLogged = false;
        }
        catch (Exception e)
        {
            // A broken packet or handler must not take the frame down; say so now and then, not every frame.
            if (!_pollErrorLogged || Environment.TickCount - _lastPollErrorTick > ErrorLogIntervalMs)
            {
                Log.Error("Network update failed", e);
                _lastPollErrorTick = Environment.TickCount;
                _pollErrorLogged = true;
            }
        }
    }

    public override void OnUnload()
    {
        // Park first (it needs the transports and the lobby as they are), then let go of everything
        // without leaving: a reload carries the session and lobby over.
        SessionKeeper.Park(this, Log);
        Notices.Dispose();
        Presence.Dispose();
        Lobby.Dispose();
        DirectLobby.Dispose();
        Steam.Dispose();
    }

    public override void OnQuit() => Lobby?.Leave();

    /// <summary>Copies config.json and the loaded mods into the session, lobby and Steam settings. Call before hosting, joining or listing.</summary>
    internal void RefreshOptions()
    {
        _settings.Reload();
        var settings = _settings.Value;
        var options = Session.Options;
        options.PlayerName = string.IsNullOrWhiteSpace(settings.PlayerName)
            ? (Instance.IsFirst ? "Player" : "Player " + Instance.Number)
            : settings.PlayerName.Trim();
        options.MaxPlayers = Math.Max(2, Math.Min(Protocol.MaxPlayers, settings.MaxPlayers));
        options.Mods = ModSetBuilder.Current(settings.IgnoredMods);

        Lobby.Options.DefaultPort = settings.Port;
        DirectLobby.BasePort = settings.Port;
        DirectLobby.ScanPorts = Math.Max(1, Math.Min(32, settings.ScanPorts));
        DirectLobby.PublicHost = settings.PublicAddress;
        Steam.Enabled = settings.Steam;
        Steam.Visibility = ParseVisibility(settings.SteamLobbyType);
    }

    /// <summary>Switches presence sharing and keeps the choice in config.json.</summary>
    internal void SetSharePresence(bool on)
    {
        _settings.Reload();
        _settings.Value.SharePresence = on;
        _settings.Save();
    }

    /// <summary>The name a hosted lobby shows: the setting, or "&lt;player&gt;'s game".</summary>
    internal string LobbyName(string given)
    {
        if (!string.IsNullOrWhiteSpace(given))
            return given.Trim();
        if (!string.IsNullOrWhiteSpace(Settings.LobbyName))
            return Settings.LobbyName.Trim();
        return Session.Options.PlayerName + "'s game";
    }

    private static LobbyVisibility ParseVisibility(string text)
    {
        switch ((text ?? "").Trim().ToLowerInvariant())
        {
            case "public": return LobbyVisibility.Public;
            case "private":
            case "invite": return LobbyVisibility.Private;
            default: return LobbyVisibility.FriendsOnly;
        }
    }
}
