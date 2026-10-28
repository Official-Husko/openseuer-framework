// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;

namespace ObenFramework.Crafting;

/// <summary>
/// Opens a <see cref="CraftingSourceScope"/> around the station methods that read or take the input slots, and
/// re-checks the extra sources before a craft starts: the game only checks ingredients when the window is
/// refreshed, and a container next to the station can be emptied in between. Every hook returns at once while
/// no provider is registered.
/// </summary>
[HarmonyPatch]
internal static class CraftingSourcesPatches
{
    // Calls with their own item list (the grow box's quick planting) don't read the input slots.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CraftingBase), "CompareRecipeToItems")]
    private static void Comparing(CraftingBase __instance, ItemStack[] items, out CraftingSourceScope __state) =>
        __state = items == null ? Begin(__instance, false) : null;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CraftingBase), "CompareRecipeToItems")]
    private static void Compared(CraftingSourceScope __state) => End(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CraftingBase), "CountItems")]
    private static void Counting(CraftingBase __instance, out CraftingSourceScope __state) => __state = Begin(__instance, false);

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CraftingBase), "CountItems")]
    private static void Counted(CraftingSourceScope __state) => End(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CraftingBase), "RemoveItems",
        new[] { typeof(Recipe), typeof(List<KeyValuePair<ItemStack, int>>), typeof(CraftingBase.CraftingInputData), typeof(int), typeof(bool), typeof(ItemStack[]) },
        new[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal })]
    private static void Removing(CraftingBase __instance, bool dontRemoveItems, ItemStack[] items, out CraftingSourceScope __state) =>
        __state = items == null ? Begin(__instance, !dontRemoveItems) : null;

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(CraftingBase), "RemoveItems",
        new[] { typeof(Recipe), typeof(List<KeyValuePair<ItemStack, int>>), typeof(CraftingBase.CraftingInputData), typeof(int), typeof(bool), typeof(ItemStack[]) },
        new[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal })]
    private static void Removed(CraftingSourceScope __state) => End(__state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(manufacturing), nameof(manufacturing.Craft))]
    private static bool Crafting(manufacturing __instance, Recipe ___selectedRecipe) => Gate(__instance, ___selectedRecipe, 1);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(manufacturingProcess), nameof(manufacturingProcess.StartProcess))]
    private static bool Processing(manufacturingProcess __instance)
    {
        if (__instance.stage != manufacturingProcess.Stage.LoadingItems || !__instance.PoweredAndActive || __instance.ActiveRecipe.Key == null)
            return true;
        return Gate(__instance, __instance.ActiveRecipe.Key, __instance.ActiveRecipe.Value);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Growing), nameof(Growing.StartGrow), new[] { typeof(int) })]
    private static bool Planting(Growing __instance, int amount, Recipe ___selectedRecipe) => Gate(__instance, ___selectedRecipe, amount);

    private static CraftingSourceScope Begin(CraftingBase station, bool consume)
    {
        if (!CraftingSources.Any)
            return null;
        try
        {
            return CraftingSources.Begin(station, consume);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Opening extra crafting sources", e);
            return null;
        }
    }

    private static void End(CraftingSourceScope scope)
    {
        if (scope == null)
            return;
        try { CraftingSources.End(scope); }
        catch (Exception e) { ItemsLog.Failed("Closing extra crafting sources", e); }
    }

    // Only stations with extra sources are checked again; without them the game's own check stands.
    private static bool Gate(CraftingBase station, Recipe recipe, int multiples)
    {
        if (!CraftingSources.Any || recipe == null)
            return true;
        try
        {
            if (!CraftingSources.HasSources(station) || CraftingStations.HasIngredients(station, recipe, multiples))
                return true;
            if (CraftingBase.currentCraftingBase == station)
                station.CheckItems();
            return false;
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Checking extra crafting sources before a craft", e);
            return true;
        }
    }
}
