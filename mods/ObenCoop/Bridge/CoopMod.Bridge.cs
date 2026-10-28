using System.Linq;
using Newtonsoft.Json.Linq;

namespace ObenCoop;

// Structured state for tools. The dev bridge's coop.* commands call these by name (no compile-time
// link between the two mods, so either can be loaded, reloaded or missing on its own).
public sealed partial class CoopMod
{
    /// <summary>Session, roster, lobby and Steam as JSON.</summary>
    public JObject BridgeStatus()
    {
        var session = Session;
        var stats = session.Stats;
        return new JObject
        {
            ["state"] = session.State.ToString(),
            ["host"] = session.IsHost,
            ["me"] = new JObject { ["name"] = session.Options.PlayerName, ["id"] = session.Options.PlayerId.ToString() },
            ["gameVersion"] = session.Options.GameVersion,
            ["transport"] = session.Transport == Direct ? "direct" : "steam",
            ["players"] = new JArray(session.Roster.Players.Select(p => new JObject
            {
                ["slot"] = p.Slot,
                ["name"] = p.Name,
                ["id"] = p.Id.ToString(),
                ["host"] = p.IsHost,
                ["local"] = p.IsLocal,
                ["ping"] = session.PingOf(p),
            })),
            ["traffic"] = new JObject
            {
                ["bytesSent"] = stats.BytesSent,
                ["bytesReceived"] = stats.BytesReceived,
                ["packetsSent"] = stats.PacketsSent,
                ["packetsReceived"] = stats.PacketsReceived,
                ["packetsLost"] = stats.PacketsLost,
                ["delivered"] = session.Router.Delivered,
                ["dropped"] = session.Router.Dropped,
            },
            ["lobby"] = LobbyJson(),
            ["steam"] = Steam.Describe(),
        };
    }

    /// <summary>Presence: what this game shares, and every other player with their interpolated pose now.</summary>
    public JObject BridgePresence()
    {
        var presence = Presence;
        var remotes = new JArray();
        foreach (var remote in presence.Remotes)
        {
            var entry = new JObject { ["name"] = remote.Player.Name, ["slot"] = remote.Player.Slot, ["area"] = remote.Area };
            var latest = remote.Latest;
            entry["latest"] = new JArray(latest.Position.X, latest.Position.Y, latest.Position.Z);
            if (presence.TryGetPose(remote.Player, out var pose))
            {
                entry["position"] = new JArray(pose.Position.X, pose.Position.Y, pose.Position.Z);
                entry["yaw"] = pose.Yaw;
                entry["velocity"] = new JArray(pose.Velocity.X, pose.Velocity.Y, pose.Velocity.Z);
                entry["flags"] = pose.Flags.ToString();
                entry["extrapolated"] = pose.Extrapolated;
                entry["ageMs"] = pose.AgeMs;
            }
            entry["late"] = remote.Track.Late;
            remotes.Add(entry);
        }
        var sent = presence.LastSent;
        return new JObject
        {
            ["sharing"] = presence.IsSharing,
            ["enabled"] = Settings.SharePresence,
            ["area"] = Publisher.Area,
            ["lastSent"] = new JArray(sent.Position.X, sent.Position.Y, sent.Position.Z),
            ["sent"] = presence.Sent,
            ["received"] = presence.Received,
            ["late"] = presence.Late,
            ["rejected"] = presence.Rejected,
            ["remotes"] = remotes,
        };
    }

    /// <summary>The last lobby list, with verdicts.</summary>
    public JObject BridgeLobbies()
    {
        var browser = Lobby.Browser;
        return new JObject
        {
            ["kind"] = browser.Kind.ToString(),
            ["scanning"] = browser.Scanning,
            ["error"] = browser.Error,
            ["results"] = new JArray(browser.Results.Select((l, i) => new JObject
            {
                ["number"] = i + 1,
                ["name"] = l.Info.DisplayName,
                ["host"] = l.Info.HostName,
                ["players"] = l.Info.Players,
                ["maxPlayers"] = l.Info.MaxPlayers,
                ["gameVersion"] = l.Info.GameVersion,
                ["address"] = l.Address.ToString(),
                ["code"] = l.Address.TryGetJoinCode(out string code) ? code : null,
                ["pingMs"] = l.PingMs,
                ["verdict"] = l.Verdict.ToString(),
                ["problem"] = l.Problem,
            })),
        };
    }

    private JObject LobbyJson()
    {
        var lobby = Lobby;
        var json = new JObject
        {
            ["status"] = lobby.Status.ToString(),
            ["kind"] = lobby.Kind.ToString(),
            ["address"] = lobby.Address.IsValid ? lobby.Address.ToString() : null,
            ["code"] = lobby.Address.TryGetJoinCode(out string code) ? code : null,
            ["error"] = lobby.Error,
        };
        if (lobby.Info != null)
            json["info"] = new JObject
            {
                ["name"] = lobby.Info.DisplayName,
                ["players"] = lobby.Info.Players,
                ["maxPlayers"] = lobby.Info.MaxPlayers,
                ["gameVersion"] = lobby.Info.GameVersion,
                ["mods"] = lobby.Info.ModCount,
            };
        return json;
    }
}
