using System;
using ObenCoop.Net.Lobby;
using ObenUIKit.Controls;

namespace ObenCoop.Ui;

/// <summary>The "Find games" part of the co-op window: search the network or Steam, pick a game to join.</summary>
internal static class LobbyListPart
{
    private static readonly string[] Places = { "Network and this PC", "Steam" };

    private static int _place;

    public static void Build(UiBuilder ui, CoopMod mod, Action<LobbyListing> join)
    {
        var browser = mod.Lobby.Browser;
        ui.Segmented("Where", Places, _place, i => _place = i);
        ui.Button(browser.Scanning ? "Searching..." : "Search", () =>
        {
            Search(mod);
            ui.Window.Defer(ui.Window.Rebuild);
        });
        ui.Live(() => Summary(browser), muted: true);
        if (browser.Results.Count > 0)
            ui.List(browser.Results.Count, (i, row) => Bind(browser, i, row), i => Pick(browser, i, join), rows: 5);
    }

    private static void Search(CoopMod mod)
    {
        var browser = mod.Lobby.Browser;
        if (browser.Scanning)
            return;
        mod.RefreshOptions();
        browser.Scan(_place == 1 ? LobbyKind.Steam : LobbyKind.Direct);
    }

    private static string Summary(LobbyBrowser browser)
    {
        if (browser.Scanning)
            return browser.Kind == LobbyKind.Steam ? "Asking Steam for friends' and public games..." : "Looking on your network and this PC...";
        if (browser.Error.Length > 0)
            return browser.Error;
        if (browser.Results.Count > 0)
            return browser.Results.Count == 1 ? "1 game. Pick it to join." : browser.Results.Count + " games. Pick one to join.";
        return browser.Kind == LobbyKind.None ? "" : "No games found. Over the internet, ask the host for their join code.";
    }

    private static void Bind(LobbyBrowser browser, int index, ListRow row)
    {
        if (index >= browser.Results.Count)
            return;
        var listing = browser.Results[index];
        var info = listing.Info;
        row.Text = $"{info.DisplayName}  {info.Players}/{info.MaxPlayers}";
        string mark = LobbyCheck.Mark(listing.Verdict);
        string ping = listing.PingMs >= 0 ? listing.PingMs + " ms" : "";
        row.Detail = mark.Length > 0 ? mark : ping;
        row.Muted = !LobbyCheck.CanTry(listing.Verdict);
    }

    private static void Pick(LobbyBrowser browser, int index, Action<LobbyListing> join)
    {
        if (index >= 0 && index < browser.Results.Count)
            join(browser.Results[index]);
    }
}
