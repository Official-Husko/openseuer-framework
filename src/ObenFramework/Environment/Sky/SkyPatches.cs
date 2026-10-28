// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ObenFramework.World;
using UnityEngine;
using UnityEngine.AzureSky;

namespace ObenFramework.Env;

/// <summary>
/// Hooks the game's sky writers: the game's values are put back before each runs and read again after, so
/// an override never hides what the game wanted. All of these return at once while no override holds.
/// </summary>
[HarmonyPatch]
internal static class SkyWriterPatches
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(UnderwaterEffect), nameof(UnderwaterEffect.EnableEffect), new[] { typeof(Color), typeof(float) });
        yield return AccessTools.Method(typeof(UnderwaterEffect), nameof(UnderwaterEffect.DisableEffect));
        yield return AccessTools.Method(typeof(SunDirection), "Update");
        yield return AccessTools.Method(typeof(LightsController), "Update");
        yield return AccessTools.Method(typeof(LightsController), nameof(LightsController.StartChangeAmbientColor), new[] { typeof(Color), typeof(float) });
        yield return AccessTools.Method(typeof(AzureEnvironmentController), "Update");
    }

    [HarmonyPrefix]
    private static void Before()
    {
        if (!SkyApply.Holding)
            return;
        try
        {
            SkyApply.BeforeGame();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Sky writer hook", e);
        }
    }

    [HarmonyFinalizer]
    private static void After()
    {
        try
        {
            SkyApply.AfterGame();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Sky writer hook", e);
        }
    }
}

/// <summary>Writes the overrides right before the sky dome sends its values to the shaders.</summary>
[HarmonyPatch(typeof(AzureSkyRenderController), "UpdateShaderUniforms")]
internal static class SkyDomePatch
{
    [HarmonyPrefix]
    private static void Before()
    {
        if (!WorldSky.Active && !SkyApply.Holding)
            return;
        try
        {
            SkyApply.Run();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Sky dome hook", e);
        }
    }
}
