namespace ObenCoop.Net.Presence;

/// <summary>Rates and limits for <see cref="CoopPresence"/>. Read every tick, so they can change at any time.</summary>
public sealed class PresenceOptions
{
    /// <summary>Time between samples while the player moves: 100 ms is 10 a second, like the reference mod.</summary>
    public int SendIntervalMs { get; set; } = 100;

    /// <summary>Time between samples while nothing changes (standing still, sitting). Keeps others from marking us stale.</summary>
    public int IdleIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Samples stop when <see cref="CoopPresence.Publish"/> hasn't been called for this long (the
    /// game is at the main menu or loading), so nobody sees a player frozen in place.
    /// </summary>
    public int PublishTimeoutMs { get; set; } = 1000;

    /// <summary>A remote player with no sample for this long is dropped from <see cref="CoopPresence.Remotes"/>.</summary>
    public int StaleAfterMs { get; set; } = 5000;

    /// <summary>
    /// How far behind the newest sample remote players are shown. Two send intervals means one lost
    /// packet doesn't make anyone stop and jump.
    /// </summary>
    public int InterpolationDelayMs { get; set; } = 200;

    /// <summary>When samples are late, how long a moving player is carried forward before they stop.</summary>
    public int MaxExtrapolationMs { get; set; } = 250;

    /// <summary>A move further than this between two samples is a jump (door, teleport), not walking.</summary>
    public float TeleportDistance { get; set; } = 8f;

    /// <summary>Smaller moves than this (metres, or degrees for the yaw) count as standing still.</summary>
    public float StillThreshold { get; set; } = 0.02f;
}
