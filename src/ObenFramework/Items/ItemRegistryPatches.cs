using System;
using HarmonyLib;

namespace ObenFramework.Items;

/// <summary>Hooks that keep mod items and recipes in the game's databases.</summary>
[HarmonyPatch]
internal static class ItemRegistryPatches
{
    // Awake only reads Items.json the first time; the postfix runs either way and adds nothing twice.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDatabase), "Awake")]
    private static void AfterItemsAwake() => Safely(ItemRegistry.ApplyAll, "ItemDatabase.Awake");

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDatabase), nameof(ItemDatabase.Reload))]
    private static void AfterItemsReload() => Safely(ItemRegistry.ApplyAll, "ItemDatabase.Reload");

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RecipeDatabase), "LoadDatabase")]
    private static void AfterRecipesLoad() => Safely(RecipeRegistry.Sync, "RecipeDatabase.LoadDatabase");

    // A pickup prefab is bound to one item by a reference baked into it, so a mod item that reuses another
    // item's prefab would turn back into that item when picked up. Point the pickup at the right item.
    // Missing item stand-ins drop as the game's ERROR prefab (bound to item 0) and need the same.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemOperations), nameof(ItemOperations.SetCollectibleItemValues))]
    private static void BeforeSetPickupValues(ItemStack item, UnityEngine.GameObject gameObject)
    {
        if (item == null || gameObject == null || !(ItemRegistry.IsModded(item.itemId) || ItemPlaceholders.IsPlaceholder(item.itemId)))
            return;

        try
        {
            var pickup = gameObject.GetComponent<CollectibleItem>();
            if (pickup != null && (pickup.Item == null || pickup.Item.ID != item.itemId))
            {
                if (item.itemReference == null)
                    item.SetItemReference();
                pickup.ChangeItemReference(item.itemReference);
            }
        }
        catch (Exception e)
        {
            ItemsLog.Error($"Couldn't point the pickup at item {item.itemId}", e);
        }
    }

    // The game calls these without a try/catch; a failure here must not stop its own load.
    private static void Safely(Action action, string where)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            ItemsLog.Error($"Adding mod items after {where} failed", e);
        }
    }
}
