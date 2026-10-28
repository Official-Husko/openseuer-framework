using HarmonyLib;
using ObenFramework.Features;

namespace PerformanceFixes.Slots;

/// <summary>
/// Inventory.AllSlots joins the inventory, backpack and clothing slots into a new array with LINQ
/// on every call, and the game calls it from around fifty places, some of them every frame. This
/// keeps the joined array and hands it out again for as long as the slots it was built from are
/// the same. The moment any of them differ (another backpack, a changed slot list) a fresh array
/// is built and the old one is left alone, so a caller that is still holding one never sees it
/// change under it.
/// </summary>
internal sealed class InventorySlotBuffers : ModFeature
{
    public const string Key = "InventorySlotBuffers";

    public override string Id => Key;
    public override string Name => "Inventory slot list is reused";
    public override string Description => "Hands out the same joined slot array again instead of building a new one on every call.";

    protected override void OnDisable() => InventorySlotBuffersPatch.Clear();
}

[HarmonyPatchCategory(InventorySlotBuffers.Key)]
[HarmonyPatch(typeof(Inventory), nameof(Inventory.AllSlots))]
internal static class InventorySlotBuffersPatch
{
    // Inventory + clothing, inventory + backpack, and all three.
    private static SlotController[] _withClothes;
    private static SlotController[] _withBackpack;
    private static SlotController[] _withBoth;

    internal static void Clear()
    {
        _withClothes = null;
        _withBackpack = null;
        _withBoth = null;
    }

    private static bool Prefix(Inventory __instance, bool ignoreBackpack, bool ignoreCharacterSlots, ref SlotController[] __result)
    {
        var slots = __instance.Slots;
        var clothes = __instance.CharacterSlots;
        var backpackStorage = ignoreBackpack ? null : BackpackStorage.instance.GetStorage();

        if (backpackStorage == null)
        {
            __result = ignoreCharacterSlots ? slots : Joined(ref _withClothes, slots, clothes, null);
        }
        else
        {
            var backpack = backpackStorage.Slots;
            __result = ignoreCharacterSlots
                ? Joined(ref _withBackpack, slots, backpack, null)
                : Joined(ref _withBoth, slots, backpack, clothes);
        }
        return false;
    }

    private static SlotController[] Joined(ref SlotController[] cache, SlotController[] first, SlotController[] second, SlotController[] third)
    {
        int length = first.Length + second.Length + (third == null ? 0 : third.Length);
        if (cache != null && cache.Length == length && Matches(cache, first, second, third))
            return cache;

        var joined = new SlotController[length];
        first.CopyTo(joined, 0);
        second.CopyTo(joined, first.Length);
        if (third != null)
            third.CopyTo(joined, first.Length + second.Length);
        cache = joined;
        return joined;
    }

    private static bool Matches(SlotController[] cache, SlotController[] first, SlotController[] second, SlotController[] third)
    {
        int at = 0;
        for (int i = 0; i < first.Length; i++, at++)
        {
            if (cache[at] != first[i])
                return false;
        }
        for (int i = 0; i < second.Length; i++, at++)
        {
            if (cache[at] != second[i])
                return false;
        }
        if (third != null)
        {
            for (int i = 0; i < third.Length; i++, at++)
            {
                if (cache[at] != third[i])
                    return false;
            }
        }
        return true;
    }
}
