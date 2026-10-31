using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World.Addressing;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.World;

/// <summary>
/// Doors: find them, read their state, change it and watch it change. Changes go through the game's own
/// methods (<c>ForceOpen</c>, <c>Close</c>, <c>Lock</c>, <c>Unlock</c>), so relays, sounds and lock timers
/// behave as in the game, and they run inside a <see cref="ChangeSource.Mod"/> scope so
/// <see cref="DoorChanged"/> tells them apart from the player's and the game's own doing. Changes are refused
/// (false) while a save is written or loaded.
/// </summary>
public static class Doors
{
    private static readonly AccessTools.FieldRef<Door, float> PositionField = AccessTools.FieldRefAccess<Door, float>("position");
    private static readonly AccessTools.FieldRef<Door, bool> DirectionField = AccessTools.FieldRefAccess<Door, bool>("direction");
    private static readonly AccessTools.FieldRef<Door, bool> ReversedField = AccessTools.FieldRefAccess<Door, bool>("ReversedDir");

    /// <summary>
    /// A door started or finished a swing, or was locked or unlocked. Raised from the game's own door
    /// methods, only while the world is settled (not during a load), and never per frame. Unlocking with a
    /// key or lockpick is reported when the door next acts, since the game does that inside its lock object.
    /// </summary>
    public static readonly WorldEvent<DoorChange> DoorChanged = new WorldEvent<DoorChange>(nameof(DoorChanged));

    /// <summary>True when the door hooks are in place, so <see cref="DoorChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    /// <summary>Every door in the loaded scenes. A scan, so not for per-frame use.</summary>
    public static List<Door> All(bool includeInactive = false)
    {
        var doors = Object.FindObjectsByType<Door>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return new List<Door>(doors);
    }

    /// <summary>
    /// Finds a door by id: what <see cref="IdOf"/> returned, a full <see cref="ObjectAddress"/> text, a hierarchy
    /// path or <c>#instanceId</c>. Null when there is none.
    /// </summary>
    public static Door Find(string id) => ObjectResolver.Find<Door>(id);

    /// <summary>
    /// The id to use for a door in a message or a file: its GUID where the editor set one (stable, short),
    /// else its full address text.
    /// </summary>
    public static string IdOf(Door door)
    {
        if (door == null)
            return null;
        return string.IsNullOrEmpty(door.GUID) ? ObjectAddress.Of(door).ToString() : door.GUID;
    }

    public static ObjectAddress AddressOf(Door door) => ObjectAddress.Of(door);

    /// <summary>The door's state right now. Throws if the door is gone.</summary>
    public static DoorState StateOf(Door door)
    {
        if (door == null)
            throw new ArgumentNullException(nameof(door));

        bool moving = door.moving;
        float position = PositionField(door);
        var phase = moving
            ? (DirectionField(door) ? DoorPhase.Opening : DoorPhase.Closing)
            : position <= 0f ? DoorPhase.Closed : position >= 1f ? DoorPhase.Open : DoorPhase.Ajar;
        return new DoorState(phase, position, door.Locked, ReversedField(door), door.doorIsOpen);
    }

    /// <summary>
    /// Opens the door the way a character does: past the lock (<c>ForceOpen</c>), unless
    /// <paramref name="respectLock"/> asks to leave a locked door alone. Returns true when the door is open
    /// or opening afterwards; an open door is left as it is.
    /// </summary>
    public static bool Open(Door door, DoorSwing swing = DoorSwing.Default, bool respectLock = false)
    {
        var state = StateOf(door);
        if (state.Phase == DoorPhase.Open || state.Phase == DoorPhase.Opening)
            return true;
        if ((respectLock && state.Locked) || SaveGate.IsBusy)
            return false;

        using (WorldChanges.EnterMod())
        {
            if (swing == DoorSwing.Opposite)
                door.ForceOpenOpposite();
            else
                door.ForceOpen();
        }
        var after = StateOf(door).Phase;
        return after == DoorPhase.Opening || after == DoorPhase.Open;
    }

    /// <summary>Closes the door. Returns true when it is closed or closing afterwards.</summary>
    public static bool Close(Door door)
    {
        var phase = StateOf(door).Phase;
        if (phase == DoorPhase.Closed || phase == DoorPhase.Closing)
            return true;
        if (SaveGate.IsBusy)
            return false;

        using (WorldChanges.EnterMod())
            door.Close();
        phase = StateOf(door).Phase;
        return phase == DoorPhase.Closed || phase == DoorPhase.Closing;
    }

    /// <summary>Locks or unlocks the door through <c>Lock()</c> and <c>Unlock()</c> (which fire the door's lock relays).</summary>
    public static bool SetLocked(Door door, bool locked)
    {
        if (door == null)
            throw new ArgumentNullException(nameof(door));
        if (door.Locked == locked)
            return true;
        if (SaveGate.IsBusy)
            return false;

        using (WorldChanges.EnterMod())
        {
            if (locked)
                door.Lock();
            else
                door.Unlock();
        }
        return door.Locked == locked;
    }

    /// <summary>
    /// Brings a door to a state received from elsewhere: the lock first, then open or closed (with the
    /// same swing side as the source). A door that is only part way is left to finish its own swing.
    /// False when part of it didn't take.
    /// </summary>
    public static bool Apply(Door door, DoorState state)
    {
        if (!SetLocked(door, state.Locked))
            return false;
        switch (state.Phase)
        {
            case DoorPhase.Open:
            case DoorPhase.Opening:
                return Open(door, state.Swing);
            case DoorPhase.Closed:
            case DoorPhase.Closing:
                return Close(door);
            default:
                return true;
        }
    }

    internal static void ClearCaches() => DoorWatch.Reset();
}
