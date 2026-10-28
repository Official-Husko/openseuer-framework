using System.Text;
using ObenCoop.Net.Lobby;

namespace ObenCoop.Commands;

/// <summary>How lobbies, lists and invites read in the console and in notifications.</summary>
internal static class LobbyText
{
    /// <summary>The line shown when a hosted lobby opened.</summary>
    public static string Opened(CoopLobby lobby)
    {
        string name = lobby.Info != null ? lobby.Info.DisplayName : "Your game";
        lobby.Address.TryGetJoinCode(out string code);
        if (lobby.Kind == LobbyKind.Steam)
            return $"Steam lobby open: {name}. Friends can join from your Steam profile, or with code {code}. Invite with: coop invite";
        return $"Lobby open: {name}. Join code {code ?? "(none)"} ({lobby.Address}). Others on your network see it in: coop lobby list";
    }

    /// <summary>The results of a lobby scan, numbered for "coop lobby join &lt;number&gt;".</summary>
    public static string List(LobbyBrowser browser)
    {
        var text = new StringBuilder();
        string where = browser.Kind == LobbyKind.Steam ? "Steam lobbies" : "Games on the network";
        if (browser.Error.Length > 0)
            return $"{where}: {browser.Error}";
        if (browser.Results.Count == 0)
        {
            text.Append(where).Append(": none found.");
            if (browser.Kind == LobbyKind.Direct)
                text.Append(" Over the internet ask the host for their join code, or: coop lobby list <address>");
            return text.ToString();
        }

        text.Append(where).Append(" (").Append(browser.Results.Count).Append("):");
        for (int i = 0; i < browser.Results.Count; i++)
        {
            var listing = browser.Results[i];
            var info = listing.Info;
            text.Append("\n  ").Append(i + 1).Append(". ").Append(info.DisplayName)
                .Append("  ").Append(info.Players).Append('/').Append(info.MaxPlayers);
            if (info.HostName.Length > 0)
                text.Append("  host ").Append(info.HostName);
            if (listing.Address.Kind == LobbyKind.Direct)
                text.Append("  ").Append(listing.Address);
            if (listing.PingMs >= 0)
                text.Append("  ").Append(listing.PingMs).Append(" ms");
            string mark = LobbyCheck.Mark(listing.Verdict);
            if (mark.Length > 0)
                text.Append("  (").Append(mark).Append(')');
        }
        text.Append("\nJoin with: coop lobby join <number>");
        return text.ToString();
    }

    /// <summary>The lobby part of "coop status" and "coop lobby info".</summary>
    public static string Describe(CoopLobby lobby)
    {
        var text = new StringBuilder();
        text.Append("Lobby: ").Append(lobby.Status);
        if (lobby.Status != LobbyStatus.Offline)
        {
            text.Append(" (").Append(lobby.Kind == LobbyKind.Steam ? "Steam" : "direct").Append(')');
            if (lobby.Address.IsValid)
                text.Append(" at ").Append(lobby.Address);
            if (lobby.Address.TryGetJoinCode(out string code))
                text.Append(", code ").Append(code);
        }
        if (lobby.Info != null && lobby.Status != LobbyStatus.Offline)
        {
            var info = lobby.Info;
            text.Append("\n  ").Append(info.DisplayName).Append(", ").Append(info.Players).Append('/').Append(info.MaxPlayers)
                .Append(" players, game ").Append(info.GameVersion).Append(", ").Append(info.ModCount).Append(" mods");
        }
        if (lobby.Error.Length > 0)
            text.Append("\n  Last problem: ").Append(lobby.Error);
        return text.ToString();
    }

    /// <summary>The answer to "coop invite".</summary>
    public static string Invite(LobbyInvite invite, bool copied)
    {
        if (!invite.Succeeded)
            return invite.Problem;
        var text = new StringBuilder();
        if (invite.OverlayOpened)
            text.Append("Opened Steam's invite dialog. ");
        if (invite.Code != null)
            text.Append("Join code: ").Append(invite.Code).Append(copied ? " (copied)" : "").Append(". Others join with: coop join ").Append(invite.Code);
        else
            text.Append("Others join with: coop join ").Append(invite.Address);
        if (invite.Address.Kind == LobbyKind.Direct && LobbyAddress.IsIPv4(invite.Address.Host) && IsPrivate(invite.Address.Host))
            text.Append("\nThat address only works on your own network. For the internet: coop invite <your public ip> (and forward UDP port ")
                .Append(invite.Address.Port).Append(')');
        return text.ToString();
    }

    private static bool IsPrivate(string ip) =>
        ip.StartsWith("10.") || ip.StartsWith("192.168.") || ip.StartsWith("127.") || ip.StartsWith("169.254.")
        || (ip.StartsWith("172.") && int.TryParse(ip.Split('.')[1], out int b) && b >= 16 && b <= 31);
}
