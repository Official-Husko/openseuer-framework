using System;
using ObenCoop.Net.Lobby;
using ObenCoop.Net.Session;
using ObenUIKit;
using ObenUIKit.Menu;
using ObenUIKit.Windows;

namespace ObenCoop.Ui;

/// <summary>
/// Co-op on the main menu and in the pause menu: a window to host, join by code or address, find games
/// on the network or on Steam, and see who is in the game. It builds itself again whenever the lobby or
/// the roster changes, so it always shows the current state.
/// </summary>
internal static class CoopMenu
{
    private const string WindowId = "obencoop.window";

    public static void Init(CoopMod mod)
    {
        MainMenuButtons.Add(mod, "coop", "Co-op", () => Open(mod), order: 10);
        PauseMenuButtons.Add(mod, "coop", "Co-op", () => Open(mod), order: 10);
    }

    public static void Open(CoopMod mod)
    {
        // Opening it again only brings the open one forward; it follows the changes already.
        if (UiKit.Find(WindowId) != null)
        {
            UiKit.Open(mod, WindowId, "Co-op", ui => CoopPage.Build(ui, mod));
            return;
        }
        var window = UiKit.Open(mod, WindowId, "Co-op", ui => CoopPage.Build(ui, mod),
            new WindowOptions { KeepOnLevelChange = true });
        Follow(mod, window);
    }

    // Rebuilds the window on every change it shows, until it closes.
    private static void Follow(CoopMod mod, UiWindow window)
    {
        var lobby = mod.Lobby;
        var session = mod.Session;
        var browser = lobby.Browser;
        // At the end of the frame, once however many changes came in: a change can come from inside a
        // click on the window itself, whose button the rebuild would take away mid-call.
        bool due = false;
        Action rebuild = () =>
        {
            if (due || !window.IsOpen)
                return;
            due = true;
            window.Defer(() =>
            {
                due = false;
                window.Rebuild();
            });
        };
        Action<LobbyStatus> status = _ => rebuild();
        Action<CoopPlayer> joined = _ => rebuild();
        Action<CoopPlayer, string> left = (_, __) => rebuild();
        Action<SessionEnd> ended = _ => rebuild();
        Action<LobbyListing> found = _ => rebuild();

        lobby.StatusChanged += status;
        session.PlayerJoined += joined;
        session.PlayerLeft += left;
        session.Ended += ended;
        browser.Found += found;
        browser.Finished += rebuild;
        window.Closed += () =>
        {
            CoopPage.Forget();
            lobby.StatusChanged -= status;
            session.PlayerJoined -= joined;
            session.PlayerLeft -= left;
            session.Ended -= ended;
            browser.Found -= found;
            browser.Finished -= rebuild;
        };
    }
}
