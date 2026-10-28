// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.Items;

/// <summary>
/// Two kinds of hooks. The scope hooks wrap the game calls that check or take blueprint and renovation
/// resources and open an <see cref="InventorySourceScope"/> for their duration. The inventory hooks add the
/// scope's containers to the inventory's counts and take what the inventory lacks from them. Outside a scope
/// the inventory hooks return at once.
/// </summary>
[HarmonyPatch]
internal static class InventorySourcesPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CheckForItem), new[] { typeof(int), typeof(float), typeof(bool), typeof(bool), typeof(int), typeof(bool), typeof(bool) })]
    private static void Checked(int id, float amount, bool checkAmmo, bool ignoreOwner, int owner, bool liquid, bool ignoreBackpack, ref bool __result)
    {
        var scope = InventorySources.Current;
        if (__result || scope == null)
            return;
        try
        {
            float needed = amount > 0f ? amount : 1f;
            float total = InventoryCount(id, checkAmmo, ignoreOwner, owner, liquid, ignoreBackpack) + scope.Count(id, checkAmmo, ignoreOwner, owner, liquid);
            __result = total >= needed;
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Counting extra inventory sources", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetItemAmountInInventory), new[] { typeof(int), typeof(bool) })]
    private static void Counted(int id, ref int __result)
    {
        var scope = InventorySources.Current;
        if (scope == null)
            return;
        try { __result += (int)scope.Count(id, false, true, -1, false); }
        catch (Exception e) { ItemsLog.Failed("Counting extra inventory sources", e); }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.FindAndGetTotalAmmo))]
    private static void AmmoCounted(int id, ref int __result)
    {
        var scope = InventorySources.Current;
        if (scope == null)
            return;
        try { __result += (int)scope.Ammo(id); }
        catch (Exception e) { ItemsLog.Failed("Counting ammo in extra inventory sources", e); }
    }

    // The inventory keeps going first: only once it holds less than the change needs do the containers pay.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.FindAndChangeItemAmmo))]
    private static bool ChangingAmmo(int id, int ammoAmount, bool ignoreBackpack, bool ignoreOwner, bool ignoreOwned, ref bool __result)
    {
        if (!InventorySources.Consuming)
            return true;
        try
        {
            if (InventoryAmmo(id, ignoreBackpack) >= Math.Max(1, -ammoAmount) || !InventorySources.Current.TakeAmmo(id, ammoAmount, ignoreOwner, ignoreOwned))
                return true;
            __result = true;
            return false;
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Taking ammo from extra inventory sources", e);
            return true;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItems), new[] { typeof(int), typeof(float), typeof(bool), typeof(int), typeof(bool), typeof(bool) })]
    private static bool Removing(int id, ref float amount, bool ignoreOwner, int owner, bool ignoreBackpack)
    {
        if (!InventorySources.Consuming || amount <= 0f)
            return true;
        try
        {
            // RemoveItems removes one unit per loop step while i < amount, so it takes the amount rounded up.
            int units = (int)Math.Ceiling(amount);
            int missing = units - (int)InventoryCount(id, false, ignoreOwner, owner, false, ignoreBackpack);
            if (missing > 0)
                amount -= InventorySources.Current.Take(id, missing, ignoreOwner, owner);
            return amount > 0f;
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Taking from extra inventory sources", e);
            return true;
        }
    }

    // Runs before other mods' prefixes: the amount it leaves is what the tenement storages and the inventory still pay.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.RemoveRequiredItems))]
    private static void RemovingRenovationItems(TenementController __instance, int id, ref int amount)
    {
        if (!InventorySources.Consuming || amount < 1 || __instance.ignoreUpgradePricesAndItems)
            return;
        try
        {
            float have = InventoryCount(id, false, true, -1, false, false);
            var storages = InventorySources.TenementStorages();
            if (storages != null)
            {
                foreach (var storage in storages)
                    have += storage != null && storage.Slots != null ? storage.GetItemAmount(id) : 0f;
            }
            int missing = amount - (int)have;
            if (missing > 0)
                amount -= InventorySources.Current.Take(id, missing, true, -1);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Taking renovation items from extra inventory sources", e);
        }
    }

    /// <summary>The player's own inventory, counted the way the game's check does, without the extra containers.</summary>
    private static float InventoryCount(int id, bool checkAmmo, bool ignoreOwner, int owner, bool liquid, bool ignoreBackpack)
    {
        float total = 0f;
        foreach (var slot in PlayerSlots.Current(includeBag: !ignoreBackpack))
            total += ItemOperations.GetItemAmountFromSlot(slot, id, checkAmmo, ignoreOwner, owner, liquid);
        return total;
    }

    private static float InventoryAmmo(int id, bool ignoreBackpack)
    {
        float total = 0f;
        foreach (var slot in PlayerSlots.Current(includeBag: !ignoreBackpack))
        {
            var stack = slot.itemStack;
            if (stack != null && stack.itemId == id && stack.itemAmount > 0)
                total += stack.GetAmmodata() is OS.Items.ItemAmmodata ammo ? ammo.ammo : 0f;
        }
        return total;
    }
}
