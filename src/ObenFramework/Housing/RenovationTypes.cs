// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.World;

namespace ObenFramework.Housing;

public enum RenovationState
{
    /// <summary>Needs another upgrade first, or another upgrade of its line is under way.</summary>
    Locked,

    /// <summary>Can be ordered from a contractor.</summary>
    Available,

    InProgress,

    /// <summary>The work is done and waits for the final bill.</summary>
    AwaitingFinalPayment,

    Installed,
}

/// <summary>A snapshot of one renovation (an upgrade of the building or of one apartment).</summary>
public sealed class RenovationInfo
{
    internal RenovationInfo(string id, string name, int apartment, int startCostOc, int finalCostRm, int hours, RenovationState state)
    {
        Id = id;
        Name = name;
        Apartment = apartment;
        StartCostOc = startCostOc;
        FinalCostRm = finalCostRm;
        Hours = hours;
        State = state;
    }

    /// <summary><c>{apartment}/{upgrade id}</c>; apartment 0 is the building.</summary>
    public string Id { get; }

    public string Name { get; }

    /// <summary>The apartment it is for, 0 for the building.</summary>
    public int Apartment { get; }

    /// <summary>Open Sewer Coin paid when the work starts.</summary>
    public int StartCostOc { get; }

    /// <summary>Real Money paid on the final bill.</summary>
    public int FinalCostRm { get; }

    /// <summary>Game hours the work takes.</summary>
    public int Hours { get; }

    public RenovationState State { get; }
}

public enum RenovationChangeKind
{
    /// <summary>The work was ordered and paid for; it runs for the renovation's hours.</summary>
    Started,

    /// <summary>The work is done and waits for the final bill.</summary>
    WorkDone,

    /// <summary>The final bill was paid (or a script finished it): the upgrade is installed.</summary>
    Finished,
}

/// <summary>A renovation moved on, as raised by <see cref="Renovations.Changed"/>.</summary>
public readonly struct RenovationChange
{
    public RenovationChange(RenovationInfo info, RenovationChangeKind kind, ChangeSource source)
    {
        Info = info;
        Kind = kind;
        Source = source;
    }

    public RenovationInfo Info { get; }

    public RenovationChangeKind Kind { get; }

    /// <summary><see cref="ChangeSource.LocalPlayer"/> on the contractor's upgrade screen, <see cref="ChangeSource.Mod"/> for the framework's calls, the game's clock for <see cref="RenovationChangeKind.WorkDone"/>.</summary>
    public ChangeSource Source { get; }
}
