// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Items;

/// <summary>A snapshot of a breakable object (a window, a crate, a vending machine's glass).</summary>
public readonly struct BreakableInfo
{
    public BreakableInfo(string id, string name, Vector3 position, bool broken, bool repairs)
    {
        Id = id;
        Name = name;
        Position = position;
        Broken = broken;
        Repairs = repairs;
    }

    public string Id { get; }

    public string Name { get; }

    public Vector3 Position { get; }

    public bool Broken { get; }

    /// <summary>It repairs itself after a while.</summary>
    public bool Repairs { get; }
}

/// <summary>A snapshot of a lock that can be broken open.</summary>
public readonly struct BreakableLockInfo
{
    public BreakableLockInfo(string id, Vector3 position, bool locked, int strength)
    {
        Id = id;
        Position = position;
        Locked = locked;
        Strength = strength;
    }

    public string Id { get; }

    public Vector3 Position { get; }

    public bool Locked { get; }

    /// <summary>Hits left before it breaks; 0 when broken.</summary>
    public int Strength { get; }
}

/// <summary>Something that drops items when smashed (a barrel, a bin, a rat) was destroyed, raised by <see cref="Breakables.Smashed"/>.</summary>
public readonly struct SmashedObject
{
    public SmashedObject(string id, Vector3 position, bool animal, int itemId)
    {
        Id = id;
        Position = position;
        Animal = animal;
        ItemId = itemId;
    }

    public string Id { get; }

    public Vector3 Position { get; }

    public bool Animal { get; }

    /// <summary>The item it spawns when smashed, or -1.</summary>
    public int ItemId { get; }
}

/// <summary>
/// Breakable objects, breakable locks and smashable item droppers in the loaded scenes: listing them, breaking
/// and repairing them, and events for each. Ids work like container ids: the GUID, or <c>furniture:{id}:{n}</c>
/// inside placed furniture. Nothing is raised while a save loads (the game breaks things again to restore them).
/// </summary>
public static class Breakables
{
    /// <summary>
    /// The framework's item hooks are in place. False when they failed to apply (a game update changed what they
    /// patch); the events then stay quiet and changes the game should pick up may not take.
    /// </summary>
    public static bool IsAvailable => ItemsModule.HooksInPlace;

    /// <summary>An object broke. The payload is its id.</summary>
    public static readonly WorldEvent<string> Broken = new WorldEvent<string>(nameof(Broken));

    /// <summary>A broken object repaired itself or was repaired. The payload is its id.</summary>
    public static readonly WorldEvent<string> Repaired = new WorldEvent<string>(nameof(Repaired));

    /// <summary>A lock was broken open. The payload is its id.</summary>
    public static readonly WorldEvent<string> LockBroken = new WorldEvent<string>(nameof(LockBroken));

    /// <summary>A smashable item dropper was destroyed.</summary>
    public static readonly WorldEvent<SmashedObject> Smashed = new WorldEvent<SmashedObject>(nameof(Smashed));

    public static List<BreakableInfo> All()
    {
        var list = new List<BreakableInfo>();
        foreach (var breakable in ObjectIds.Active<Breakable>())
            list.Add(Describe(breakable));
        return list;
    }

    public static Breakable Find(string id) => ObjectIds.Find<Breakable>(id);

    public static bool IsBroken(string id)
    {
        var breakable = Find(id);
        return breakable != null && breakable.strength <= 0;
    }

    /// <summary>Breaks an object as if it took enough damage. True when it is broken afterwards.</summary>
    public static bool Break(string id)
    {
        var breakable = Find(id);
        if (breakable == null || breakable.strength <= 0 || SaveGate.IsBusy || breakable.transform.parent == null)
            return false;
        using (WorldChanges.EnterMod())
            breakable.Break(breakable.strength, Vector3.zero, Vector3.zero, DamageReceiver.DamageType.Any);
        return breakable.strength <= 0;
    }

    /// <summary>Repairs a broken object that can repair itself. True when it is whole afterwards.</summary>
    public static bool Repair(string id)
    {
        var breakable = Find(id);
        if (breakable == null || breakable.strength > 0 || breakable.repairAfterTime < 0 || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            breakable.Repair();
        return breakable.strength > 0;
    }

    public static List<BreakableLockInfo> Locks()
    {
        var list = new List<BreakableLockInfo>();
        foreach (var lck in ObjectIds.Active<BreakableLock>())
            list.Add(new BreakableLockInfo(ObjectIds.IdOf(lck), lck.transform.position, lck.Locked, lck.strength));
        return list;
    }

    /// <summary>Breaks a lock open. True when it is broken afterwards.</summary>
    public static bool BreakLock(string id)
    {
        var lck = ObjectIds.Find<BreakableLock>(id);
        if (lck == null || lck.strength <= 0 || SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            lck.Break(lck.strength, DamageReceiver.DamageType.Any);
        return lck.strength <= 0;
    }

    public static BreakableInfo Describe(Breakable breakable) =>
        new BreakableInfo(ObjectIds.IdOf(breakable), breakable.name, breakable.transform.position, breakable.strength <= 0, breakable.repairAfterTime >= 0);
}
