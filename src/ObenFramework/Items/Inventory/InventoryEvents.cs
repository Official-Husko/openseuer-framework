using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>
/// Events for the player's inventory: the inventory grid, the clothing and hand slots and the equipped
/// bag, exactly what <see cref="PlayerItems.Count"/> counts. They report net changes per item, so moving a
/// stack between two inventory slots raises nothing, while moving it into a container raises
/// <see cref="ItemRemoved"/>. Subscribe with an owner (<c>InventoryEvents.ItemAdded.Subscribe(this, ...)</c>).
/// </summary>
/// <remarks>
/// The game changes stacks in many places, so the framework doesn't follow each call. Any change to a stack
/// marks the inventory for a recount, and the recount runs once at the next frame (the framework's own
/// calls such as <see cref="PlayerItems.Give(int, int, bool)"/> recount before they return). Several changes
/// in one frame are reported as one net change per item. Nothing is raised while a save loads or between
/// scenes; the inventory the game restores there counts as the starting point. Money is not in the
/// inventory: use the wallet's events for it.
/// </remarks>
public static class InventoryEvents
{
    /// <summary>The player has more of an item than before.</summary>
    public static readonly WorldEvent<InventoryChange> ItemAdded = new WorldEvent<InventoryChange>(nameof(ItemAdded));

    /// <summary>The player has fewer of an item than before.</summary>
    public static readonly WorldEvent<InventoryChange> ItemRemoved = new WorldEvent<InventoryChange>(nameof(ItemRemoved));

    /// <summary>
    /// The player used an item from the inventory (ate, drank, read, applied it). The payload is the item id.
    /// Uses of objects in the world and anything while trading are not reported.
    /// </summary>
    public static readonly WorldEvent<int> ItemUsed = new WorldEvent<int>(nameof(ItemUsed));

    /// <summary>The item in a hand slot (primary or secondary) changed, including the hand becoming empty.</summary>
    public static readonly WorldEvent<HeldChange> HeldChanged = new WorldEvent<HeldChange>(nameof(HeldChanged));

    /// <summary>
    /// Recounts now and raises the events for anything that changed, instead of waiting for the next frame.
    /// Useful after calling the game's own inventory methods when a handler must run before you continue.
    /// </summary>
    public static void Flush() => InventoryWatch.Check();
}
