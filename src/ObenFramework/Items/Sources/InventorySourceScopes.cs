// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;

namespace ObenFramework.Items;

/// <summary>
/// Opens an <see cref="InventorySourceScope"/> around the game calls that check or take blueprint and
/// renovation resources. A call inside another one reuses the outer scope. The finalizers close the scope
/// even when the game's method throws.
/// </summary>
[HarmonyPatch]
internal static class InventorySourceScopes
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TaskItemBlueprint), nameof(TaskItemBlueprint.PlayerHasResources))]
    private static void BlueprintCheck(out InventorySourceScope __state) =>
        __state = Open(InventorySources.FurnitureBlueprint, false);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TaskItemBlueprint), nameof(TaskItemBlueprint.PlayerHasResources))]
    private static void BlueprintCheckDone(InventorySourceScope __state) => Close(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ShopBaseUI), "Craft")]
    private static void BlueprintTake(out InventorySourceScope __state) =>
        __state = Open(InventorySources.FurnitureBlueprint, true);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(ShopBaseUI), "Craft")]
    private static void BlueprintTakeDone(InventorySourceScope __state) => Close(__state);

    // The resource list of the workshop window; only its crafting side shows what the player has.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FurnitureResource), nameof(FurnitureResource.AddResourceInfo))]
    private static void ResourceShown(bool crafting, out InventorySourceScope __state) =>
        __state = crafting ? Open(InventorySources.FurnitureBlueprint, false) : null;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(FurnitureResource), nameof(FurnitureResource.AddResourceInfo))]
    private static void ResourceShownDone(InventorySourceScope __state) => Close(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.CheckForItem))]
    private static void RenovationCheck(out InventorySourceScope __state) =>
        __state = Open(InventorySources.Renovation, false);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.CheckForItem))]
    private static void RenovationCheckDone(InventorySourceScope __state) => Close(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.GetItemAmount))]
    private static void RenovationCount(out InventorySourceScope __state) =>
        __state = Open(InventorySources.Renovation, false);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TenementController), nameof(TenementController.GetItemAmount))]
    private static void RenovationCountDone(InventorySourceScope __state) => Close(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TenementUpgrade), nameof(TenementUpgrade.TakeRequiredItemsAndMoney))]
    private static void RenovationTake(out InventorySourceScope __state) =>
        __state = Open(InventorySources.Renovation, true);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(TenementUpgrade), nameof(TenementUpgrade.TakeRequiredItemsAndMoney))]
    private static void RenovationTakeDone(InventorySourceScope __state) => Close(__state);

    private static InventorySourceScope Open(string kind, bool consume)
    {
        if (!InventorySources.Any)
            return null;
        try
        {
            return InventorySources.Open(kind, consume);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Opening extra inventory sources for " + kind, e);
            return null;
        }
    }

    private static void Close(InventorySourceScope scope)
    {
        if (scope == null)
            return;
        try { InventorySources.Close(scope); }
        catch (Exception e) { ItemsLog.Failed("Closing extra inventory sources", e); }
    }
}
