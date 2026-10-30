using ObenCoop.Commands;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Session;
using ObenUIKit;
using ObenUIKit.Controls;

namespace ObenCoop.Ui;

/// <summary>
/// What the co-op window shows: the state, then either hosting and joining, or the game this player is
/// in with its players, join code and invites. Typed text and choices live here, so a rebuild keeps them.
/// </summary>
internal static class CoopPage
{
    private static readonly string[] Ways = { "Direct (IP)", "Steam" };

    private static string _lobbyName;
    private static string _joinText = "";
    private static int _hostWay;
    private static string _message = "";
    private static LobbyStatus _messageStatus;
    private static bool _reveal;

    /// <summary>Hides join codes again (streamer mode): the window closed.</summary>
    public static void Forget() => _reveal = false;

    public static void Build(UiBuilder ui, CoopMod mod)
    {
        ui.Live(() => Visible(mod, Status(mod)));
        ui.Live(() => Visible(mod, Message(mod)), muted: true);
        ui.Space(10f);

        var session = mod.Session;
        if (session.IsActive)
            InGame(ui, mod);
        else if (mod.Lobby.Status == LobbyStatus.Pending)
            ui.Button("Cancel", () => Leave(mod));
        else
            Offline(ui, mod);

        ui.Space(10f);
        ui.Label("Co-op is still being built: playing in one shared world comes later. Everyone loads the same save for now.", muted: true);
    }

    private static void Offline(UiBuilder ui, CoopMod mod)
    {
        ui.Card("Host a game", card =>
        {
            card.TextField("Name", _lobbyName ?? mod.LobbyName(null), text => _lobbyName = text);
            card.Segmented("Play over", Ways, _hostWay, i => _hostWay = i);
            card.Live(() => _hostWay == 1 ? Unavailable(mod, LobbyKind.Steam) ?? SteamApp() : PortHint(mod), muted: true);
            card.Button("Host", () => Host(mod));
        });
        ui.Card("Join a game", card =>
        {
            var field = card.TextField("Join code or address", _joinText, text => _joinText = text);
            field.Masked = Hiding(mod);
            card.Row(row =>
            {
                row.Button("Join", () => Join(mod, _joinText));
                RevealButton(row, mod);
            });
        });
        ui.Card("Find games", card => LobbyListPart.Build(card, mod, listing => Join(mod, listing)));
    }

    private static void InGame(UiBuilder ui, CoopMod mod)
    {
        var lobby = mod.Lobby;
        ui.Card(mod.Session.IsHost ? "Your game" : "In the game", card =>
        {
            card.Live(() => Visible(mod, Code(lobby)), muted: true);
            card.Row(row =>
            {
                row.Button("Copy join code", () => Say(mod, LobbyCommands.Invite(mod, null)));
                RevealButton(row, mod);
                if (lobby.Kind == LobbyKind.Steam)
                    row.Button("Invite friends", () => Say(mod, lobby.Invite().OverlayOpened ? "Opened Steam's invite dialog." : "The Steam overlay isn't available."));
            });
            PlayerListPart.Build(card, mod);
            card.Button("Leave", ButtonStyle.Quiet, () => Leave(mod));
        });
    }

    // Streamer mode: codes and addresses are stars until Show, which lasts until the window closes.
    private static bool Hiding(CoopMod mod) => mod.Settings.HideJoinCodes && !_reveal;

    private static string Visible(CoopMod mod, string text) => _reveal ? text : mod.Shown(text);

    private static void RevealButton(UiBuilder row, CoopMod mod)
    {
        if (!mod.Settings.HideJoinCodes)
            return;
        row.Button(_reveal ? "Hide" : "Show", ButtonStyle.Quiet, () =>
        {
            _reveal = !_reveal;
            row.Window.Defer(row.Window.Rebuild);
        });
    }

    private static void Host(CoopMod mod)
    {
        mod.RefreshOptions();
        var kind = _hostWay == 1 ? LobbyKind.Steam : LobbyKind.Direct;
        Say(mod, mod.Lobby.Host(kind, mod.LobbyName(_lobbyName), mod.Settings.Port)
            ? (kind == LobbyKind.Steam ? "Creating a Steam lobby..." : "")
            : mod.Lobby.Error);
    }

    private static void Join(CoopMod mod, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Say(mod, "Type a join code or an address first.");
            return;
        }
        mod.RefreshOptions();
        Say(mod, mod.Lobby.Join(text.Trim()) ? $"Joining {mod.Lobby.Address}..." : mod.Lobby.Error);
    }

    private static void Join(CoopMod mod, LobbyListing listing)
    {
        if (!LobbyCheck.CanTry(listing.Verdict))
        {
            Say(mod, listing.Problem);
            return;
        }
        mod.RefreshOptions();
        Say(mod, mod.Lobby.Join(listing.Address) ? $"Joining {listing.Info.DisplayName}..." : mod.Lobby.Error);
    }

    private static void Leave(CoopMod mod)
    {
        mod.Lobby.Leave();
        Say(mod, "");
    }

    private static void Say(CoopMod mod, string text)
    {
        _message = text ?? "";
        _messageStatus = mod.Lobby.Status;
    }

    // A note about the last thing asked for, until the lobby moves on (the status line says the rest).
    private static string Message(CoopMod mod) => mod.Lobby.Status == _messageStatus ? _message : "";

    private static string Status(CoopMod mod)
    {
        var session = mod.Session;
        var lobby = mod.Lobby;
        int players = session.Roster.Count;
        string count = players == 1 ? "1 player" : players + " players";
        switch (session.State)
        {
            case SessionState.Hosting:
                return $"Hosting {(lobby.Info != null ? lobby.Info.DisplayName : "a game")}, {count}.";
            case SessionState.Connecting:
                return $"Joining {lobby.Address}...";
            case SessionState.Joined:
                var host = session.Roster.Host;
                return $"In {(host != null ? host.Name + "'s" : "a")} game, {count}.";
            default:
                if (lobby.Status == LobbyStatus.Pending)
                    return lobby.Kind == LobbyKind.Steam ? "Talking to Steam..." : "Setting up the game...";
                return lobby.Error.Length > 0 ? "Not in a co-op game. " + lobby.Error : "Not in a co-op game.";
        }
    }

    private static string Code(CoopLobby lobby)
    {
        if (!lobby.Address.IsValid)
            return "";
        string where = lobby.Kind == LobbyKind.Steam ? "Steam lobby" : lobby.Address.ToString();
        return lobby.Address.TryGetJoinCode(out string code) ? $"Join code {code} ({where})" : where;
    }

    private static string Unavailable(CoopMod mod, LobbyKind kind) => mod.Lobby.Unavailable(kind);

    private static string SteamApp() =>
        ObenFramework.Game.GameSteam.AppId == ObenFramework.Game.GameSteam.SpacewarAppId
            ? "Over Steam as Spacewar (480): friends need the same Steam app setting."
            : "Over Steam: your friends see it, or invite them.";

    private static string PortHint(CoopMod mod) =>
        $"Others on your network find it. Over the internet, forward UDP port {mod.Settings.Port} on your router.";
}
