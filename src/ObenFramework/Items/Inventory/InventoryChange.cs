using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>
/// How the count of one item in the player's inventory changed, raised by <see cref="InventoryEvents.ItemAdded"/>
/// and <see cref="InventoryEvents.ItemRemoved"/>.
/// </summary>
public readonly struct InventoryChange
{
    public InventoryChange(int itemId, int amount, int total, ChangeSource source)
    {
        ItemId = itemId;
        Amount = amount;
        Total = total;
        Source = source;
    }

    public int ItemId { get; }

    /// <summary>How many were added or removed. Always positive.</summary>
    public int Amount { get; }

    /// <summary>How many the player has now (what <see cref="PlayerItems.Count"/> returns).</summary>
    public int Total { get; }

    /// <summary>
    /// <see cref="ChangeSource.Mod"/> when a mod caused it (the framework's own calls, or a call inside
    /// <see cref="WorldChanges.Enter"/>), <see cref="ChangeSource.LocalPlayer"/> for the inventory window,
    /// otherwise <see cref="ChangeSource.Game"/>.
    /// </summary>
    public ChangeSource Source { get; }
}

/// <summary>What the player holds in one hand changed, raised by <see cref="InventoryEvents.HeldChanged"/>.</summary>
public readonly struct HeldChange
{
    public HeldChange(bool leftHand, int itemId, int previousItemId)
    {
        LeftHand = leftHand;
        ItemId = itemId;
        PreviousItemId = previousItemId;
    }

    /// <summary>The secondary (left) hand slot; false for the primary (right) one.</summary>
    public bool LeftHand { get; }

    /// <summary>The item now in the hand slot, or -1 when it is empty.</summary>
    public int ItemId { get; }

    public int PreviousItemId { get; }
}
