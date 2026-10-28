namespace ObenFramework.World;

/// <summary>What happened to a door.</summary>
public enum DoorChangeKind
{
    /// <summary>The door started to open (or turned around while closing).</summary>
    Opening,

    /// <summary>The door started to close (or turned around while opening).</summary>
    Closing,

    /// <summary>The door reached fully open.</summary>
    Opened,

    /// <summary>The door reached fully shut.</summary>
    Closed,

    Locked,
    Unlocked,
}

/// <summary>A change of one door, as raised by <see cref="Doors.DoorChanged"/>.</summary>
public readonly struct DoorChange
{
    public DoorChange(Door door, DoorChangeKind kind, ChangeSource source, DoorState state)
    {
        Door = door;
        Kind = kind;
        Source = source;
        State = state;
    }

    public Door Door { get; }

    public DoorChangeKind Kind { get; }

    /// <summary>
    /// Who caused it. <see cref="DoorChangeKind.Opened"/> and <see cref="DoorChangeKind.Closed"/> carry the
    /// source of the opening or closing that led to them.
    /// </summary>
    public ChangeSource Source { get; }

    /// <summary>The player did it: used the door, or something the player triggered by hand.</summary>
    public bool ByLocalPlayer => Source == ChangeSource.LocalPlayer;

    /// <summary>The door right after the change (its swing side is in <see cref="DoorState.Opposite"/>).</summary>
    public DoorState State { get; }

    /// <summary>The door's id as <see cref="Doors.IdOf"/> gives it. Built on demand, so only ask when you need it.</summary>
    public string Id => Doors.IdOf(Door);
}
