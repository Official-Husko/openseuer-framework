using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// The workbench's auto-eat (PlayerAutoConsume.ConsumeNeeds, run by ShopBaseUI.Craft and every crafting second)
/// eats up to five items per need in one go. When one empties a stack, Slot.UpdateSlot only queues the slot's
/// ItemData for destruction at the end of the frame, and that object keeps its own copy of the stack (amount 1), so
/// the next pass in the same frame picked it again: its food was applied a second time, then SlotController.TakeItem
/// threw a NullReferenceException on the emptied stack, which also broke the Craft press. The fix skips a candidate
/// whose slot no longer holds anything.
/// </summary>
internal sealed class AutoConsumeLastItem : ModFeature
{
    public const string Key = "AutoConsumeLastItem";

    public override string Id => Key;
    public override string Name => "Auto-eat doesn't eat the last item twice";
    public override string Description => "Eating automatically while crafting no longer eats the last item of a stack twice and then fails.";
}

[HarmonyPatchCategory(AutoConsumeLastItem.Key)]
[HarmonyPatch(typeof(PlayerAutoConsume), "GetCandidate")]
internal static class AutoConsumeLastItemPatch
{
    // Skipping the original gives no candidate (null), as the game does for an item that isn't in a slot.
    private static bool Prefix(ItemData itemData)
    {
        if (itemData == null)
            return true;
        var stack = itemData.slotController?.itemStack;
        return stack != null && stack.itemId != -1 && stack.itemAmount > 0;
    }
}
