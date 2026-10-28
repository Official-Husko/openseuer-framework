namespace ObenFramework.World;

/// <summary>Where a door is in its swing.</summary>
public enum DoorPhase
{
    /// <summary>Shut. It may still be locked.</summary>
    Closed,

    /// <summary>Swinging or sliding open.</summary>
    Opening,

    /// <summary>All the way open.</summary>
    Open,

    /// <summary>Swinging or sliding shut.</summary>
    Closing,

    /// <summary>Stopped part way (only doors that allow it).</summary>
    Ajar,
}

/// <summary>Which way a door swings open. Doors open away from the side the player is on.</summary>
public enum DoorSwing
{
    /// <summary>The door's own default direction.</summary>
    Default,

    /// <summary>The other way (a door that only swings one way ignores this).</summary>
    Opposite,
}

/// <summary>A snapshot of one door.</summary>
public readonly struct DoorState
{
    public DoorState(DoorPhase phase, float position, bool locked, bool opposite, bool isOpen)
    {
        Phase = phase;
        Position = position;
        Locked = locked;
        Opposite = opposite;
        IsOpen = isOpen;
    }

    public DoorPhase Phase { get; }

    /// <summary>How far open, 0 (shut) to 1 (fully open).</summary>
    public float Position { get; }

    public bool Locked { get; }

    /// <summary>The door is open or swinging the opposite way from its default direction.</summary>
    public bool Opposite { get; }

    /// <summary>The game's own flag: true from the moment the door starts opening until it is fully shut again.</summary>
    public bool IsOpen { get; }

    public DoorSwing Swing => Opposite ? DoorSwing.Opposite : DoorSwing.Default;

    public override string ToString() =>
        Phase + (Locked ? ", locked" : "") + (Opposite ? ", opposite" : "") + " (" + Position.ToString("0.00") + ")";
}
