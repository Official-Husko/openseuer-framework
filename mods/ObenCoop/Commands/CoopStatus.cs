using System.Text;
using ObenCoop.Net.Session;

namespace ObenCoop.Commands;

/// <summary>The text of "coop status".</summary>
internal static class CoopStatus
{
    public static string Describe(CoopMod mod)
    {
        var session = mod.Session;
        var text = new StringBuilder();
        text.Append("Co-op: ").Append(session.State);

        if (session.State == SessionState.Offline)
        {
            text.Append("\nYou are ").Append(session.Options.PlayerName).Append(" (").Append(session.Options.PlayerId).Append(')');
            text.Append("\nProtocol ").Append(Protocol.Version).Append(", game ").Append(session.Options.GameVersion);
            text.Append('\n').Append(LobbyText.Describe(mod.Lobby));
            text.Append("\nSteam: ").Append(mod.Steam.Describe());
            return text.ToString();
        }

        text.Append(", ").Append(session.Roster.Count).Append(" player(s)");
        foreach (var player in session.Roster.Players)
        {
            text.Append("\n  ").Append(player.Slot).Append("  ").Append(player.Name);
            if (player.IsHost)
                text.Append(" (host)");
            if (player.IsLocal)
                text.Append(" (you)");
            int ping = session.PingOf(player);
            if (ping >= 0)
                text.Append("  ").Append(ping).Append(" ms");
        }

        var stats = session.Stats;
        text.Append("\nSent ").Append(stats.BytesSent).Append(" bytes in ").Append(stats.PacketsSent).Append(" packets, received ")
            .Append(stats.BytesReceived).Append(" bytes in ").Append(stats.PacketsReceived).Append(" packets, ")
            .Append(stats.PacketsLost).Append(" lost.");
        text.Append("\nMessages delivered ").Append(session.Router.Delivered).Append(", dropped ").Append(session.Router.Dropped).Append('.');
        text.Append('\n').Append(LobbyText.Describe(mod.Lobby));
        text.Append('\n').Append(PresenceCommands.Describe(mod));
        text.Append("\nSteam: ").Append(mod.Steam.Describe());
        return text.ToString();
    }
}
