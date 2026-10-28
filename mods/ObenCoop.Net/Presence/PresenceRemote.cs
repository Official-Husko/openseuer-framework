using ObenCoop.Net.Session;

namespace ObenCoop.Net.Presence;

/// <summary>Another player's presence: who, the newest sample, and the history to draw them smoothly.</summary>
public sealed class PresenceRemote
{
    internal PresenceRemote(CoopPlayer player)
    {
        Player = player;
    }

    public CoopPlayer Player { get; }

    /// <summary>The newest sample's area (scene name).</summary>
    public string Area => Track.Latest.Area ?? "";

    /// <summary>The newest sample as it arrived.</summary>
    public PresenceSample Latest => Track.Latest;

    /// <summary>Samples in arrival order on the sender's clock; see <see cref="CoopPresence.TryGetPose"/> for drawing.</summary>
    public PresenceTrack Track { get; } = new PresenceTrack();

    /// <summary>Local clock (session milliseconds) when the newest sample arrived.</summary>
    public long LastSeen => Track.LastArrival;

    internal AreaNames Names { get; } = new AreaNames();

    public override string ToString() => $"{Player.Name} in {Area} at {Latest.Position}";
}
