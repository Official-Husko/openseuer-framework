using System;
using ObenCoop.Chat;
using ObenCoop.Commands;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Session;
using ObenFramework.Console;

namespace ObenCoop.Lobby;

/// <summary>
/// Tells the player what the session and lobby are doing: joins, leaves, failures, invites, and the
/// lobby list when a scan started from the console is over. Also acts on accepted Steam invites.
/// </summary>
internal sealed class LobbyNotices : IDisposable
{
    private const string Title = "Co-op";

    private readonly CoopMod _mod;
    private string _lastEnd = "";

    public LobbyNotices(CoopMod mod)
    {
        _mod = mod;
        mod.Session.Started += OnStarted;
        mod.Session.PlayerJoined += OnPlayerJoined;
        mod.Session.PlayerLeft += OnPlayerLeft;
        mod.Session.Ended += OnEnded;
        mod.Lobby.StatusChanged += OnStatusChanged;
        mod.Lobby.JoinRequested += OnJoinRequested;
        mod.Lobby.Browser.Finished += OnScanFinished;
    }

    /// <summary>Set by "coop lobby list" so the results are printed when the scan is over.</summary>
    public bool PrintNextScan { get; set; }

    public void Dispose()
    {
        _mod.Session.Started -= OnStarted;
        _mod.Session.PlayerJoined -= OnPlayerJoined;
        _mod.Session.PlayerLeft -= OnPlayerLeft;
        _mod.Session.Ended -= OnEnded;
        _mod.Lobby.StatusChanged -= OnStatusChanged;
        _mod.Lobby.JoinRequested -= OnJoinRequested;
        _mod.Lobby.Browser.Finished -= OnScanFinished;
    }

    private void Show(string text) => CoopNotices.Show(_mod.Log, Title, text);

    private void OnStarted()
    {
        var session = _mod.Session;
        string where = session.IsHost
            ? $"Hosting for up to {session.Options.MaxPlayers} players."
            : $"Joined {session.Roster.Host.Name}'s game as {session.Roster.Local.Name}.";
        Show(where);
    }

    private void OnPlayerJoined(CoopPlayer player) => Show(player.Name + " joined.");

    private void OnPlayerLeft(CoopPlayer player, string reason) => Show($"{player.Name} left. {reason}");

    private void OnEnded(SessionEnd end)
    {
        _lastEnd = end.Message;
        Show(end.Message);
    }

    private void OnStatusChanged(LobbyStatus status)
    {
        var lobby = _mod.Lobby;
        if (status == LobbyStatus.Hosting)
        {
            Show(LobbyText.Opened(lobby));
            return;
        }
        // Failures before the session started (a refused lookup, a Steam lobby that couldn't be made)
        // have no session end to show them.
        if (status == LobbyStatus.Offline && lobby.Error.Length > 0 && lobby.Error != _lastEnd)
            Show(lobby.Error);
        _lastEnd = "";
    }

    private void OnJoinRequested(LobbyAddress address)
    {
        if (!_mod.Settings.JoinInvites)
        {
            Show($"You were invited to a game. Join it with: coop join {address}");
            return;
        }
        if (_mod.Session.IsHost)
        {
            Show($"You were invited to a game, but you are hosting. Leave first (coop leave), then: coop join {address}");
            return;
        }
        _mod.RefreshOptions();
        Show(_mod.Lobby.Join(address) ? "Joining the game you were invited to..." : _mod.Lobby.Error);
    }

    private void OnScanFinished()
    {
        if (!PrintNextScan)
            return;
        PrintNextScan = false;
        // A list is too long for a notification; it goes to the console (F1) and the log.
        string text = LobbyText.List(_mod.Lobby.Browser);
        _mod.Log.Info(text);
        GameConsole.Print(text);
    }
}
