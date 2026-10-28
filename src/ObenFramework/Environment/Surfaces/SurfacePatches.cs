using System;
using HarmonyLib;
using ObenFramework.World;
using OS.Items;

namespace ObenFramework.Env;

/// <summary>Raises <see cref="Surfaces.SurfaceChanged"/> when a surface's covering really changes.</summary>
[HarmonyPatch]
internal static class SurfacePatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(FurnitureSurfaceArea), nameof(FurnitureSurfaceArea.SetWallpaper), new[] { typeof(ItemWallpaperData), typeof(int), typeof(int) })]
    private static void BeforeSet(FurnitureSurfaceArea __instance, out long __state) => __state = Key(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FurnitureSurfaceArea), nameof(FurnitureSurfaceArea.SetWallpaper), new[] { typeof(ItemWallpaperData), typeof(int), typeof(int) })]
    private static void AfterSet(FurnitureSurfaceArea __instance, long __state) => Raise(__instance, __state);

    [HarmonyPrefix]
    [HarmonyPatch(typeof(FurnitureSurfaceArea), nameof(FurnitureSurfaceArea.RemoveWallpaper))]
    private static void BeforeRemove(FurnitureSurfaceArea __instance, out long __state) => __state = Key(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(FurnitureSurfaceArea), nameof(FurnitureSurfaceArea.RemoveWallpaper))]
    private static void AfterRemove(FurnitureSurfaceArea __instance, long __state) => Raise(__instance, __state);

    // Item and variant packed into one value, so the prefix state is a plain number.
    private static long Key(FurnitureSurfaceArea surface) => ((long)Surfaces.ItemOf(surface) << 32) | (uint)Surfaces.SkinOf(surface);

    private static void Raise(FurnitureSurfaceArea surface, long before)
    {
        if (Key(surface) == before || !Surfaces.SurfaceChanged.HasSubscribers || !WorldChanges.Active)
            return;
        try
        {
            Surfaces.SurfaceChanged.Raise(Surfaces.Describe(surface, WorldChanges.Current));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Surface hook", e);
        }
    }
}
