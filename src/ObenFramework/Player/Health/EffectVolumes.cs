// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Reflection;
using HarmonyLib;

namespace ObenFramework.Player;

/// <summary>
/// Reads the private state of the game's drunk and mushroom effects. The strength of each is the weight of
/// its post-processing volume, a type from Unity's post-processing package the framework doesn't build
/// against, so the volume is read as an object and its weight through one cached field. Only used on
/// request, never per frame.
/// </summary>
internal static class EffectVolumes
{
    private static readonly AccessTools.FieldRef<MushroomEffect, bool> MushroomOnField =
        AccessTools.FieldRefAccess<MushroomEffect, bool>("shroomsEnabled");
    private static readonly AccessTools.FieldRef<OrangeMushroomEffect, bool> OrangeOnField =
        AccessTools.FieldRefAccess<OrangeMushroomEffect, bool>("shroomsEnabled");

    private static readonly FieldInfo DrunkVolume = AccessTools.Field(typeof(DrunkEffect), "postProcessVolume");
    private static readonly FieldInfo MushroomVolume = AccessTools.Field(typeof(MushroomEffect), "postProcessVolume");
    private static readonly FieldInfo OrangeVolume = AccessTools.Field(typeof(OrangeMushroomEffect), "postProcessVolume");
    private static readonly FieldInfo OrangeBadVolume = AccessTools.Field(typeof(OrangeMushroomEffect), "postProcessVolumeBad");
    private static readonly FieldInfo Weight = DrunkVolume != null ? AccessTools.Field(DrunkVolume.FieldType, "weight") : null;

    public static bool MushroomOn() => MushroomEffect.instance != null && MushroomOnField(MushroomEffect.instance);

    public static bool OrangeOn() => OrangeMushroomEffect.instance != null && OrangeOnField(OrangeMushroomEffect.instance);

    public static float Drunk() => WeightOf(DrunkVolume, DrunkEffect.instance);

    public static float Mushroom() => WeightOf(MushroomVolume, MushroomEffect.instance);

    public static float Orange() => Math.Max(WeightOf(OrangeVolume, OrangeMushroomEffect.instance), WeightOf(OrangeBadVolume, OrangeMushroomEffect.instance));

    private static float WeightOf(FieldInfo volumeField, UnityEngine.Object owner)
    {
        if (owner == null || volumeField == null || Weight == null)
            return 0f;
        var volume = volumeField.GetValue(owner) as UnityEngine.Object;
        if (volume == null)
            return 0f;
        float weight = (float)Weight.GetValue(volume);
        return Math.Min(1f, Math.Max(0f, weight));
    }
}
