using System;

namespace ObenCoop.Net.Presence;

/// <summary>What a player is doing, as far as presence cares. Bits, so several can be set.</summary>
[Flags]
public enum PresenceFlags : byte
{
    None = 0,
    Moving = 1,

    /// <summary>The walk toggle is on (slower than the default pace).</summary>
    Walking = 2,
    Crouching = 4,
    Sitting = 8,
    Swimming = 16,
    Sleeping = 32,

    /// <summary>
    /// The player jumped since the last sample (a teleport, a door to another spot, a level change).
    /// Receivers snap to this sample instead of sliding there. Set by <see cref="CoopPresence"/> itself.
    /// </summary>
    Teleported = 64,
}

/// <summary>One presence update as it travels: where a player was at a moment of their own clock.</summary>
public struct PresenceSample
{
    /// <summary>Counts up with every sample the sender makes (wraps), so late or repeated packets can be told apart.</summary>
    public ushort Sequence;

    /// <summary>The sender's clock in milliseconds when the sample was taken (wraps after about 49 days).</summary>
    public uint Time;

    /// <summary>Where the player is: the scene name, 1 to <see cref="PresenceCodec.MaxAreaLength"/> characters.</summary>
    public string Area;

    public PresenceVector Position;

    /// <summary>Heading in degrees, 0 to 360.</summary>
    public float Yaw;

    /// <summary>Metres per second, for extrapolating a little when samples are late.</summary>
    public PresenceVector Velocity;

    public PresenceFlags Flags;
}

/// <summary>Where another player should be drawn right now (interpolated between samples).</summary>
public struct PresencePose
{
    public string Area;
    public PresenceVector Position;
    public float Yaw;
    public PresenceVector Velocity;
    public PresenceFlags Flags;

    /// <summary>The samples ran out and the position was carried forward along the last velocity.</summary>
    public bool Extrapolated;

    /// <summary>Milliseconds since the newest sample arrived.</summary>
    public long AgeMs;
}
