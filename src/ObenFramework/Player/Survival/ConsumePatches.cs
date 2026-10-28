// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;
using OS.Items;

namespace ObenFramework.Player.Survival;

/// <summary>
/// Asks <see cref="ConsumeHooks"/> before the player consumes something. The real effect happens in
/// <c>ItemConsumable.Invoke</c>, but by then the game has already started the use animation and
/// cooldown (inventory) or taken a sip off the liquid (world items). So the question is asked early, in
/// the two entry points, and <c>Invoke</c> only asks itself when it is reached some other way.
/// </summary>
[HarmonyPatch]
internal static class ConsumePatches
{
    // True while an entry point has already asked, so the Invoke it leads to doesn't ask again.
    private static bool _asked;

    private static readonly Func<CollectibleItem, ItemData> ItemDataOf = BindItemData();

    private static Func<CollectibleItem, ItemData> BindItemData()
    {
        try
        {
            return AccessTools.MethodDelegate<Func<CollectibleItem, ItemData>>(
                AccessTools.Method(typeof(CollectibleItem), "GetOrCreateItemData", Type.EmptyTypes));
        }
        catch (Exception e)
        {
            GameEvents.Log?.Error("Couldn't bind CollectibleItem.GetOrCreateItemData; consumption is only checked at ItemConsumable.Invoke", e);
            return null;
        }
    }

    // Inventory use (the use button on an item or a drink in a bottle).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemInfo), nameof(ItemInfo.UseItem), typeof(int), typeof(bool))]
    private static bool BeforeUseItem(ItemInfo __instance, int index, bool secondaryButton, out bool __state)
    {
        __state = _asked;
        if (!ConsumeHooks.HasHandlers)
            return true;
        try
        {
            var data = __instance.ItemData;
            var panel = __instance.itemInfoPanel;
            bool buttonOff = data == null || (!panel.UseButtonEnabled && !secondaryButton) || (!panel.SecondaryUseButtonEnabled && secondaryButton);
            if (buttonOff || TradePanel.instance.activeTrade != null || __instance.cooldown)
                return true;

            var action = __instance.childItem != null
                ? FirstConsumable(__instance.childItem.item.Actions)
                : IndexedConsumable(data.item.Actions, index);
            return action == null || Ask(data);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Checking an inventory use failed", e);
            return true;
        }
    }

    // Using an item that lies in the world (food on a table, a bottle on a shelf).
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CollectibleItem), nameof(CollectibleItem.InvokeActions))]
    private static bool BeforeWorldUse(CollectibleItem __instance, out bool __state)
    {
        __state = _asked;
        if (!ConsumeHooks.HasHandlers || ItemDataOf == null)
            return true;
        try
        {
            if (TradePanel.instance.activeTrade != null)
                return true;

            var data = ItemDataOf(__instance);
            var liquid = ItemMetaUtilities.GetMetaOfType<ItemLiquidData>(__instance.meta);
            bool drink = liquid != null && liquid.liquidItem != null && !string.IsNullOrEmpty(liquid.liquidItem.Item.FirstAction.Name);
            var action = (drink ? liquid.liquidItem.Item.FirstAction : __instance.Item.Item.FirstAction) as ItemConsumable;
            if (action == null || !action.CanUse(data, out _) || !OwnerAllows(data))
                return true;
            return Ask(data);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Checking a world use failed", e);
            return true;
        }
    }

    // The effect itself. Reached without a check from the entry points above when the game consumes on
    // the player's behalf (depression drinking, scripted use), and always for the Consumed event.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemConsumable), nameof(ItemConsumable.Invoke), typeof(ItemData), typeof(bool))]
    private static bool BeforeInvoke(ItemData itemData, out bool __state)
    {
        __state = true;
        if (_asked || !ConsumeHooks.HasHandlers)
            return true;
        try
        {
            __state = ConsumeHooks.Allow(itemData);
            return __state;
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Checking a consumption failed", e);
            return true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemConsumable), nameof(ItemConsumable.Invoke), typeof(ItemData), typeof(bool))]
    private static void AfterInvoke(ItemData itemData, bool __state)
    {
        if (!__state || itemData == null || itemData.item == null)
            return;
        SurvivalEvents.Consumed.Raise(new ConsumedItem(itemData.item.ID, itemData.item.Title), GameEvents.Log);
    }

    // A finalizer so the flag is restored even if the game's code throws.
    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ItemInfo), nameof(ItemInfo.UseItem), typeof(int), typeof(bool))]
    private static void RestoreAfterUseItem(bool __state) => _asked = __state;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CollectibleItem), nameof(CollectibleItem.InvokeActions))]
    private static void RestoreAfterWorldUse(bool __state) => _asked = __state;

    private static bool Ask(ItemData data)
    {
        _asked = true;
        return ConsumeHooks.Allow(data);
    }

    private static ItemConsumable FirstConsumable(Item.ItemAction[] actions)
    {
        if (actions == null)
            return null;
        foreach (var action in actions)
        {
            if (action is ItemConsumable consumable)
                return consumable;
        }
        return null;
    }

    private static ItemConsumable IndexedConsumable(Item.ItemAction[] actions, int index) =>
        actions != null && index >= 0 && index < actions.Length ? actions[index] as ItemConsumable : null;

    private static bool OwnerAllows(ItemData data)
    {
        if (data.meta == null)
            return true;
        foreach (var entry in data.meta)
        {
            if (entry != null && entry.GetType() == typeof(ItemOwnerPermission))
                return ((ItemOwnerPermission)entry).allowUse;
        }
        return true;
    }
}
