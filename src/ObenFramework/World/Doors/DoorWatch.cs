using System.Runtime.CompilerServices;

namespace ObenFramework.World;

/// <summary>
/// Turns the game's door methods into <see cref="Doors.DoorChanged"/> events by comparing the door's state
/// before the outermost patched call with its state after each one. The game calls its door methods from
/// each other (<c>ToggleInterrupt</c> into <c>Close</c>, <c>OnFullyClosed</c> into <c>Lock</c>), so comparing
/// states instead of counting calls is what keeps every change reported exactly once.
/// </summary>
internal static class DoorWatch
{
    private sealed class Last
    {
        public DoorPhase Phase;
        public bool Locked;
        public ChangeSource Cause;
    }

    private static readonly ConditionalWeakTable<Door, Last> Known = new ConditionalWeakTable<Door, Last>();
    private static int _depth;

    public static void Reset() => _depth = 0;

    /// <summary>Before a patched door method: remember the state if this is the outermost call.</summary>
    public static void Begin(Door door)
    {
        _depth++;
        if (_depth != 1 || !Doors.DoorChanged.HasSubscribers)
            return;

        var state = Doors.StateOf(door);
        var last = Known.GetOrCreateValue(door);
        last.Phase = state.Phase;
        last.Locked = state.Locked;
    }

    /// <summary>After a patched door method (also when it threw): raise what differs from the remembered state.</summary>
    public static void End(Door door)
    {
        if (_depth > 0)
            _depth--;
        if (!Doors.DoorChanged.HasSubscribers || !Known.TryGetValue(door, out var last))
            return;

        var state = Doors.StateOf(door);
        var phase = last.Phase;
        bool locked = last.Locked;
        last.Phase = state.Phase;
        last.Locked = state.Locked;
        if (!WorldChanges.Active)
            return;

        if (state.Phase != phase)
        {
            var kind = KindOf(state.Phase, phase, out bool starts);
            if (kind != null)
            {
                if (starts)
                    last.Cause = WorldChanges.Current;
                Doors.DoorChanged.Raise(new DoorChange(door, kind.Value, last.Cause, state));
            }
        }

        if (state.Locked != locked)
        {
            var kind = state.Locked ? DoorChangeKind.Locked : DoorChangeKind.Unlocked;
            Doors.DoorChanged.Raise(new DoorChange(door, kind, WorldChanges.Current, state));
        }
    }

    // Opening a door that is already fully open (the game's OnOpen restarts the swing) isn't a change.
    private static DoorChangeKind? KindOf(DoorPhase now, DoorPhase before, out bool starts)
    {
        starts = now == DoorPhase.Opening || now == DoorPhase.Closing;
        switch (now)
        {
            case DoorPhase.Opening:
                return before == DoorPhase.Open ? (DoorChangeKind?)null : DoorChangeKind.Opening;
            case DoorPhase.Closing:
                return DoorChangeKind.Closing;
            case DoorPhase.Open:
                return before == DoorPhase.Opening ? DoorChangeKind.Opened : (DoorChangeKind?)null;
            case DoorPhase.Closed:
                return before == DoorPhase.Closing ? DoorChangeKind.Closed : (DoorChangeKind?)null;
            default:
                return null;
        }
    }
}
