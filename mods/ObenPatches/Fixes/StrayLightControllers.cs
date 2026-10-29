using HarmonyLib;
using ObenFramework.Features;
using UnityEngine;

namespace ObenPatches.Fixes;

/// <summary>
/// Three lamps in the game carry a LightController on the lamp's mesh with no Light next to it (one in Open Sewer
/// Tenement, two in Bazaar interiors). Its Awake reads the light on its second line, so every load of those levels
/// logs a NullReferenceException, and Unity then switches the controller off. The fix switches such a controller off
/// before Awake runs: the same end state, without the error.
/// </summary>
internal sealed class StrayLightControllers : ModFeature
{
    public const string Key = "StrayLightControllers";

    public override string Id => Key;
    public override string Name => "No error from lamps without a light";
    public override string Description => "Switches off the light controllers that sit on a lamp without a light, which log an error each time their level loads.";
}

[HarmonyPatchCategory(StrayLightControllers.Key)]
[HarmonyPatch(typeof(LightController), "Awake")]
internal static class StrayLightControllersPatch
{
    private static bool Prefix(LightController __instance)
    {
        if (__instance.GetComponent<Light>() != null)
            return true;
        // A disabled behaviour gets no Start or Update, so it never joins the game's light lists either.
        __instance.enabled = false;
        return false;
    }
}
