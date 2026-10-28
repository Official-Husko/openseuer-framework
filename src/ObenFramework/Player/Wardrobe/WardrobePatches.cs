// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Events;

namespace ObenFramework.Player;

/// <summary>
/// Raises <see cref="Wardrobe.Changed"/>. The worn items can change through equipping, unequipping, and
/// dragging items between slots, so each of those calls notes the four worn ids when the outermost one
/// starts and compares when it ends. The calls nest (equipping moves items), and only the outermost one
/// reports, so a swap is one event per slot. Four array reads per call, no allocations.
/// </summary>
[HarmonyPatch]
internal static class WardrobePatches
{
    private static readonly int[] Before = new int[4];
    private static int _depth;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Equip))]
    private static void BeforeEquip() => Enter();

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Equip))]
    private static void AfterEquip() => Exit();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Unequip))]
    private static void BeforeUnequip() => Enter();

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.Unequip))]
    private static void AfterUnequip() => Exit();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SlotController), nameof(SlotController.TakeItem), new[] { typeof(Item), typeof(int), typeof(int), typeof(object[]) })]
    private static void BeforeTake() => Enter();

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SlotController), nameof(SlotController.TakeItem), new[] { typeof(Item), typeof(int), typeof(int), typeof(object[]) })]
    private static void AfterTake() => Exit();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SlotController), nameof(SlotController.DropItem))]
    private static void BeforeDrop() => Enter();

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(SlotController), nameof(SlotController.DropItem))]
    private static void AfterDrop() => Exit();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemData), nameof(ItemData.Return))]
    private static void BeforeReturn() => Enter();

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ItemData), nameof(ItemData.Return))]
    private static void AfterReturn() => Exit();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemData), nameof(ItemData.ReturnOriginal))]
    private static void BeforeReturnOriginal() => Enter();

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ItemData), nameof(ItemData.ReturnOriginal))]
    private static void AfterReturnOriginal() => Exit();

    private static void Enter()
    {
        if (_depth++ != 0)
            return;
        try
        {
            for (int i = 0; i < Before.Length; i++)
                Before[i] = Wardrobe.Worn(i);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reading the worn items failed", e);
        }
    }

    private static void Exit()
    {
        if (_depth == 0 || --_depth != 0)
            return;
        try
        {
            for (int i = 0; i < Before.Length; i++)
            {
                int now = Wardrobe.Worn(i);
                if (now != Before[i])
                    Wardrobe.RaiseChanged(i, Before[i], now);
            }
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Reporting a wardrobe change failed", e);
        }
    }
}
