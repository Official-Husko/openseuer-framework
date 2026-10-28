using System.Globalization;
using System.Text;
using ObenCoop.Net.Presence;
using ObenFramework.Player;

namespace ObenCoop.Commands;

/// <summary>"coop presence [on|off]": where the other players are, and whether this game shares its own.</summary>
internal static class PresenceCommands
{
    public static string Run(CoopMod mod, string[] args)
    {
        if (args.Length > 1)
        {
            switch (args[1].ToLowerInvariant())
            {
                case "on": return Switch(mod, true);
                case "off": return Switch(mod, false);
                default: return "Usage: coop presence [on|off]";
            }
        }
        return Describe(mod);
    }

    public static string Describe(CoopMod mod)
    {
        var presence = mod.Presence;
        var text = new StringBuilder();
        text.Append("Presence: ").Append(mod.Settings.SharePresence ? "sharing" : "not sharing (coop presence on)");
        if (presence.IsSharing)
            text.Append(" from ").Append(mod.Publisher.Area);
        text.Append(", sent ").Append(presence.Sent).Append(", received ").Append(presence.Received);
        if (presence.Late > 0 || presence.Rejected > 0)
            text.Append(" (").Append(presence.Late).Append(" late, ").Append(presence.Rejected).Append(" rejected)");

        if (presence.Remotes.Count == 0)
        {
            text.Append(mod.Session.IsActive ? "\nNobody else is sharing where they are (they need to be in a level)." : "\nNot in a co-op game.");
            return text.ToString();
        }

        var me = PlayerRef.Position;
        string myArea = PlayerRef.SceneName;
        foreach (var remote in presence.Remotes)
        {
            text.Append("\n  ").Append(remote.Player.Name).Append(" in ").Append(remote.Area);
            if (!presence.TryGetPose(remote.Player, out var pose))
                continue;
            text.Append(" at ").Append(pose.Position).Append(", facing ").Append(((int)pose.Yaw).ToString(CultureInfo.InvariantCulture));
            if (me.HasValue && pose.Area == myArea)
            {
                var here = new PresenceVector(me.Value.x, me.Value.y, me.Value.z);
                text.Append(", ").Append(here.DistanceTo(pose.Position).ToString("0.0", CultureInfo.InvariantCulture)).Append(" m away");
            }
            if (pose.Flags != PresenceFlags.None)
                text.Append(" [").Append(pose.Flags).Append(']');
            text.Append(", last heard ").Append(pose.AgeMs).Append(" ms ago");
        }
        return text.ToString();
    }

    private static string Switch(CoopMod mod, bool on)
    {
        mod.SetSharePresence(on);
        if (!on)
            mod.Presence.Withdraw();
        return on ? "Sharing where you are with the other players." : "Stopped sharing where you are.";
    }
}
