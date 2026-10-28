// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.World;

namespace ObenFramework.Items;

public static partial class ItemPlaceholders
{
    /// <summary>
    /// Deletes the stacks of missing mod items (all of them, or only those the save lists for
    /// <paramref name="modId"/>) from the player's inventory and the containers in the loaded scenes, and
    /// forgets them in the save's list. For a mod the player removed for good; the items can't come back
    /// afterwards. Stacks in other scenes, on the floor or in loot that hasn't been rolled are not touched.
    /// Returns how many stacks were deleted.
    /// </summary>
    public static int RemoveMissing(string modId = null)
    {
        int removed = 0;
        var inventory = Inventory.instance;
        if (inventory != null)
        {
            foreach (var slot in inventory.AllSlots())
            {
                if (slot != null && IsMissing(slot.itemStack, modId))
                {
                    slot.itemStack.RemoveStack();
                    removed++;
                }
            }
            if (removed > 0)
                inventory.UpdateAllSlots();
        }

        foreach (var storage in Containers.All(includeInactive: true))
        {
            bool changed = false;
            foreach (var stack in storage.storageSlots)
            {
                if (!IsMissing(stack, modId))
                    continue;
                Clear(stack);
                changed = true;
                removed++;
            }
            if (changed)
                storage.UpdateSlots();
        }

        Manifest.ForgetMod(modId);
        ItemsLog.Warn($"Deleted {removed} stack(s) of missing items{(modId == null ? "" : " of " + modId)} from the inventory and the loaded containers.");
        return removed;
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
