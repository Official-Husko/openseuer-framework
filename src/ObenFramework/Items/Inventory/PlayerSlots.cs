using System.Collections.Generic;
using OS.Items;

namespace ObenFramework.Items;

/// <summary>
/// The player's slots in the order the game's own counting uses: the inventory grid, the equipped bag,
/// then the clothing and hand slots. Built from the arrays directly instead of <c>Inventory.AllSlots</c>,
/// which concatenates new arrays on every call.
/// </summary>
internal static class PlayerSlots
{
    private static readonly List<SlotController> Buffer = new List<SlotController>(64);

    /// <summary>
    /// The slots, in a list that is reused by the next call: read it right away and don't keep it.
    /// Empty when there is no inventory.
    /// </summary>
    public static List<SlotController> Current(bool includeBag = true, bool includeEquipment = true)
    {
        Buffer.Clear();
        var inventory = Inventory.instance;
        if (inventory == null || inventory.Slots == null)
            return Buffer;

        Append(inventory.Slots);
        if (includeBag)
            Append(BagSlots(inventory));
        if (includeEquipment)
            Append(inventory.CharacterSlots);
        return Buffer;
    }

    /// <summary>The slots of the equipped bag, or null.</summary>
    public static SlotController[] BagSlots(Inventory inventory)
    {
        var backpack = BackpackStorage.instance;
        if (backpack == null || inventory.CharacterSlots == null || inventory.CharacterSlots.Length <= 5)
            return null;
        var storage = backpack.GetStorage();
        return storage != null ? storage.Slots : null;
    }

    /// <summary>True when a slot holds a container filled with this liquid.</summary>
    public static bool HoldsLiquid(SlotController slot, int liquidId)
    {
        var liquid = slot != null && slot.itemStack != null ? slot.itemStack.GetMetaOfType<ItemLiquidData>() : null;
        return liquid != null && liquid.liquidItem != null && liquid.liquidItem.ID == liquidId;
    }

    private static void Append(SlotController[] slots)
    {
        if (slots == null)
            return;
        foreach (var slot in slots)
        {
            if (slot != null)
                Buffer.Add(slot);
        }
    }
}
