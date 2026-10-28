using UnityEngine;

namespace ObenFramework.World;

/// <summary>A snapshot of one lock and what it is on.</summary>
public readonly struct LockInfo
{
    public LockInfo(Component owner, string id, LockKind kind, bool locked, bool lockpickable, float difficulty,
        string keyName, bool playerHasKey, int relockMinutes)
    {
        Owner = owner;
        Id = id;
        Kind = kind;
        Locked = locked;
        Lockpickable = lockpickable;
        Difficulty = difficulty;
        KeyName = keyName;
        PlayerHasKey = playerHasKey;
        RelockMinutes = relockMinutes;
    }

    /// <summary>The door, storage, station or switch the lock is on.</summary>
    public Component Owner { get; }

    public string Id { get; }

    public LockKind Kind { get; }

    public bool Locked { get; }

    public bool Lockpickable { get; }

    /// <summary>How hard it is to pick, 0 (easy) to 2 (hardest), the game's own scale.</summary>
    public float Difficulty { get; }

    /// <summary>The name of the key that opens it, or null when no key does.</summary>
    public string KeyName { get; }

    public bool PlayerHasKey { get; }

    /// <summary>Game minutes after which it locks itself again once opened, or -1 when it stays open.</summary>
    public int RelockMinutes { get; }
}

/// <summary>A lock was locked or unlocked.</summary>
public readonly struct LockChange
{
    public LockChange(Component owner, bool locked, ChangeSource source)
    {
        Owner = owner;
        Locked = locked;
        Source = source;
    }

    /// <summary>The object the lock is on. Null in the rare case the framework can't tell (a lock with no component around it).</summary>
    public Component Owner { get; }

    public bool Locked { get; }

    public ChangeSource Source { get; }

    public string Id => Owner != null ? Locks.IdOf(Owner) : null;
}

/// <summary>A lockpicking attempt ended.</summary>
public readonly struct LockpickResult
{
    public LockpickResult(Component owner, bool success)
    {
        Owner = owner;
        Success = success;
    }

    public Component Owner { get; }

    public bool Success { get; }

    public string Id => Owner != null ? Locks.IdOf(Owner) : null;
}
