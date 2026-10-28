// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Items;

/// <summary>One stack in a container of a scene that is not loaded, as the save file has it.</summary>
public readonly struct UnloadedStack
{
    public UnloadedStack(int slot, int itemId, int amount, int ownerId)
    {
        Slot = slot;
        ItemId = itemId;
        Amount = amount;
        OwnerId = ownerId;
    }

    public int Slot { get; }

    public int ItemId { get; }

    public int Amount { get; }

    /// <summary>Id of the character the stack belongs to, -1 for nobody.</summary>
    public int OwnerId { get; }
}

/// <summary>The contents of a container in a scene that is not loaded, read from the save.</summary>
public sealed class UnloadedContainer
{
    public UnloadedContainer(string guid, List<UnloadedStack> stacks)
    {
        Guid = guid;
        Stacks = stacks;
    }

    /// <summary>The container's GUID in the save, the same as <see cref="World.Containers.IdOf"/> gives while its scene is loaded.</summary>
    public string Guid { get; }

    /// <summary>The non-empty slots.</summary>
    public List<UnloadedStack> Stacks { get; }

    /// <summary>How many of an item the container holds.</summary>
    public int Amount(int itemId)
    {
        int total = 0;
        foreach (var stack in Stacks)
        {
            if (stack.ItemId == itemId)
                total += stack.Amount;
        }
        return total;
    }
}

/// <summary>A queued change to a container in a scene that is not loaded, raised by <see cref="UnloadedStorage.Applied"/> and <see cref="UnloadedStorage.Failed"/>.</summary>
public readonly struct UnloadedChange
{
    internal UnloadedChange(UnloadedStorageEntry entry, string reason = null)
    {
        ModId = entry.Mod;
        Scene = entry.Scene;
        Guid = entry.Guid;
        ItemId = entry.ItemId;
        Amount = entry.Count;
        Give = entry.Give;
        Reason = reason;
    }

    /// <summary>The mod that queued it.</summary>
    public string ModId { get; }

    public string Scene { get; }

    /// <summary>The container's GUID.</summary>
    public string Guid { get; }

    public int ItemId { get; }

    public int Amount { get; }

    /// <summary>True for items put into the container, false for items taken out.</summary>
    public bool Give { get; }

    /// <summary>Why it failed, for <see cref="UnloadedStorage.Failed"/>; null otherwise.</summary>
    public string Reason { get; }
}

/// <summary>One queued change as it is kept in the save.</summary>
[Serializable]
internal sealed class UnloadedStorageEntry
{
    public string Mod;
    public string Scene;
    public string Guid;
    public int ItemId;
    public int Count;
    public bool Give;
}

/// <summary>The queue, stored per save through ModSaves.</summary>
[Serializable]
internal sealed class UnloadedStorageFile
{
    public List<UnloadedStorageEntry> Entries = new List<UnloadedStorageEntry>();
}
