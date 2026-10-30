// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using OS.Items;

namespace ObenFramework.Economy;

/// <summary>
/// The stacks of a set of slots as they were, to put every one back when a trade from code fails halfway.
/// Only the stacks' own fields are written back, so slots keep their stack objects and the UI its links.
/// </summary>
internal sealed class StackSnapshot
{
    private readonly List<Entry> _entries = new List<Entry>();

    public StackSnapshot(IEnumerable<SlotController> slots)
    {
        if (slots == null)
            return;
        foreach (var slot in slots)
        {
            var stack = slot != null ? slot.itemStack : null;
            if (stack == null)
                continue;
            _entries.Add(new Entry
            {
                Slot = slot,
                Stack = stack,
                ItemId = stack.itemId,
                Amount = stack.itemAmount,
                OwnerId = stack.ownerId,
                Reference = stack.itemReference,
                Refrigerated = stack.refigerated,
                SpoilDisabled = stack.spoilDisabled,
                Meta = stack.itemId != -1 && stack.itemAmount > 0 ? stack.Meta : null,
            });
        }
    }

    /// <summary>Units of an item across the captured slots as they are now.</summary>
    public int Units(int itemId)
    {
        int total = 0;
        foreach (var entry in _entries)
        {
            if (entry.Stack.itemId == itemId && entry.Stack.itemAmount > 0)
                total += entry.Stack.itemAmount;
        }
        return total;
    }

    public void Restore()
    {
        foreach (var entry in _entries)
        {
            var stack = entry.Stack;
            stack.itemId = entry.ItemId;
            stack.itemAmount = entry.Amount;
            stack.ownerId = entry.OwnerId;
            stack.itemReference = entry.Reference;
            stack.refigerated = entry.Refrigerated;
            stack.spoilDisabled = entry.SpoilDisabled;
            // With the item id back first, the setter keeps the array as it is (an empty stack gets null).
            stack.Meta = entry.Meta;
            entry.Slot.itemStack = stack;
        }
        foreach (var entry in _entries)
            entry.Slot.UpdateNow();
    }

    private sealed class Entry
    {
        public SlotController Slot;
        public ItemStack Stack;
        public int ItemId;
        public int Amount;
        public int OwnerId;
        public ItemReference Reference;
        public float Refrigerated;
        public bool SpoilDisabled;
        public object[] Meta;
    }
}
