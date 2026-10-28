using System;
using BeautifyEffect;
using HarmonyLib;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>Keeps a mod's blur on top of the game's blur switches and profile changes (menus, effects).</summary>
[HarmonyPatch]
internal static class BlurPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EffectsController), nameof(EffectsController.EnableBlur))]
    private static void Enabled() => Switched(true);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EffectsController), nameof(EffectsController.DisableBlur))]
    private static void Disabled() => Switched(false);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EffectsController), nameof(EffectsController.ChangeBeautifyProfiles), new[] { typeof(BeautifyProfile) })]
    private static void ProfileChanged() => Profile();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EffectsController), nameof(EffectsController.ChangeBeautifyProfilesToDefault))]
    private static void ProfileReset() => Profile();

    private static void Switched(bool on)
    {
        try
        {
            ScreenBlur.GameSwitched(on);
        }
        catch (Exception e)
        {
            WorldLog.Failed("Blur hook", e);
        }
    }

    private static void Profile()
    {
        try
        {
            ScreenBlur.ProfileChanged();
        }
        catch (Exception e)
        {
            WorldLog.Failed("Blur profile hook", e);
        }
    }
}
