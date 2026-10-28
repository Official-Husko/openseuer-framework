using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace PerformanceFixes.Lighting;

/// <summary>
/// Every physics tick the game asks each light zone whether the player is inside it, using LINQ
/// with closures, so every zone allocates a delegate and an enumerator on every tick. This does
/// the same two checks with plain loops. The zone logic, including when the current zone changes,
/// is untouched; only the garbage is gone.
/// </summary>
internal sealed class LightZoneCheck : ModFeature
{
    public const string Key = "LightZoneCheck";

    public override string Id => Key;
    public override string Name => "Light zone checks without garbage";
    public override string Description => "Checks which lighting zone the player is in without allocating on every physics tick.";
}

[HarmonyPatchCategory(LightZoneCheck.Key)]
[HarmonyPatch(typeof(LightCullZone), nameof(LightCullZone.CheckIfPlayerInZone))]
internal static class LightZoneCheckPatch
{
    private static bool Prefix(LightCullZone __instance, ref bool __result)
    {
        var zones = __instance.zones;
        var transform = __instance.transform;
        __result = false;
        if (zones.Count == 0)
        {
            Leave(__instance, zones, transform, default);
            return false;
        }

        Vector3 head = PlayerLocator.instance.playerHeadPosition;
        for (int i = 0; i < zones.Count; i++)
        {
            if (LightCullZone.IsWithinBoxBounds(head, zones[i], transform))
            {
                if (LightsController.CurrentZone != __instance)
                    LightsController.CurrentZone = __instance;
                __result = true;
                return false;
            }
        }
        Leave(__instance, zones, transform, head);
        return false;
    }

    // Outside every box: the current zone is cleared once the player is further than the buffer
    // from all of them (an empty list counts as further from all of them).
    private static void Leave(LightCullZone zone, List<Bounds> zones, Transform transform, Vector3 head)
    {
        if (LightsController.CurrentZone != zone || !zone.setNullOnTriggerExit)
            return;
        for (int i = 0; i < zones.Count; i++)
        {
            if (!(Vector3.Distance(Closest(zones[i], transform, head), head) > zone.bufferDistance))
                return;
        }
        LightsController.CurrentZone = null;
    }

    private static Vector3 Closest(Bounds bounds, Transform transform, Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        return transform.TransformPoint(new Vector3(
            Mathf.Clamp(local.x, center.x - extents.x, center.x + extents.x),
            Mathf.Clamp(local.y, center.y - extents.y, center.y + extents.y),
            Mathf.Clamp(local.z, center.z - extents.z, center.z + extents.z)));
    }
}
