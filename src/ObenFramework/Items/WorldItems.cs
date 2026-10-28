using UnityEngine;

namespace ObenFramework.Items;

/// <summary>
/// Puts item stacks into the world as pickups. The spawned object is a normal pickup: the player can
/// take it, and the game saves it with the level.
/// </summary>
public static class WorldItems
{
    // The layers the game itself uses to find the ground when it drops an item.
    private const int GroundMask = 8523777;

    /// <summary>
    /// Spawns a stack at an exact position. Returns the pickup object, or null when the item is
    /// unknown or has no pickup prefab. The stack is one pickup however large it is; the amount is
    /// not split to the item's stack size.
    /// </summary>
    public static GameObject Spawn(Item item, int amount, Vector3 position)
    {
        if (item == null || amount <= 0 || ItemDatabase.instance == null)
            return null;

        var stack = ItemStack.CreateStackFromItem(item, amount);
        var prefab = amount == 1 ? item.Prefab : item.PrefabMany;
        if (prefab == null)
        {
            ItemsLog.Warn($"Item {item.ID} ({item.Title}) has no pickup prefab, so it can't be spawned.");
            return null;
        }
        return ItemOperations.SpawnItemToPosition(stack, position, default);
    }

    public static GameObject Spawn(int id, int amount, Vector3 position) =>
        Spawn(ItemLookup.ById(id), amount, position);

    /// <summary>
    /// Spawns a stack on the ground just in front of the player. Returns null when there is no player
    /// or the item can't be spawned.
    /// </summary>
    public static GameObject SpawnAtPlayer(Item item, int amount)
    {
        if (PlayerLocator.instance == null)
            return null;

        var player = PlayerLocator.instance.transform;
        var position = player.position + player.forward * 0.6f;
        if (Physics.Raycast(position + Vector3.up, Vector3.down, out var hit, 3f, GroundMask))
            position = hit.point;
        return Spawn(item, amount, position);
    }

    public static GameObject SpawnAtPlayer(int id, int amount) => SpawnAtPlayer(ItemLookup.ById(id), amount);
}
