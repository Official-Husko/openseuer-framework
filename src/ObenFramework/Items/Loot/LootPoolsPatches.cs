// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>
/// Puts mod loot back whenever the game reads its loot table, and reports containers filling themselves. The
/// fill hook compares the slots before and after <c>Storage.SpawnItems</c>, and only while someone listens.
/// </summary>
[HarmonyPatch]
internal static class LootPoolsPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StorageSpawnCategoryDatabase), nameof(StorageSpawnCategoryDatabase.ConstructSpawnCategoryDatabase))]
    private static void TableBuilt()
    {
        try { LootPools.Sync(); }
        catch (Exception e) { ItemsLog.Failed("Adding mod loot to the loot table", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Storage), nameof(Storage.SpawnItems))]
    private static void Filling(Storage __instance, bool ___removeOldItems, out int[] __state)
    {
        __state = null;
        if (!LootPools.ContainerFilled.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            var slots = __instance.storageSlots;
            if (slots == null)
                return;
            __state = new int[slots.Length];
            for (int i = 0; i < slots.Length; i++)
                __state[i] = slots[i] == null || ___removeOldItems ? -1 : slots[i].itemId;
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Container fill hook", e);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Storage), nameof(Storage.SpawnItems))]
    private static void Filled(Storage __instance, int[] __state)
    {
        if (__state == null)
            return;
        try
        {
            var slots = __instance.storageSlots;
            var added = new List<int>();
            for (int i = 0; i < slots.Length; i++)
            {
                int before = i < __state.Length ? __state[i] : -1;
                if (slots[i] != null && slots[i].itemId != -1 && slots[i].itemId != before)
                    added.Add(slots[i].itemId);
            }
            if (added.Count > 0)
                LootPools.ContainerFilled.Raise(new ContainerFilled(Containers.Describe(__instance), added));
        }
        catch (Exception e)
        {
            ItemsLog.Failed("Container fill hook", e);
        }
    }
}
