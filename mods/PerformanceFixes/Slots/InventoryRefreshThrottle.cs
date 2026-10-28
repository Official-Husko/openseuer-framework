using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace PerformanceFixes.Slots;

/// <summary>
/// The inventory subscribes to the game clock's seconds event, which fires on nearly every frame,
/// and while the inventory screen is open it refreshes every slot each time: sprites, texts and
/// child objects of dozens of slots rewritten 60 times a second. This limits that to a few times a
/// second. Opening the screen and every other refresh (picking up, moving or using an item)
/// still happens right away, because only the clock-driven refresh is slowed down.
/// </summary>
internal sealed class InventoryRefreshThrottle : ModFeature
{
    public const string Key = "InventoryRefreshThrottle";

    public override string Id => Key;
    public override string Name => "Inventory screen refreshes a few times a second";
    public override string Description => "Stops the open inventory from redrawing every slot on every frame.";

    protected override void OnDisable() => InventoryRefreshThrottlePatch.Reset();
}

[HarmonyPatchCategory(InventoryRefreshThrottle.Key)]
[HarmonyPatch(typeof(Inventory))]
internal static class InventoryRefreshThrottlePatch
{
    private static float _lastRefresh;
    private static bool _wasOpen;

    internal static void Reset()
    {
        _lastRefresh = 0f;
        _wasOpen = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("DeltaSeconds")]
    private static bool Seconds(Inventory __instance)
    {
        var panel = __instance.inventoryPanelUI;
        if (panel == null || !panel.gameObject.activeInHierarchy)
        {
            _wasOpen = false;
            return true;
        }

        // The first tick after the screen opens always runs, so it never opens stale.
        if (!_wasOpen)
        {
            _wasOpen = true;
            return true;
        }

        float now = Time.unscaledTime;
        float interval = Mathf.Clamp(PerformanceFixesMod.Config.Value.InventoryRefreshSeconds, 0.05f, 2f);
        return now < _lastRefresh || now - _lastRefresh >= interval;
    }

    // Any refresh counts, including the ones the game asks for itself after a change.
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Inventory.UpdateAllSlotsNow))]
    private static void Refreshed() => _lastRefresh = Time.unscaledTime;
}
