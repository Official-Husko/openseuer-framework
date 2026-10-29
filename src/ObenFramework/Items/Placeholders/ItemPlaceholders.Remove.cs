// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>A stack of a missing mod item, from <see cref="ItemPlaceholders.Scan"/>.</summary>
public readonly struct MissingStack
{
    public MissingStack(int itemId, string mod, string title, int amount, string where)
    {
        ItemId = itemId;
        Mod = mod;
        Title = title;
        Amount = amount;
        Where = where;
    }

    public int ItemId { get; }

    /// <summary>The mod the save says the item came from, or null when the save doesn't say.</summary>
    public string Mod { get; }

    public string Title { get; }

    public int Amount { get; }

    /// <summary>"inventory", or the id of the container (<see cref="Containers.IdOf"/>).</summary>
    public string Where { get; }

    public override string ToString() => $"{Amount}x {Title ?? ItemId.ToString()} ({Mod ?? "unknown mod"}) in {Where}";
}

public static partial class ItemPlaceholders
{
    /// <summary>
    /// Every stack of a missing mod item in the player's inventory and the containers of the loaded scenes,
    /// with its mod, amount and place. Changes nothing; see <see cref="RemoveMissing"/> to delete them.
    /// </summary>
    public static List<MissingStack> Scan()
    {
        var found = new List<MissingStack>();
        Walk(null, (stack, where) =>
        {
            var entry = Manifest.Find(stack.itemId);
            found.Add(new MissingStack(stack.itemId, entry?.Mod, entry?.Title, stack.itemAmount, where));
            return false;
        });
        return found;
    }

    /// <summary>
    /// Deletes the stacks of the missing items the save lists for <paramref name="modId"/> from the player's
    /// inventory and the containers in the loaded scenes, and forgets them in the save's list. For a mod the
    /// player removed for good; the items can't come back afterwards. Stacks in other scenes, on the floor
    /// or in loot that hasn't been rolled are not touched. Returns how many stacks were deleted.
    /// </summary>
    public static int RemoveMissing(string modId)
    {
        if (string.IsNullOrEmpty(modId))
            throw new ArgumentException("Name the mod whose items go; RemoveAllMissing deletes every mod's.", nameof(modId));
        return Remove(modId);
    }

    /// <summary>
    /// <see cref="RemoveMissing"/> for every missing item, whichever mod it came from (also items the save
    /// has no record of).
    /// </summary>
    public static int RemoveAllMissing() => Remove(null);

    private static int Remove(string modId)
    {
        int removed = Walk(modId, (stack, where) =>
        {
            if (where == InventoryPlace)
                stack.RemoveStack();
            else
                Clear(stack);
            return true;
        });
        Manifest.ForgetMod(modId);
        ItemsLog.Warn($"Deleted {removed} stack(s) of missing items{(modId == null ? "" : " of " + modId)} from the inventory and the loaded containers.");
        return removed;
    }

    private const string InventoryPlace = "inventory";

    // Visits each missing stack (of one mod, or all with null); the visitor returns true when it changed the
    // stack, so the inventory or container is refreshed. Returns how many stacks were changed.
    private static int Walk(string modId, Func<ItemStack, string, bool> visit)
    {
        int changedStacks = 0;
        var inventory = Inventory.instance;
        if (inventory != null)
        {
            int before = changedStacks;
            foreach (var slot in inventory.AllSlots())
            {
                if (slot != null && IsMissing(slot.itemStack, modId) && visit(slot.itemStack, InventoryPlace))
                    changedStacks++;
            }
            if (changedStacks > before)
                inventory.UpdateAllSlots();
        }

        foreach (var storage in Containers.All(includeInactive: true))
        {
            int before = changedStacks;
            string where = null;
            foreach (var stack in storage.storageSlots)
            {
                if (!IsMissing(stack, modId))
                    continue;
                where ??= Containers.IdOf(storage) ?? storage.name;
                if (visit(stack, where))
                    changedStacks++;
            }
            if (changedStacks > before)
                storage.UpdateSlots();
        }
        return changedStacks;
    }

    // A stack of a mod-range id that no mod registers, optionally only the ones the save lists for modId.
    private static bool IsMissing(ItemStack stack, string modId)
    {
        if (stack == null || stack.itemAmount <= 0 || !ReservedIds.Contains(stack.itemId) || ItemRegistry.IsModded(stack.itemId))
            return false;
        if (modId == null)
            return true;
        var entry = Manifest.Find(stack.itemId);
        return entry != null && string.Equals(entry.Mod, modId, StringComparison.OrdinalIgnoreCase);
    }

    private static void Clear(ItemStack stack)
    {
        stack.itemReference = null;
        stack.itemId = -1;
        stack.itemAmount = 0;
        stack.ownerId = -1;
        stack.Meta = null;
    }
}
