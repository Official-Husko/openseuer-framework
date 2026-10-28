// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using OS.Items;

namespace ObenFramework.Crafting;

/// <summary>
/// Applies <see cref="CraftingSpeed"/> and <see cref="CraftingQuality"/>. The speed goes into the process
/// time factor that <c>RemoveItems</c> hands back, which every station reads its craft time from; the
/// quality goes onto the output's quality meta and the still's batch quality. Each returns at once while no
/// mod has set a factor.
/// </summary>
[HarmonyPatch]
internal static class CraftingFactorPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CraftingBase), "RemoveItems",
        new[] { typeof(Recipe), typeof(List<KeyValuePair<ItemStack, int>>), typeof(CraftingBase.CraftingInputData), typeof(int), typeof(bool), typeof(ItemStack[]) },
        new[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal })]
    private static void Timed(CraftingBase __instance, Recipe recipe, ref CraftingBase.CraftingInputData inputData)
    {
        if (inputData == null || recipe == null)
            return;
        try
        {
            float speed = CraftingSpeed.For(__instance);
            if (speed == 1f)
                return;
            int baseSeconds = recipe.GetProcessTime(1f, __instance.EquipmentQualityWithUpgrades);
            inputData.processTimeFactor = QualityMath.TimeFactor(inputData.processTimeFactor, speed, baseSeconds);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Crafting speed hook", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.ApplyQuality))]
    private static void QualityApplied(CraftingBase __instance, Recipe recipe, object[] __result)
    {
        if (__result == null || Pinned(recipe))
            return;
        try
        {
            var quality = Item.GetMetaOfType<ItemQualityData>(__result);
            if (quality != null && !quality.fixedQuality)
                quality.quality = CraftingQuality.Adjust(__instance, quality.quality);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Crafting quality hook", e);
        }
    }

    // A still works out its batch quality once here and later fills bottles with that fixed quality.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CraftingBase), nameof(CraftingBase.GetOutputQualityValue))]
    private static void OutputQuality(CraftingBase __instance, Recipe recipe, ref float __result)
    {
        if (Pinned(recipe))
            return;
        try { __result = CraftingQuality.Adjust(__instance, __result); }
        catch (Exception e) { ItemsLog.Failed("Crafting output quality hook", e); }
    }

    private static bool Pinned(Recipe recipe) =>
        recipe != null && recipe.metaSettings != null && recipe.metaSettings.useFixedQuality;
}
