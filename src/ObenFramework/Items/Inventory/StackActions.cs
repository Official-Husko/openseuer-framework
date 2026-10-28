// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Items;

/// <summary>What a stack made by <see cref="StackActions"/> looks like beyond its item and amount.</summary>
public sealed class StackSpec
{
    /// <summary>Quality from just above 0 to 2 (1 is normal), or null for the item's default.</summary>
    public float? Quality { get; set; }

    /// <summary>The character the stack belongs to (taking it is theft), or -1 for nobody.</summary>
    public int OwnerId { get; set; } = -1;

    /// <summary>For a liquid container: the liquid to fill it with, or 0 for none.</summary>
    public int LiquidItemId { get; set; }

    /// <summary>How much liquid the whole stack holds, when <see cref="LiquidItemId"/> is set.</summary>
    public float LiquidAmount { get; set; }

    /// <summary>A plain stack: default quality, nobody's, empty.</summary>
    public static StackSpec Default => new StackSpec();
}

/// <summary>
/// Gives, drops and takes stacks with details <see cref="PlayerItems"/> doesn't cover: quality, an owner,
/// liquid contents, and liquid units. Everything refuses money ids and returns false while a save loads.
/// </summary>
public static class StackActions
{
    private const float MaxQuality = 2f;

    /// <summary>Gives the stack only if all of it fits in the inventory grid.</summary>
    public static bool Give(int itemId, int amount, StackSpec spec = null) => Place(itemId, amount, spec ?? StackSpec.Default, false);

    /// <summary>Gives the stack; what doesn't fit is dropped at the player's feet.</summary>
    public static bool GiveOrDrop(int itemId, int amount, StackSpec spec = null) => Place(itemId, amount, spec ?? StackSpec.Default, true);

    /// <summary>Drops the stack at the player's feet, as if they threw it out of the inventory.</summary>
    public static bool Drop(int itemId, int amount, StackSpec spec = null)
    {
        if (PlayerLocator.instance == null || !Prepare(itemId, amount, spec ?? StackSpec.Default, out var item, out var meta))
            return false;
        ItemOperations.DropItem(item, amount, (spec ?? StackSpec.Default).OwnerId, meta);
        return true;
    }

    /// <summary>
    /// Pours a liquid into the empty or matching containers the player carries. Returns how much went in,
    /// which is less than asked when the containers are full.
    /// </summary>
    public static float AddLiquid(int liquidItemId, float amount)
    {
        if (!(amount > 0f) || float.IsInfinity(amount) || !PlayerItems.IsReady || SaveGate.IsBusy)
            return 0f;
        var liquid = ItemLookup.ById(liquidItemId);
        if (liquid == null || !TakeMath.IsLiquid(liquid.Categories))
            return 0f;

        using (WorldChanges.EnterMod())
        {
            ItemOperations.AddLiquid(liquid, amount, out float remaining);
            InventoryWatch.Check();
            return amount - remaining;
        }
    }

    /// <summary>How much of a liquid the player carries in containers.</summary>
    public static float LiquidAmount(int liquidItemId) =>
        PlayerItems.IsReady ? (float)Inventory.instance.CountItemsDialogue(liquidItemId, false, true, -1.0, liquid: true) : 0f;

    /// <summary>
    /// Takes an amount of a liquid out of the player's containers, all or nothing. Emptied containers stay
    /// in the inventory as empty ones. Owned containers are used without a theft check.
    /// </summary>
    public static bool TakeLiquid(int liquidItemId, float amount)
    {
        if (!(amount > 0f) || float.IsInfinity(amount) || !PlayerItems.IsReady || SaveGate.IsBusy)
            return false;
        if (LiquidAmount(liquidItemId) + 0.0001f < amount)
            return false;

        using (WorldChanges.EnterMod())
        {
            bool done = Inventory.instance.RemoveLiquid(liquidItemId, amount, ignoreOwner: true, owner: -1, ignoreCrime: true, removeLiquidContainer: false);
            Inventory.instance.UpdateAllSlots();
            InventoryWatch.Check();
            return done;
        }
    }

    /// <summary>The stacks in the inventory the player stole.</summary>
    public static List<ItemStackInfo> StolenStacks()
    {
        var list = new List<ItemStackInfo>();
        if (!PlayerItems.IsReady)
            return list;
        foreach (var stack in Inventory.instance.GetStolenItems())
            list.Add(ItemStackInfo.From(stack));
        return list;
    }

    private static bool Place(int itemId, int amount, StackSpec spec, bool dropRest)
    {
        var inventory = Inventory.instance;
        if (inventory == null || !Prepare(itemId, amount, spec, out var item, out var meta))
            return false;
        if (!dropRest && !inventory.InventoryIsNotFull(item, spec.OwnerId, amount, 1, meta, inventory.Slots))
            return false;

        using (WorldChanges.EnterMod())
        {
            ItemOperations.AddItemsAndDropRemaining(item, spec.OwnerId, null, meta, amount);
            InventoryWatch.Check();
        }
        return true;
    }

    private static bool Prepare(int itemId, int amount, StackSpec spec, out Item item, out object[] meta)
    {
        item = null;
        meta = null;
        if (amount < 1 || PlayerItems.IsMoney(itemId) || SaveGate.IsBusy)
            return false;
        item = ItemLookup.ById(itemId);
        if (item == null)
            return false;
        meta = MetaFor(item, amount, spec);
        return meta != null;
    }

    // Null when the spec can't apply to the item (quality on an item without quality, liquid in a non-container).
    private static object[] MetaFor(Item item, int amount, StackSpec spec)
    {
        var meta = item.CloneMeta(false, amount, item);
        if (spec.Quality.HasValue)
        {
            var quality = Item.GetMetaOfType<ItemQualityData>(meta);
            float value = spec.Quality.Value;
            if (quality == null || float.IsNaN(value) || value <= 0f || value > MaxQuality)
                return null;
            quality.quality = value;
        }
        if (spec.LiquidItemId == 0)
            return meta;

        var liquid = ItemLookup.ById(spec.LiquidItemId);
        if (liquid == null || !TakeMath.IsLiquid(liquid.Categories) || !(spec.LiquidAmount > 0f) || Item.GetMetaOfType<ItemLiquidData>(meta) == null)
            return null;
        return item.CloneMetaAndSetLiquid(liquid, meta, spec.LiquidAmount, out _, amount);
    }
}
