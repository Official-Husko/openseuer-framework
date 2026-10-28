// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Saves;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Items;

/// <summary>
/// What the player holds: the primary (right) and secondary (left) hand slots of the inventory, and
/// equipping or putting away an item there the way the inventory window does. Changes are reported by
/// <see cref="InventoryEvents.HeldChanged"/>.
/// </summary>
public static class HeldItems
{
    private const int RightHand = 6;
    private const int LeftHand = 7;
    private const string Unstorable = "Unstorable";

    /// <summary>The item in the primary hand slot, or -1.</summary>
    public static int Primary => Get(false);

    /// <summary>The item in the secondary hand slot, or -1.</summary>
    public static int Secondary => Get(true);

    /// <summary>The item in a hand slot, or -1 when it is empty or there is no inventory.</summary>
    public static int Get(bool leftHand)
    {
        var inventory = Inventory.instance;
        return inventory != null ? InventoryWatch.HandItem(inventory, leftHand ? LeftHand : RightHand) : -1;
    }

    /// <summary>
    /// Moves a single, unowned stack of the item from the inventory grid into the hand slot its category
    /// asks for, swapping out what was there. Refuses (returns false) when the item isn't wieldable, the
    /// player has no such stack, the slot holds someone else's item, a two-handed item is involved while the
    /// other hand is full, or a save is loading.
    /// </summary>
    public static bool Equip(int itemId)
    {
        var inventory = Inventory.instance;
        var item = ItemLookup.ById(itemId);
        if (inventory == null || inventory.Slots == null || inventory.CharacterSlots == null || item == null || SaveGate.IsBusy)
            return false;

        int index = Inventory.GetEquipSlotIndex(item);
        if (index != RightHand && index != LeftHand)
            return false;

        var source = SingleStack(inventory.Slots, itemId);
        var hand = inventory.CharacterSlots[index];
        if (source == null || !hand.CheckIfAllowed(item) || Owned(hand) || TwoHandClash(inventory, index, item))
            return false;

        using (WorldChanges.EnterMod())
        {
            inventory.EquipItem(index == RightHand ? Inventory.wieldablePrimary : Inventory.wieldableSecondary, source);
            InventoryWatch.Check();
        }
        return hand.itemStack.itemId == itemId;
    }

    /// <summary>
    /// Puts the item in a hand slot back into the inventory. Refuses when the hand is empty, holds someone
    /// else's item, holds an item that can't go in the inventory, or a save is loading. Returns true when the
    /// hand is empty afterwards (false also when the inventory had no room).
    /// </summary>
    public static bool Unequip(bool leftHand)
    {
        var inventory = Inventory.instance;
        if (inventory == null || inventory.CharacterSlots == null || SaveGate.IsBusy)
            return false;

        var hand = inventory.CharacterSlots[leftHand ? LeftHand : RightHand];
        if (Empty(hand) || Owned(hand))
            return false;
        var item = ItemLookup.ById(hand.itemStack.itemId);
        if (item == null || (item.Categories != null && Array.IndexOf(item.Categories, Unstorable) >= 0))
            return false;

        using (WorldChanges.EnterMod())
        {
            inventory.Unequip(hand);
            InventoryWatch.Check();
        }
        return Empty(hand);
    }

    private static SlotController SingleStack(SlotController[] slots, int itemId)
    {
        foreach (var slot in slots)
        {
            if (slot != null && slot.itemStack.itemId == itemId && slot.itemStack.itemAmount == 1 && slot.itemStack.ownerId <= 0)
                return slot;
        }
        return null;
    }

    private static bool Empty(SlotController slot) =>
        slot == null || slot.itemStack == null || slot.itemStack.itemId == -1 || slot.itemStack.itemAmount <= 0;

    private static bool Owned(SlotController slot) => !Empty(slot) && slot.itemStack.ownerId > 0;

    private static bool TwoHandClash(Inventory inventory, int index, Item item)
    {
        var other = inventory.CharacterSlots[index == RightHand ? LeftHand : RightHand];
        return !Empty(other) && (TwoHanded(item) || TwoHanded(ItemLookup.ById(other.itemStack.itemId)));
    }

    private static bool TwoHanded(Item item) =>
        item != null && item.Actions != null && item.GetActionOfType<ItemEquipable>() is ItemEquipable equipable && equipable.bothHands;
}
