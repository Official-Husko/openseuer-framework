// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// The game's key locks: on doors, doors to other levels, storages, stations, switches, padlocks and market
/// stalls. Read and change them, set which key opens them, hand the player the key, start the lockpicking
/// game, and hear when any lock is locked, unlocked or picked. Keypads, which have a code instead of a key,
/// are in <see cref="Keypads"/>.
/// </summary>
public static class Locks
{
    /// <summary>Highest lockpicking difficulty the game uses.</summary>
    public const float MaxDifficulty = 2f;

    /// <summary>
    /// A lock was locked or unlocked: by a key, a lockpick, a timer, a schedule, a script or a mod. Not raised
    /// while a save loads.
    /// </summary>
    public static readonly WorldEvent<LockChange> Changed = new WorldEvent<LockChange>(nameof(Changed));

    /// <summary>The lockpicking game ended, in success or failure (not when the player walks away).</summary>
    public static readonly WorldEvent<LockpickResult> Lockpicked = new WorldEvent<LockpickResult>(nameof(Lockpicked));

    /// <summary>Every component with a lock in the loaded scenes. A scan of the ten lock-holding types.</summary>
    public static List<Component> All() => LockHolders.All();

    /// <summary>The lock-holding component with this id (see <see cref="IdOf"/>), or null.</summary>
    public static Component Find(string id)
    {
        var target = ObjectIds.Find<Interactable>(id);
        return target != null ? LockHolders.HolderOn(target.gameObject) : null;
    }

    public static string IdOf(Component owner) => owner is Interactable interactable ? ObjectIds.IdOf(interactable) : ObjectIds.IdOf(owner);

    /// <summary>The game's lock object of a component, or null when it has none.</summary>
    public static Lock LockOf(Component owner) => LockHolders.LockOf(owner, out _);

    /// <summary>The first lock-holding component on a GameObject, or null.</summary>
    public static Component HolderOn(GameObject go) => LockHolders.HolderOn(go);

    public static LockInfo Describe(Component owner)
    {
        var target = Require(owner, out var kind);
        var key = target.keyItem;
        bool hasKey = key != null && TaskItemsManager.instance != null && TaskItemsManager.instance.CheckIfPlayerHasTaskItem(key);
        return new LockInfo(owner, IdOf(owner), kind, target.Locked, target.Lockpickable, target.Difficulty,
            key != null ? key.itemName : null, hasKey, target.LockAfterTime);
    }

    public static bool IsLocked(Component owner) => Require(owner, out _).Locked;

    /// <summary>
    /// Locks or unlocks through the owner's own methods where it has them (doors fire their lock relays), else
    /// the lock's. Returns whether the lock is in the asked state afterwards.
    /// </summary>
    public static bool SetLocked(Component owner, bool locked)
    {
        var target = Require(owner, out _);
        if (target.Locked == locked)
            return true;
        using (WorldChanges.EnterMod())
        {
            switch (owner)
            {
                case Door door:
                    if (locked) door.Lock(); else door.Unlock();
                    break;
                case DoorChangelevel levelDoor:
                    if (locked) levelDoor.Lock(); else levelDoor.UnLock();
                    break;
                default:
                    if (locked) target._Lock(); else target._Unlock();
                    break;
            }
        }
        return target.Locked == locked;
    }

    /// <summary>Lets the player pick the lock or not. The game saves it with the object.</summary>
    public static void SetLockpickable(Component owner, bool lockpickable) => Require(owner, out _).Lockpickable = lockpickable;

    /// <summary>Sets how hard the lock is to pick, 0 to <see cref="MaxDifficulty"/>.</summary>
    public static void SetDifficulty(Component owner, float difficulty)
    {
        if (float.IsNaN(difficulty) || difficulty < 0f || difficulty > MaxDifficulty)
            throw new ArgumentOutOfRangeException(nameof(difficulty), "The difficulty must be 0 to 2.");
        Require(owner, out _).Difficulty = difficulty;
    }

    /// <summary>The key item that opens the lock, or null.</summary>
    public static TaskItem KeyOf(Component owner) => Require(owner, out _).keyItem;

    /// <summary>
    /// Sets which key opens the lock (any key task item, a game key or one a mod made); null means no key does.
    /// The key is part of the level's design, not its save: set it again when the level loads.
    /// </summary>
    public static void SetKey(Component owner, TaskItem key) => Require(owner, out _).keyItem = key;

    public static bool PlayerHasKey(Component owner)
    {
        var key = Require(owner, out _).keyItem;
        return key != null && TaskItemsManager.instance != null && TaskItemsManager.instance.CheckIfPlayerHasTaskItem(key);
    }

    /// <summary>Gives the player the key of this lock, with the game's notification. False when it has none or they hold it.</summary>
    public static bool GiveKey(Component owner, bool notify = true)
    {
        var key = Require(owner, out _).keyItem;
        var tasks = TaskItemsManager.instance;
        if (key == null || tasks == null || tasks.CheckIfPlayerHasTaskItem(key))
            return false;
        return tasks.AddTaskItem(key, 1, showNotification: notify);
    }

    /// <summary>
    /// Starts the lockpicking game on a locked, pickable lock as if the player had tried to open it. The game
    /// cancels it when the player looks away, so call it while they face the lock (from an interaction).
    /// <paramref name="done"/> gets true once the lock opened, false on failure or cancel. False when it can't start.
    /// </summary>
    public static bool StartLockpick(Component owner, Action<bool> done, float? difficulty = null)
    {
        if (done == null)
            throw new ArgumentNullException(nameof(done));
        var target = Require(owner, out _);
        var picking = PlayerLockpicking.instance;
        if (!target.Locked || !target.Lockpickable || picking == null || LockPicking.Busy)
            return false;
        LockPicking.Start(owner, target, done);
        picking.ToggleLockpicking(LockPicking.Finished, difficulty ?? target.Difficulty, target);
        return true;
    }

    internal static Lock Require(Component owner, out LockKind kind)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        return LockHolders.LockOf(owner, out kind) ?? throw new ArgumentException($"{owner.name} ({owner.GetType().Name}) has no lock.", nameof(owner));
    }
}

/// <summary>The one lockpicking attempt a mod started through <see cref="Locks.StartLockpick"/>.</summary>
internal static class LockPicking
{
    private static Component _owner;
    private static Lock _lock;
    private static Action<bool> _done;

    public static bool Busy => _lock != null;

    public static bool IsOurs(Lock target) => target != null && ReferenceEquals(target, _lock);

    public static void Start(Component owner, Lock target, Action<bool> done)
    {
        _owner = owner;
        _lock = target;
        _done = done;
    }

    public static void Finished(bool success)
    {
        var owner = _owner;
        var done = _done;
        Clear();
        if (success && owner != null)
            Locks.SetLocked(owner, false);
        if (WorldChanges.Active)
            Locks.Lockpicked.Raise(new LockpickResult(owner, success));
        Call(done, success && owner != null && !Locks.IsLocked(owner));
    }

    public static void Aborted(Lock target)
    {
        if (!IsOurs(target))
            return;
        var done = _done;
        Clear();
        Call(done, false);
    }

    public static void Clear()
    {
        _owner = null;
        _lock = null;
        _done = null;
    }

    private static void Call(Action<bool> done, bool result)
    {
        try { done?.Invoke(result); }
        catch (Exception e) { WorldLog.Failed("A lockpick callback", e); }
    }
}
