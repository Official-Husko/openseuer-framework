using ObenCoop.Net.Lobby;
using ObenCoop.Net.Session;

namespace ObenCoop.Commands;

/// <summary>The "coop" command in the F1 console.</summary>
internal static class CoopCommands
{
    public const string Name = "coop";

    public const string Usage =
        "coop host [port] | join <ip[:port] | code | steam:id> | leave | status | say <text> | " +
        "lobby create|list|join|info ... | invite [public ip] | presence [on|off]";

    public static string Run(CoopMod mod, string[] args)
    {
        if (args.Length == 0)
            return "Usage: " + Usage;

        switch (args[0].ToLowerInvariant())
        {
            case "host": return Host(mod, args);
            case "join": return Join(mod, args);
            case "leave": return Leave(mod);
            case "status": return CoopStatus.Describe(mod);
            case "say": return Say(mod, args);
            case "lobby": return LobbyCommands.Run(mod, args);
            case "invite": return LobbyCommands.Invite(mod, args.Length > 1 ? args[1] : null);
            case "presence": return PresenceCommands.Run(mod, args);
            default: return $"Unknown subcommand '{args[0]}'. Usage: {Usage}";
        }
    }

    // "coop host" is a direct-play lobby with the default name; "coop lobby create" has the options.
    private static string Host(CoopMod mod, string[] args)
    {
        int port = mod.Settings.Port;
        if (args.Length > 1 && (!int.TryParse(args[1], out port) || port < 1 || port > 65535))
            return $"'{args[1]}' is not a port (1 to 65535).";

        mod.RefreshOptions();
        if (!mod.Lobby.Host(LobbyKind.Direct, mod.LobbyName(null), port))
            return mod.Lobby.Error;
        return $"Listening on UDP port {port}. Others join with: coop join <your address>:{port}, a join code (coop invite), "
            + "or find you in: coop lobby list (over the internet the port has to be forwarded on your router).";
    }

    private static string Join(CoopMod mod, string[] args)
    {
        if (args.Length < 2)
            return "Usage: coop join <ip[:port] | join code | steam:lobby id>";

        mod.RefreshOptions();
        if (!mod.Lobby.Join(args[1]))
            return mod.Lobby.Error;
        return $"Looking up {mod.Lobby.Address} and joining...";
    }

    private static string Leave(CoopMod mod)
    {
        if (mod.Session.State == SessionState.Offline && mod.Lobby.Status == LobbyStatus.Offline)
            return "Not in a co-op game.";
        mod.Lobby.Leave();
        return null;
    }

    private static string Say(CoopMod mod, string[] args)
    {
        if (args.Length < 2)
            return "Usage: coop say <text>";
        if (!mod.Chat.Say(string.Join(" ", args, 1, args.Length - 1)))
            return "Not in a co-op game, so nobody would hear that.";
        return null;
    }
}
