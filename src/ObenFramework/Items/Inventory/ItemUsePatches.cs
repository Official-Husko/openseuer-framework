// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>
/// Raises <see cref="InventoryEvents.ItemUsed"/> for items the player uses from the inventory. Using an item
/// runs either all its actions (<c>InvokeActions</c>, which calls <c>InvokeAction</c> for each) or one chosen
/// action; the depth keeps the first from reporting once per action. Uses in the world and while trading are
/// left out.
/// </summary>
[HarmonyPatch]
internal static class ItemUsePatches
{
    private static int _depth;

    internal struct State
    {
        public bool Raise;
        public bool Counted;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Item), nameof(Item.InvokeActions), new[] { typeof(ItemData), typeof(bool) })]
    private static void UsingAll(Item __instance, bool worlduse, out State __state)
    {
        __state = new State { Raise = Wanted(worlduse) && __instance.Actions != null && __instance.Actions.Length > 0, Counted = true };
        _depth++;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Item), nameof(Item.InvokeActions), new[] { typeof(ItemData), typeof(bool) })]
    private static void UsedAll(Item __instance, State __state)
    {
        if (__state.Raise)
            Raise(__instance);
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Item), nameof(Item.InvokeActions), new[] { typeof(ItemData), typeof(bool) })]
    private static void UseAllDone(State __state)
    {
        if (__state.Counted && _depth > 0)
            _depth--;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Item), nameof(Item.InvokeAction))]
    private static void UsingOne(bool worlduse, out bool __state) => __state = _depth == 0 && Wanted(worlduse);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Item), nameof(Item.InvokeAction))]
    private static void UsedOne(Item __instance, bool __state)
    {
        if (__state)
            Raise(__instance);
    }

    private static bool Wanted(bool worlduse) =>
        !worlduse && InventoryEvents.ItemUsed.HasSubscribers && WorldChanges.Active
        && !(TradePanel.instance != null && TradePanel.instance.activeTrade != null);

    private static void Raise(Item item)
    {
        try { InventoryEvents.ItemUsed.Raise(item.ID); }
        catch (Exception e) { ItemsLog.Failed("Item used hook", e); }
    }
}
