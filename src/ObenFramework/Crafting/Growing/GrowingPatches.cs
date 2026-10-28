// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Crafting;

/// <summary>
/// Reports harvests (every harvest goes through the full <c>grow.HarvestExternal</c> overload) and fruit picks
/// (<c>GrowItem.GetItem</c> returns 0 when it gave the fruit).
/// </summary>
[HarmonyPatch]
internal static class GrowingPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(grow), nameof(grow.HarvestExternal),
        new[] { typeof(bool), typeof(bool), typeof(Func<List<ItemStack>, bool>) },
        new[] { ArgumentType.Out, ArgumentType.Out, ArgumentType.Normal })]
    private static void Harvested(grow __instance, List<ItemStack> __result)
    {
        if (__result == null || __result.Count == 0 || !Plants.Harvested.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            var items = new List<ItemCount>();
            foreach (var stack in __result)
            {
                if (stack != null)
                    items.Add(new ItemCount(stack.itemReference != null ? stack.itemReference.ID : stack.itemId, stack.itemAmount));
            }
            if (items.Count > 0)
                Plants.Harvested.Raise(new PlantHarvest(Plants.IdOf(__instance), items));
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Plant harvest hook", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GrowItem), nameof(GrowItem.GetItem))]
    private static void Picked(GrowItem __instance, int __result)
    {
        if (__result != 0 || !Plants.Picked.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            var plant = Plants.PlantOf(__instance);
            int itemId = Plants.ItemOf(__instance);
            if (plant != null && itemId >= 0 && !__instance.Spoiled())
                Plants.Picked.Raise(new PlantPick(Plants.IdOf(plant), itemId, Plants.AmountOf(__instance)));
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Plant pick hook", e);
        }
    }
}
