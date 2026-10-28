// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Items;

/// <summary>
/// The extra containers for one game call (a blueprint check, a renovation, taking their resources), and
/// counting and taking from them. Lives only for the duration of that call.
/// </summary>
internal sealed class InventorySourceScope
{
    public InventorySourceScope(string kind, bool consume, List<Storage> storages)
    {
        Kind = kind;
        Consume = consume;
        Storages = storages;
    }

    public string Kind { get; }

    /// <summary>The call takes resources, not only checks them.</summary>
    public bool Consume { get; }

    public List<Storage> Storages { get; }

    public bool Closed { get; set; }

    private bool _changed;

    public float Count(int itemId, bool checkAmmo, bool ignoreOwner, int owner, bool liquid)
    {
        float total = 0f;
        foreach (var storage in Storages)
        {
            var slots = storage.Slots;
            if (slots == null)
                continue;
            foreach (var slot in slots)
            {
                if (slot != null)
                    total += ItemOperations.GetItemAmountFromSlot(slot, itemId, checkAmmo, ignoreOwner, owner, liquid);
            }
        }
        return total;
    }

    public float Ammo(int itemId)
    {
        float total = 0f;
        foreach (var storage in Storages)
        {
            foreach (var slot in storage.Slots ?? Array.Empty<SlotController>())
            {
                var ammo = Holding(slot, itemId) ? slot.itemStack.GetAmmodata() : null;
                if (ammo != null)
                    total += ammo.ammo;
            }
        }
        return total;
    }

    /// <summary>Changes the ammo of the first matching item in the containers, the way the inventory's own call does.</summary>
    public bool TakeAmmo(int itemId, int ammoAmount, bool ignoreOwner, bool ignoreOwned)
    {
        foreach (var storage in Storages)
        {
            foreach (var slot in storage.Slots ?? Array.Empty<SlotController>())
            {
                if (!Holding(slot, itemId))
                    continue;
                var stack = slot.itemStack;
                if (stack.ownerId > 0 && (!ignoreOwner || (!ignoreOwned && stack.GetMetaOfType<ItemOwnerPermission>() != null)))
                    continue;
                var ammo = stack.GetAmmodata();
                if (ammo == null)
                    continue;
                if (ammoAmount < -1 && ammo.ammo < -ammoAmount)
                    return false;
                using (WorldChanges.EnterMod())
                    ammo.ChangeAmmoDirect(slot, ammoAmount);
                _changed = true;
                return true;
            }
        }
        return false;
    }

    /// <summary>Takes up to <paramref name="units"/> of an item (or, for a liquid, filled containers of it). Returns how many.</summary>
    public int Take(int itemId, int units, bool ignoreOwner, int owner)
    {
        if (units <= 0)
            return 0;

        int wanted = units;
        using (WorldChanges.EnterMod())
        {
            foreach (var storage in Storages)
            {
                if (wanted <= 0)
                    break;
                storage.RemoveItem(itemId, out int left, wanted, false, ignoreOwner, owner);
                if (left < wanted)
                    _changed = true;
                wanted = Math.Max(0, left);
            }
            if (wanted > 0 && IsLiquid(itemId))
                wanted -= TakeLiquidContainers(itemId, wanted, ignoreOwner, owner);
        }
        return units - wanted;
    }

    /// <summary>Lets the containers' windows show what was taken.</summary>
    public void Refresh()
    {
        if (!_changed)
            return;
        foreach (var storage in Storages)
        {
            try
            {
                if (storage != null && storage.Slots != null)
                    storage.UpdateSlots();
            }
            catch (Exception e)
            {
                ItemsLog.Failed("Refreshing an extra inventory source", e);
            }
        }
    }

    // The game counts a bottle of the liquid as one unit of it; take whole bottles the same way.
    private int TakeLiquidContainers(int itemId, int units, bool ignoreOwner, int owner)
    {
        int taken = 0;
        foreach (var storage in Storages)
        {
            foreach (var slot in storage.Slots ?? Array.Empty<SlotController>())
            {
                if (taken >= units)
                    return taken;
                if (slot == null || (!ignoreOwner && slot.itemStack.ownerId != owner) || !PlayerSlots.HoldsLiquid(slot, itemId))
                    continue;
                slot.TakeItem(1);
                taken++;
                _changed = true;
            }
        }
        return taken;
    }

    private static bool Holding(SlotController slot, int itemId) =>
        slot != null && slot.itemStack != null && slot.itemStack.itemId == itemId && slot.itemStack.itemAmount > 0;

    private static bool IsLiquid(int itemId)
    {
        var item = ItemLookup.ById(itemId);
        return item != null && TakeMath.IsLiquid(item.Categories);
    }
}
