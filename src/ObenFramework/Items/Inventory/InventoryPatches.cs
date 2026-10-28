using System;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>
/// Marks the player's inventory for a recount whenever a stack changes. The hooks sit on the few methods
/// that change an <c>ItemStack</c> (every add, take, drop and craft ends in one of them) and on the
/// slot methods that write a stack's fields directly. They only set a flag; which stacks belong to the
/// player is worked out by the recount.
/// </summary>
[HarmonyPatch]
internal static class InventoryPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.AddItemToPanel),
        new[] { typeof(Item), typeof(int), typeof(object[]), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(ItemOperations.ItemOperationSettings) },
        new[] { ArgumentType.Ref, ArgumentType.Out, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal })]
    private static void Added() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.RemoveItemAmount))]
    private static void AmountRemoved() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.RemoveStack))]
    private static void StackRemoved() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.DragItemFromPanel), new[] { typeof(ItemData) })]
    private static void DraggedData() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.DragItemFromPanel), new[] { typeof(ItemStack) })]
    private static void DraggedStack() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.DragItemFromPanel), new[] { typeof(int) })]
    private static void DraggedAmount() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.ChangeItem))]
    private static void StackItemChanged() => InventoryWatch.MarkDirty();

    // These three write itemId or itemAmount themselves instead of going through the stack's methods.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SlotController), nameof(SlotController.ChangeItem))]
    private static void SlotItemChanged() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SlotController), nameof(SlotController.RemoveLiquidAndReturnRemaining))]
    private static void LiquidRemoved() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SlotController), nameof(SlotController.MoveToOtherInventory),
        new[] { typeof(SlotController), typeof(int), typeof(int) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    private static void Moved() => InventoryWatch.MarkDirty();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemOperations), nameof(ItemOperations.ForceItemToSlot))]
    private static void Forced() => InventoryWatch.MarkDirty();

    // A load swaps whole stacks in; the restored inventory is the new starting point, not a change.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.OnLoadingGame))]
    private static void Loaded() => InventoryWatch.Reset();

    /// <summary>Dragging a stack onto a slot and the quick-move click are the player's own doing.</summary>
    [HarmonyPatch]
    private static class PlayerActions
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Slot), nameof(Slot.OnDrop), new[] { typeof(ItemData) })]
        private static void DropStarting() => WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Slot), nameof(Slot.OnDrop), new[] { typeof(ItemData) })]
        private static Exception DropFinished(Exception __exception) => Leave(__exception);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ItemData), nameof(ItemData.QuickMove))]
        private static void MoveStarting() => WorldChanges.Adjust(ChangeSource.LocalPlayer, 1);

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(ItemData), nameof(ItemData.QuickMove))]
        private static Exception MoveFinished(Exception __exception) => Leave(__exception);

        private static Exception Leave(Exception exception)
        {
            WorldChanges.Adjust(ChangeSource.LocalPlayer, -1);
            return exception;
        }
    }
}
