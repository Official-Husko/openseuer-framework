using UnityEngine;

namespace ObenFramework.World;

/// <summary>A snapshot of one container (the game's <c>Storage</c>: a drawer, chest, fridge, crate, a machine's slots).</summary>
public readonly struct ContainerInfo
{
    public ContainerInfo(Storage storage, string id, string title, string scene, Vector3 position, int slotCount,
        bool isEmpty, bool gameLocked, int npcOwner, string editOwner)
    {
        Storage = storage;
        Id = id;
        Title = title;
        Scene = scene;
        Position = position;
        SlotCount = slotCount;
        IsEmpty = isEmpty;
        GameLocked = gameLocked;
        NpcOwner = npcOwner;
        EditOwner = editOwner;
    }

    /// <summary>The live object.</summary>
    public Storage Storage { get; }

    /// <summary>
    /// The container's address, see <see cref="Containers.IdOf"/>. The same text finds it again with
    /// <see cref="Containers.Find"/> in another session or on another machine.
    /// </summary>
    public string Id { get; }

    public string Title { get; }

    public string Scene { get; }

    public Vector3 Position { get; }

    public int SlotCount { get; }

    public bool IsEmpty { get; }

    /// <summary>The game's own lock (a key or lockpick opens it), not <see cref="ContainerLocks"/>.</summary>
    public bool GameLocked { get; }

    /// <summary>The id of the character the contents belong to (taking from it is stealing), or -1 when it belongs to nobody.</summary>
    public int NpcOwner { get; }

    /// <summary>Who holds the container's edit lock (see <see cref="ContainerLocks"/>), or null.</summary>
    public string EditOwner { get; }
}

/// <summary>One stack in a container.</summary>
public readonly struct ContainerItem
{
    public ContainerItem(int slot, int itemId, string title, int amount, int owner)
    {
        Slot = slot;
        ItemId = itemId;
        Title = title;
        Amount = amount;
        Owner = owner;
    }

    /// <summary>Index in the container's slots.</summary>
    public int Slot { get; }

    public int ItemId { get; }

    public string Title { get; }

    public int Amount { get; }

    /// <summary>Id of the character the stack belongs to, -1 for nobody.</summary>
    public int Owner { get; }
}

/// <summary>The container an event is about, raised by <see cref="Containers.Opened"/>, <see cref="Containers.Closed"/> and <see cref="Containers.Changed"/>.</summary>
public readonly struct ContainerEvent
{
    public ContainerEvent(ContainerInfo container, ChangeSource source)
    {
        Container = container;
        Source = source;
    }

    public ContainerInfo Container { get; }

    public ChangeSource Source { get; }

    public bool ByLocalPlayer => Source == ChangeSource.LocalPlayer;
}
