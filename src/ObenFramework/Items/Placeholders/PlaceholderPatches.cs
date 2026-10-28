// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Patching;

namespace ObenFramework.Items;

/// <summary>
/// Hands out <see cref="ItemPlaceholders"/> where the game looks an item up by id. Every item reference
/// and stack goes through this lookup, so it is the one place a missing mod item can keep its id. The
/// check costs one comparison when the item exists.
/// </summary>
[PatchArea("ItemPlaceholders")]
[HarmonyPatch(typeof(ItemDatabase), nameof(ItemDatabase.FetchItemByID), new[] { typeof(int) })]
internal static class PlaceholderLookupPatch
{
    private static void Postfix(int id, ref Item __result)
    {
        if (__result != null || !ReservedIds.Contains(id))
            return;
        try
        {
            __result = ItemPlaceholders.For(id);
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Missing item stand-in", e);
        }
    }
}

/// <summary>Shops don't take stand-ins: selling one would lose the item for good.</summary>
[PatchArea("ItemPlaceholders")]
[HarmonyPatch(typeof(Trade), nameof(Trade.CheckIfAllowed))]
internal static class PlaceholderSalePatch
{
    private static void Postfix(ItemData itemData, ref bool __result)
    {
        try
        {
            if (__result && itemData != null && ItemPlaceholders.IsPlaceholder(itemData.item))
                __result = false;
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Missing item sale check", e);
        }
    }
}
