using ObenCoop.Net.Lobby;
using ObenCoop.Net.Session;
using ObenUIKit;
using ObenUIKit.Controls;
using ObenUIKit.Menu;

namespace ObenCoop.Ui;

/// <summary>
/// Co-op on the main menu and in the pause menu. For now both open a small window with the co-op state and where to
/// host and join (the F1 console); hosting, joining and the lobby list move into it as co-op grows.
/// </summary>
internal static class CoopMenu
{
    private const string WindowId = "obencoop.window";

    public static void Init(CoopMod mod)
    {
        MainMenuButtons.Add(mod, "coop", "Co-op", () => Open(mod), order: 10);
        PauseMenuButtons.Add(mod, "coop", "Co-op", () => Open(mod), order: 10);
    }

    public static void Open(CoopMod mod) => UiKit.Open(mod, WindowId, "Co-op", ui => Build(ui, mod));

    private static void Build(UiBuilder ui, CoopMod mod)
    {
        ui.Header("Status");
        ui.Live(() => Status(mod));
        ui.Space(10f);
        ui.Label("Co-op is still being built: playing in one shared world comes later, and for now everyone loads the "
            + "same save. Hosting and joining go through the F1 console:", muted: true);
        ui.Label("coop host  ·  coop join [address or join code]  ·  coop lobby list  ·  coop status", muted: true);
    }

    private static string Status(CoopMod mod)
    {
        var session = mod.Session;
        if (session == null)
            return "Co-op isn't running.";
        int players = session.Roster.Count;
        string count = players == 1 ? "1 player" : players + " players";
        switch (session.State)
        {
            case SessionState.Hosting:
                return $"Hosting {(mod.Lobby.Info != null ? mod.Lobby.Info.DisplayName : "a game")}, {count}.";
            case SessionState.Connecting:
                return $"Joining {mod.Lobby.Address}...";
            case SessionState.Joined:
                var host = session.Roster.Host;
                return $"In {(host != null ? host.Name + "'s" : "a")} game, {count}.";
            default:
                return mod.Lobby.Status == LobbyStatus.Pending ? "Setting up a game..." : "Not in a co-op game.";
        }
    }
}
