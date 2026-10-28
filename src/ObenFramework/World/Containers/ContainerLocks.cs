using System;
using System.Collections.Generic;

namespace ObenFramework.World;

/// <summary>A container's edit lock was taken or released.</summary>
public readonly struct ContainerLockChange
{
    public ContainerLockChange(string containerId, string owner, string previousOwner)
    {
        ContainerId = containerId;
        Owner = owner;
        PreviousOwner = previousOwner;
    }

    public string ContainerId { get; }

    /// <summary>The new holder, or null when the lock was released.</summary>
    public string Owner { get; }

    public string PreviousOwner { get; }
}

/// <summary>
/// A simple, advisory lock per container so that only one user edits it at a time. The owner is any text
/// (the co-op mod uses a player id). <see cref="Containers.Add"/> and <see cref="Containers.Remove"/> refuse
/// to change a container that another owner holds, and the local player cannot open one that a remote owner
/// holds. It is not the game's own key lock (<see cref="ContainerInfo.GameLocked"/>) and it is not saved: all
/// locks are dropped when the scene changes, because the containers they were about are gone.
/// </summary>
public static class ContainerLocks
{
    private static readonly Dictionary<string, string> Held = new Dictionary<string, string>();

    /// <summary>The owner name the local player's own actions use. Set it to the local player's id when other machines also take locks.</summary>
    public static string LocalOwner { get; set; } = "local";

    /// <summary>
    /// When true the local player takes the lock for the container they open and gives it back when they
    /// close it, so a remote user cannot edit it meanwhile. Off by default.
    /// </summary>
    public static bool LockWhileLocalPlayerHasOpen { get; set; }

    /// <summary>Raised whenever a lock is taken, handed over or released.</summary>
    public static readonly WorldEvent<ContainerLockChange> LockChanged = new WorldEvent<ContainerLockChange>(nameof(LockChanged));

    internal static bool Any => Held.Count > 0;

    /// <summary>Takes the lock for <paramref name="owner"/>. True when the owner holds it afterwards (it was free or already theirs).</summary>
    public static bool TryLock(string containerId, string owner)
    {
        if (string.IsNullOrEmpty(containerId))
            throw new ArgumentException("No container id given.", nameof(containerId));
        if (string.IsNullOrEmpty(owner))
            throw new ArgumentException("No owner given.", nameof(owner));

        if (Held.TryGetValue(containerId, out var holder))
            return holder == owner;
        Held[containerId] = owner;
        LockChanged.Raise(new ContainerLockChange(containerId, owner, null));
        return true;
    }

    /// <summary>Releases the lock. Only its holder can; returns false when the container was not locked or someone else holds it.</summary>
    public static bool Unlock(string containerId, string owner)
    {
        if (string.IsNullOrEmpty(containerId) || !Held.TryGetValue(containerId, out var holder) || holder != owner)
            return false;
        Held.Remove(containerId);
        LockChanged.Raise(new ContainerLockChange(containerId, null, holder));
        return true;
    }

    /// <summary>Who holds the lock, or null.</summary>
    public static string OwnerOf(string containerId) =>
        !string.IsNullOrEmpty(containerId) && Held.TryGetValue(containerId, out var owner) ? owner : null;

    /// <summary>True when someone other than <paramref name="owner"/> holds the lock.</summary>
    public static bool IsHeldByOther(string containerId, string owner) =>
        OwnerOf(containerId) is string holder && holder != owner;

    /// <summary>Releases everything an owner holds, such as when a player leaves. Returns how many.</summary>
    public static int ReleaseAll(string owner)
    {
        var mine = new List<string>();
        foreach (var pair in Held)
        {
            if (pair.Value == owner)
                mine.Add(pair.Key);
        }
        foreach (var id in mine)
            Unlock(id, owner);
        return mine.Count;
    }

    /// <summary>The locks held now, as container id and owner.</summary>
    public static List<KeyValuePair<string, string>> All() => new List<KeyValuePair<string, string>>(Held);

    internal static void Clear() => Held.Clear();
}
