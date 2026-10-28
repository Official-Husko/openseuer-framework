// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Crafting;

/// <summary>
/// Every station takes ingredients through <c>CraftingBase.RemoveItems</c> and makes its output through
/// <c>CraftingBase.CreateItems</c> (the still's override calls the base one), so these two hooks cover all
/// of them. RemoveItems also runs with <c>dontRemoveItems</c> set when a window only previews quality or
/// time; those calls are not crafts.
/// </summary>
[HarmonyPatch]
internal static class CraftingPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CraftingBase), "RemoveItems",
        new[] { typeof(Recipe), typeof(List<KeyValuePair<ItemStack, int>>), typeof(CraftingBase.CraftingInputData), typeof(int), typeof(bool), typeof(ItemStack[]) },
        new[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal })]
    private static void Started(CraftingBase __instance, Recipe recipe, int multiples, bool dontRemoveItems)
    {
        if (dontRemoveItems || recipe == null || !CraftingEvents.CraftStarted.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            CraftingEvents.CraftStarted.Raise(new CraftStart(recipe.ID, CraftingStations.Describe(__instance), multiples));
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Craft started hook", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CraftingBase), "CreateItems")]
    private static void Created(CraftingBase __instance, Recipe recipe, int multiples, bool __result)
    {
        if (recipe == null || !CraftingEvents.Crafted.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            var results = ItemCount.From(__result ? recipe.output : recipe.outputFail, multiples);
            CraftingEvents.Crafted.Raise(new CraftDone(recipe.ID, CraftingStations.Describe(__instance), __result, multiples, results));
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Crafted hook", e);
        }
    }
}
