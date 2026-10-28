// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Features;

namespace ObenPatches.Fixes;

/// <summary>
/// TenementController.RemoveRequiredItems takes a renovation's materials from the tenement resource
/// storages and then the inventory, but it asks every storage for the full amount instead of what is still
/// missing, and then charges the inventory whatever the last storage couldn't give. With two or more
/// resource storages the player pays more than once. The fix walks the storages with the amount still
/// missing and takes only the rest from the inventory.
/// </summary>
internal sealed class RenovationMaterialsOnce : ModFeature
{
    public const string Key = "RenovationMaterialsOnce";

    public override string Id => Key;
    public override string Name => "Renovations take their materials only once";
    public override string Description =>
        "With several tenement resource storages, a renovation takes each material once instead of from every storage and your inventory.";
}

[HarmonyPatchCategory(RenovationMaterialsOnce.Key)]
[HarmonyPatch(typeof(TenementController), nameof(TenementController.RemoveRequiredItems))]
internal static class RenovationMaterialsOncePatch
{
    // Last, so prefixes that change the amount (the framework's extra inventory sources) or take over the
    // call have had their say.
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(bool __runOriginal, int id, int amount, List<Storage> ___tenementResourceStorages)
    {
        if (!__runOriginal)
            return false;
        // With no storage or one, the game's loop gets the amount right.
        if (amount < 1 || ___tenementResourceStorages == null || ___tenementResourceStorages.Count < 2)
            return true;

        int missing = amount;
        foreach (var storage in ___tenementResourceStorages)
        {
            if (missing < 1)
                break;
            if (storage == null)
                continue;
            storage.RemoveItem(id, out int left, missing);
            missing = left;
        }
        if (missing > 0 && Inventory.instance != null)
            Inventory.instance.RemoveItems(id, missing, ignoreOwner: true, -1, ignoreCrime: true);
        return false;
    }
}
