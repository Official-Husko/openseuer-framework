using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace ObenPatches.Fixes;

/// <summary>
/// RefreshCubemapInTrigger is meant to re-render a reflection probe while the player stands in its
/// trigger, but it compares the collider's tag with "Player)" (a stray parenthesis), so it never
/// does. Fixed literally it would render the probe every frame, which is expensive, so this
/// version renders it when the player walks in and then a few times a second while they stay.
/// Off by default until it has been seen in a level.
/// </summary>
internal sealed class ReflectionProbeRefresh : ModFeature
{
    public const string Key = "ReflectionProbeRefresh";

    public override string Id => Key;
    public override string Name => "Reflections update near the player";
    public override string Description => "Reflection probes that should refresh while you stand near them do so, a few times a second.";
    public override bool EnabledByDefault => false;

    protected override void OnDisable() => ReflectionProbeRefreshPatch.NextRender.Clear();
}

[HarmonyPatchCategory(ReflectionProbeRefresh.Key)]
[HarmonyPatch(typeof(RefreshCubemapInTrigger))]
internal static class ReflectionProbeRefreshPatch
{
    private const float Interval = 0.25f;

    internal static readonly Dictionary<RefreshCubemapInTrigger, float> NextRender = new Dictionary<RefreshCubemapInTrigger, float>();

    [HarmonyPrefix]
    [HarmonyPatch("OnTriggerEnter")]
    private static bool Enter(RefreshCubemapInTrigger __instance, Collider other, ref bool ___update)
    {
        if (other.CompareTag("Player"))
        {
            ___update = true;
            NextRender[__instance] = 0f;
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("OnTriggerExit")]
    private static bool Exit(RefreshCubemapInTrigger __instance, Collider other, ref bool ___update)
    {
        if (other.CompareTag("Player"))
        {
            ___update = false;
            NextRender.Remove(__instance);
        }
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    private static bool Update(RefreshCubemapInTrigger __instance, bool ___update)
    {
        if (!___update || !NextRender.TryGetValue(__instance, out float next) || Time.unscaledTime < next)
            return false;
        NextRender[__instance] = Time.unscaledTime + Interval;
        var probe = __instance.GetComponent<ReflectionProbe>();
        if (probe != null)
            probe.RenderProbe();
        return false;
    }
}
