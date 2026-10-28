using System;
using ObenCoop.Net.Lobby;
using UnityEngine;

namespace ObenCoop.Commands;

/// <summary>"coop lobby ..." and "coop invite".</summary>
internal static class LobbyCommands
{
    private const string Usage =
        "coop lobby create [steam] [name...] | list [steam | <address>] | join <number | code | address | steam:id> | info";

    public static string Run(CoopMod mod, string[] args)
    {
        if (args.Length < 2)
            return "Usage: " + Usage;
        switch (args[1].ToLowerInvariant())
        {
            case "create":
            case "host": return Create(mod, args);
            case "list":
            case "find": return List(mod, args);
            case "join": return Join(mod, args);
            case "info": return LobbyText.Describe(mod.Lobby) + "\nSteam: " + mod.Steam.Describe();
            case "leave": return CoopCommands.Run(mod, new[] { "leave" });
            default: return $"Unknown lobby subcommand '{args[1]}'. Usage: {Usage}";
        }
    }

    public static string Invite(CoopMod mod, string publicHost)
    {
        var invite = mod.Lobby.Invite(publicHost ?? mod.Settings.PublicAddress);
        bool copied = false;
        if (invite.Succeeded && invite.Code != null)
        {
            try
            {
                GUIUtility.systemCopyBuffer = invite.Code;
                copied = true;
            }
            catch (Exception e)
            {
                mod.Log.Warn("Couldn't copy the join code to the clipboard: " + e.Message);
            }
        }
        return LobbyText.Invite(invite, copied);
    }

    private static string Create(CoopMod mod, string[] args)
    {
        var kind = LobbyKind.Direct;
        int first = 2;
        if (args.Length > 2 && IsSteam(args[2]))
        {
            kind = LobbyKind.Steam;
            first = 3;
        }
        string name = args.Length > first ? string.Join(" ", args, first, args.Length - first) : null;

        mod.RefreshOptions();
        if (!mod.Lobby.Host(kind, mod.LobbyName(name), mod.Settings.Port))
            return mod.Lobby.Error;
        return kind == LobbyKind.Steam
            ? "Creating a Steam lobby..."
            : null; // The lobby opens at once and says so itself.
    }

    private static string List(CoopMod mod, string[] args)
    {
        mod.RefreshOptions();
        var kind = LobbyKind.Direct;
        var only = default(LobbyAddress);
        if (args.Length > 2 && IsSteam(args[2]))
        {
            kind = LobbyKind.Steam;
        }
        else if (args.Length > 2)
        {
            if (!LobbyAddress.TryParse(args[2], mod.Settings.Port, out only, out string problem))
                return problem;
            kind = only.Kind;
        }

        mod.Notices.PrintNextScan = true;
        if (!mod.Lobby.Browser.Scan(kind, only))
        {
            mod.Notices.PrintNextScan = false;
            return mod.Lobby.Browser.Error;
        }
        return kind == LobbyKind.Steam
            ? "Looking for friends' and public Steam lobbies..."
            : only.IsValid ? $"Asking {only}..." : "Looking for games on your network and on this machine...";
    }

    private static string Join(CoopMod mod, string[] args)
    {
        if (args.Length < 3)
            return "Usage: coop lobby join <number from coop lobby list | code | address | steam:id>";

        mod.RefreshOptions();
        bool started;
        if (int.TryParse(args[2], out int number) && number > 0 && number < 1000)
        {
            var listing = mod.Lobby.Browser.Pick(number);
            if (listing == null)
                return $"There is no game {number} in the last list. Run coop lobby list first.";
            if (!LobbyCheck.CanTry(listing.Verdict))
                return listing.Problem;
            started = mod.Lobby.Join(listing.Address);
        }
        else
        {
            started = mod.Lobby.Join(args[2]);
        }
        return started ? $"Joining {mod.Lobby.Address}..." : mod.Lobby.Error;
    }

    private static bool IsSteam(string word) => string.Equals(word, "steam", StringComparison.OrdinalIgnoreCase);
}
