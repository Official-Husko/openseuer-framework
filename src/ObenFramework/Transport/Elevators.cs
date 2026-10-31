// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Transport;

/// <summary>A snapshot of one elevator.</summary>
public readonly struct ElevatorInfo
{
    public ElevatorInfo(Elevator elevator, string id, int floors, float position, int moving, bool powered, List<int> queue, bool[] enabled)
    {
        Elevator = elevator;
        Id = id;
        Floors = floors;
        Position = position;
        Moving = moving;
        Powered = powered;
        Queue = queue;
        FloorsEnabled = enabled;
    }

    public Elevator Elevator { get; }

    public string Id { get; }

    public int Floors { get; }

    /// <summary>Where the car is, as a floor index with a fraction while it moves between floors.</summary>
    public float Position { get; }

    /// <summary>The nearest floor.</summary>
    public int Floor => (int)Math.Round(Position);

    /// <summary>1 going up, -1 going down, 0 standing.</summary>
    public int Moving { get; }

    public bool Powered { get; }

    /// <summary>Floors the elevator has been called to and not reached yet, lowest first.</summary>
    public List<int> Queue { get; }

    /// <summary>Which floors can be called (a disabled floor ignores calls).</summary>
    public bool[] FloorsEnabled { get; }
}

/// <summary>An elevator was called to a floor or reached one.</summary>
public readonly struct ElevatorEvent
{
    public ElevatorEvent(Elevator elevator, int floor, ChangeSource source)
    {
        Elevator = elevator;
        Floor = floor;
        Source = source;
    }

    public Elevator Elevator { get; }

    public int Floor { get; }

    public ChangeSource Source { get; }

    public string Id => Elevators.IdOf(Elevator);
}

/// <summary>
/// Elevators: their floors and queue, calling them, power and disabled floors. The doors of the car and the
/// floors are ordinary doors (see <c>Doors</c>). <see cref="Called"/> and <see cref="Arrived"/> come from the
/// game's own methods, never from a per-frame hook. Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class Elevators
{
    internal static readonly AccessTools.FieldRef<Elevator, int> MovingField = AccessTools.FieldRefAccess<Elevator, int>("moving");
    internal static readonly AccessTools.FieldRef<Elevator, List<float>> QueueField = AccessTools.FieldRefAccess<Elevator, List<float>>("Queue");

    /// <summary>The elevator was sent for: a new floor joined its queue.</summary>
    public static readonly WorldEvent<ElevatorEvent> Called = new WorldEvent<ElevatorEvent>(nameof(Called));

    /// <summary>
    /// The elevator stopped at a floor it was called to. Raised by the game's floor-reached step, which also runs
    /// (and opens the doors) when the car is called to the floor it stands at.
    /// </summary>
    public static readonly WorldEvent<ElevatorEvent> Arrived = new WorldEvent<ElevatorEvent>(nameof(Arrived));

    /// <summary>True when the elevator hooks are in place, so <see cref="Called"/> and <see cref="Arrived"/> are raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Transport");

    /// <summary>The elevators of the loaded scenes. A scan.</summary>
    public static List<Elevator> All() => ObjectIds.Active<Elevator>();

    public static Elevator Find(string id) => ObjectIds.Find<Elevator>(id);

    public static string IdOf(Elevator elevator) => ObjectIds.IdOf(elevator);

    public static ElevatorInfo Describe(Elevator elevator)
    {
        if (elevator == null)
            throw new ArgumentNullException(nameof(elevator));
        var queue = new List<int>();
        foreach (float floor in QueueField(elevator))
            queue.Add((int)floor);
        int count = elevator.Floors != null ? elevator.Floors.Length : 0;
        var enabled = new bool[count];
        for (int i = 0; i < count; i++)
            enabled[i] = elevator.Floors[i] != null && elevator.Floors[i].enabled;
        return new ElevatorInfo(elevator, IdOf(elevator), count, elevator.Position, MovingField(elevator), elevator.powered, queue, enabled);
    }

    /// <summary>
    /// Calls the elevator to a floor through the game's <c>Call</c>, like a call button. Returns true when the
    /// floor is queued or the car is already standing there; false when it has no power, the floor is disabled,
    /// the queue is full or a save is written or loaded.
    /// </summary>
    public static bool Call(Elevator elevator, int floor)
    {
        if (elevator == null)
            throw new ArgumentNullException(nameof(elevator));
        if (floor < 0 || floor >= elevator.Floors.Length)
            throw new ArgumentOutOfRangeException(nameof(floor), $"The elevator has floors 0 to {elevator.Floors.Length - 1}.");
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            elevator.Call(floor);
        return QueueField(elevator).Contains(floor) || (MovingField(elevator) == 0 && elevator.Position == floor);
    }

    /// <summary>Turns the elevator's power on or off (an unpowered elevator ignores calls).</summary>
    public static bool SetPowered(Elevator elevator, bool powered)
    {
        if (elevator == null)
            throw new ArgumentNullException(nameof(elevator));
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            elevator.TogglePower(powered);
        return elevator.powered == powered;
    }

    /// <summary>Lets a floor be called or not.</summary>
    public static bool SetFloorEnabled(Elevator elevator, int floor, bool enabled)
    {
        if (elevator == null)
            throw new ArgumentNullException(nameof(elevator));
        if (floor < 0 || floor >= elevator.Floors.Length)
            throw new ArgumentOutOfRangeException(nameof(floor));
        if (SaveGate.IsBusy)
            return false;
        if (enabled)
            elevator.SetFloorEnabled(floor);
        else
            elevator.SetFloorDisabled(floor);
        return elevator.Floors[floor] != null && elevator.Floors[floor].enabled == enabled;
    }
}

/// <summary>Turns <c>Elevator.Call</c> and the private floor-reached step into <see cref="Elevators"/> events.</summary>
internal static class ElevatorWatch
{
    [HarmonyPatch(typeof(Elevator), nameof(Elevator.Call))]
    private static class CallPatch
    {
        [HarmonyPrefix]
        private static void Before(Elevator __instance, out int __state)
        {
            __state = -1;
            try
            {
                if (Elevators.Called.HasSubscribers)
                    __state = Elevators.QueueField(__instance).Count;
            }
            catch (Exception e) { TransportModule.Log.Error("Elevator call hook", e); }
        }

        [HarmonyPostfix]
        private static void After(Elevator __instance, int floor, int __state)
        {
            try
            {
                if (__state >= 0 && WorldChanges.Active && Elevators.QueueField(__instance).Count > __state)
                    Elevators.Called.Raise(new ElevatorEvent(__instance, floor, WorldChanges.Current));
            }
            catch (Exception e) { TransportModule.Log.Error("Elevator call hook", e); }
        }
    }

    [HarmonyPatch(typeof(Elevator), "OnFloorreached")]
    private static class ArrivePatch
    {
        [HarmonyPostfix]
        private static void After(Elevator __instance, int floor, bool start)
        {
            try
            {
                if (!start && Elevators.Arrived.HasSubscribers && WorldChanges.Active)
                    Elevators.Arrived.Raise(new ElevatorEvent(__instance, floor, ChangeSource.Game));
            }
            catch (Exception e) { TransportModule.Log.Error("Elevator arrive hook", e); }
        }
    }
}
