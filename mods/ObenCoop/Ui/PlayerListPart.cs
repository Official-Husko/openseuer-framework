using ObenUIKit.Controls;

namespace ObenCoop.Ui;

/// <summary>The players in the game, with who hosts, who is you and each one's ping.</summary>
internal static class PlayerListPart
{
    public static void Build(UiBuilder ui, CoopMod mod)
    {
        var roster = mod.Session.Roster;
        ui.List(roster.Count, (i, row) =>
        {
            if (i >= roster.Count)
                return;
            var player = roster.Players[i];
            row.Text = player.Name + (player.IsLocal ? " (you)" : "") + (player.IsHost ? ", host" : "");
            int ping = player.IsLocal ? -1 : mod.Session.PingOf(player);
            row.Detail = ping >= 0 ? ping + " ms" : "";
        }, rows: 4);
    }
}
