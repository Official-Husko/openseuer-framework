// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Items;
using ObenFramework.Saves;
using OS.Items;

namespace ObenFramework.Player;

/// <summary>What a worn slot held before and after a change, from <see cref="Wardrobe.Changed"/>.</summary>
public readonly struct WardrobeChange
{
    public WardrobeChange(string slot, int before, int after)
    {
        Slot = slot;
        Before = before;
        After = after;
    }

    /// <summary>"Head", "Face", "Watch" or "Backpack".</summary>
    public string Slot { get; }

    /// <summary>Item id worn before, or -1 for nothing.</summary>
    public int Before { get; }

    /// <summary>Item id worn now, or -1 for nothing.</summary>
    public int After { get; }
}

/// <summary>
/// The things the player wears in the character slots: hat or helmet (Head), mask or glasses (Face),
/// watch and backpack. Read what is worn, put something on from the bag or take it off the way the
/// inventory screen does, and hear about every change. The two hand slots are tools, not clothes; see
/// <see cref="Weapons"/>.
/// </summary>
public static class Wardrobe
{
    private static readonly string[] SlotNames = { "Head", "Face", "Watch", "Backpack" };

    // Where the game keeps them in Inventory.CharacterSlots (see Inventory.Equip).
    private static readonly int[] SlotIndexes = { 2, 3, 4, 5 };

    /// <summary>Something was put on or taken off, by the player in the inventory or by a mod.</summary>
    public static readonly GameEvent<WardrobeChange> Changed = new GameEvent<WardrobeChange>(nameof(Changed));

    public static IReadOnlyList<string> Slots => SlotNames;

    internal static int SlotCount => SlotNames.Length;

    internal static string NameOf(int slot) => SlotNames[slot];

    /// <summary>The item id worn on a slot, or -1 for nothing or without a player.</summary>
    public static int WornOn(string slot) => Worn(IndexOf(slot));

    /// <summary>
    /// Puts on an item from the bag in the slot its category says, swapping out what was there. False when
    /// the item isn't in the bag, isn't wearable, belongs to someone who doesn't allow its use, or the worn
    /// one belongs to someone else.
    /// </summary>
    public static bool Equip(int itemId)
    {
        var inventory = Inventory.instance;
        if (inventory == null || inventory.Slots == null || inventory.CharacterSlots == null || SaveGate.IsBusy)
            return false;

        var source = FindInBag(inventory, itemId);
        if (source == null || !UseAllowed(source))
            return false;
        var item = ItemLookup.ById(itemId);
        int slot = SlotFor(item);
        if (slot < 0)
            return false;

        var worn = inventory.CharacterSlots[SlotIndexes[slot]];
        if (worn == null || !worn.CheckIfAllowed(item) || OwnedBySomeoneElse(worn))
            return false;
        inventory.EquipItem(SlotNames[slot], source);
        return Worn(slot) == itemId;
    }

    /// <summary>Takes off what is worn on a slot into the bag. False when nothing is worn or it isn't the player's.</summary>
    public static bool Unequip(string slot)
    {
        int index = IndexOf(slot);
        var inventory = Inventory.instance;
        if (inventory == null || inventory.CharacterSlots == null || SaveGate.IsBusy)
            return false;
        var worn = inventory.CharacterSlots[SlotIndexes[index]];
        if (ItemIn(worn) < 0 || OwnedBySomeoneElse(worn))
            return false;
        inventory.Unequip(worn);
        return ItemIn(worn) < 0;
    }

    /// <summary>The item id on a slot by position (0 Head .. 3 Backpack), or -1.</summary>
    internal static int Worn(int slot)
    {
        var inventory = Inventory.instance;
        if (inventory == null || inventory.CharacterSlots == null || SlotIndexes[slot] >= inventory.CharacterSlots.Length)
            return -1;
        return ItemIn(inventory.CharacterSlots[SlotIndexes[slot]]);
    }

    internal static void RaiseChanged(int slot, int before, int after) =>
        Changed.Raise(new WardrobeChange(SlotNames[slot], before, after), GameEvents.Log);

    private static int IndexOf(string slot)
    {
        int index = Array.IndexOf(SlotNames, slot);
        if (index < 0)
            throw new ArgumentException($"Unknown slot '{slot}'. Known: {string.Join(", ", SlotNames)}", nameof(slot));
        return index;
    }

    private static int ItemIn(SlotController slot)
    {
        if (slot == null || slot.itemStack == null || slot.itemStack.itemAmount <= 0)
            return -1;
        return slot.itemStack.itemId;
    }

    private static SlotController FindInBag(Inventory inventory, int itemId)
    {
        foreach (var slot in inventory.Slots)
        {
            if (slot != null && ItemIn(slot) == itemId)
                return slot;
        }
        return null;
    }

    private static int SlotFor(Item item)
    {
        if (item == null || item.Categories == null)
            return -1;
        foreach (string category in item.Categories)
        {
            int index = Array.IndexOf(SlotNames, category);
            if (index >= 0)
                return index;
        }
        return -1;
    }

    private static bool OwnedBySomeoneElse(SlotController slot) => ItemIn(slot) >= 0 && slot.itemStack.ownerId > 0;

    // An item that belongs to an NPC carries a permission; without one it is the player's to use.
    private static bool UseAllowed(SlotController slot)
    {
        var meta = slot.itemStack.Meta;
        if (meta == null)
            return true;
        foreach (var entry in meta)
        {
            if (entry is ItemOwnerPermission permission)
                return permission.allowUse;
        }
        return true;
    }
}
