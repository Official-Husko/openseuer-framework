using UnityEngine;

namespace ObenFramework.World;

/// <summary>A snapshot of an item lying in the world (a pickup).</summary>
public readonly struct FloorItemInfo
{
    public FloorItemInfo(GameObject gameObject, string id, int itemId, string title, int amount, Vector3 position, string scene)
    {
        GameObject = gameObject;
        Id = id;
        ItemId = itemId;
        Title = title;
        Amount = amount;
        Position = position;
        Scene = scene;
    }

    /// <summary>The pickup object. Unity-null once it was taken; the other fields stay valid.</summary>
    public GameObject GameObject { get; }

    /// <summary>The id for this session (see <see cref="FloorItems.IdOf"/>).</summary>
    public string Id { get; }

    /// <summary>The item's id in the item database; 0 and 1 are the coins and real money.</summary>
    public int ItemId { get; }

    public string Title { get; }

    /// <summary>How many the pickup holds. For pickups that were picked up this is how many were taken.</summary>
    public int Amount { get; }

    public Vector3 Position { get; }

    public string Scene { get; }
}

/// <summary>A pickup appeared, was dropped or was picked up, as raised by the events of <see cref="FloorItems"/>.</summary>
public readonly struct FloorItemEvent
{
    public FloorItemEvent(FloorItemInfo item, ChangeSource source)
    {
        Item = item;
        Source = source;
    }

    public FloorItemInfo Item { get; }

    public ChangeSource Source { get; }

    public bool ByLocalPlayer => Source == ChangeSource.LocalPlayer;
}
