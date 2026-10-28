using System;
using System.IO;
using ObenCoop.Chat;
using ObenCoop.Commands;
using ObenCoop.Identity;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using ObenCoop.Sessions;
using ObenFramework.Console;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenCoop;

/// <summary>
/// ObenCoop. For now it owns one co-op session (host, join, roster, chat) and pumps its network in
/// OnUpdate; nothing touches game state yet. The session survives a hot reload of the mod: the live
/// connection and the session bytes are parked in ReloadState (see <see cref="SessionKeeper"/>).
/// </summary>
public sealed class CoopMod : ObenMod
{
    private const int ErrorLogIntervalMs = 5000;

    private ConfigFile<CoopSettings> _settings;
    private DirectTransport _transport;
    private int _lastPollErrorTick;
    private bool _pollErrorLogged;

    internal CoopSession Session { get; private set; }

    internal CoopChat Chat { get; private set; }

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
        _transport = new DirectTransport { Log = message => Log.Warn(message) };
        Session = new CoopSession(_transport, options);
        RefreshOptions();

        Session.Started += OnStarted;
        Session.PlayerJoined += player => CoopNotices.Show(Log, "Co-op", player.Name + " joined.");
        Session.PlayerLeft += (player, reason) => CoopNotices.Show(Log, "Co-op", $"{player.Name} left. {reason}");
        Session.Ended += end => CoopNotices.Show(Log, "Co-op", end.Message);
        Chat = new CoopChat(this, Session, Log);

        if (SessionKeeper.TryResume(Session, _transport, Log))
            CoopNotices.Show(Log, "Co-op", "The session carried on after a mod reload.");

        GameConsole.Register(this, CoopCommands.Name, CoopCommands.Usage, args => CoopCommands.Run(this, args));
    }

    public override void OnUpdate()
    {
        try
        {
            Session.Poll();
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

    public override void OnUnload() => SessionKeeper.Park(Session, _transport, Log);

    public override void OnQuit() => Session?.Leave();

    /// <summary>Copies the current settings and loaded mods into the session options. Call before hosting or joining.</summary>
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
    }

    private void OnStarted()
    {
        string where = Session.IsHost
            ? $"Hosting for up to {Session.Options.MaxPlayers} players."
            : $"Joined {Session.Roster.Host.Name}'s game as {Session.Roster.Local.Name}.";
        CoopNotices.Show(Log, "Co-op", where);
    }
}
