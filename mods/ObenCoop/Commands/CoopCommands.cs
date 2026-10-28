using System;
using ObenCoop.Net.Session;

namespace ObenCoop.Commands;

/// <summary>The "coop" command in the F1 console: host, join, leave, status, say.</summary>
internal static class CoopCommands
{
    public const string Name = "coop";
    public const string Usage = "coop host [port] | join <ip[:port]> | leave | status | say <text>";

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
            default: return $"Unknown subcommand '{args[0]}'. Usage: {Usage}";
        }
    }

    private static string Host(CoopMod mod, string[] args)
    {
        int port = mod.Settings.Port;
        if (args.Length > 1 && (!int.TryParse(args[1], out port) || port < 1 || port > 65535))
            return $"'{args[1]}' is not a port (1 to 65535).";

        mod.RefreshOptions();
        mod.Session.Host(port);
        return $"Listening on UDP port {port}. Others join with: coop join <your address>:{port} "
            + "(over the internet the port has to be forwarded on your router).";
    }

    private static string Join(CoopMod mod, string[] args)
    {
        if (args.Length < 2)
            return "Usage: coop join <ip[:port]>";

        mod.RefreshOptions();
        mod.Session.Join(args[1]);
        return $"Connecting to {args[1]}...";
    }

    private static string Leave(CoopMod mod)
    {
        if (mod.Session.State == SessionState.Offline)
            return "Not in a co-op game.";
        mod.Session.Leave();
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
