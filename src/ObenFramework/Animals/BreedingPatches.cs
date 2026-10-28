// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using HarmonyLib;
using ObenFramework.Npcs;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.Animals;

/// <summary>
/// The game side of <see cref="Breeding.Bred"/> and <see cref="Breeding.TrapSprung"/>. A pen's
/// <c>Breed</c> plants each newborn with <c>Growing.PlantExternal</c>, so the breed call marks which pen is
/// at work and the plant call reports the young. Every hook returns at once while nobody listens.
/// </summary>
[HarmonyPatch]
internal static class BreedingPatches
{
    private static AnimalBreeder _breeding;
    private static bool _catchUp;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimalBreeder), nameof(AnimalBreeder.Breed))]
    private static void BeforeBreed(AnimalBreeder __instance, int ageInSeconds)
    {
        if (!Breeding.Bred.HasSubscribers)
            return;
        _breeding = __instance;
        _catchUp = ageInSeconds > 1;
    }

    [HarmonyFinalizer]
    [HarmonyPatch(typeof(AnimalBreeder), nameof(AnimalBreeder.Breed))]
    private static void AfterBreed() => _breeding = null;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Growing), nameof(Growing.PlantExternal))]
    private static void AfterPlant(Item plant, GameObject __result)
    {
        if (_breeding == null || plant == null || __result == null || !Breeding.Bred.HasSubscribers)
            return;
        try { Breeding.Bred.Raise(new BredAnimal(ObjectAddress.Of(_breeding).ToString(), plant, _catchUp)); }
        catch (Exception e) { NpcsLog.Failed("Animal bred hook", e); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RatTrap), "OnTriggerEnter")]
    private static void BeforeTrap(RatTrap __instance, out bool __state) => __state = __instance.triggered;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RatTrap), "OnTriggerEnter")]
    private static void AfterTrap(RatTrap __instance, bool __state, RatTrap.State ___state)
    {
        if (__state || !__instance.triggered || !Breeding.TrapSprung.HasSubscribers)
            return;
        try { Breeding.TrapSprung.Raise(new TrapSnap(__instance, CatchOf(___state))); }
        catch (Exception e) { NpcsLog.Failed("Rat trap hook", e); }
    }

    private static TrapCatch CatchOf(RatTrap.State state) =>
        state == RatTrap.State.Rat ? TrapCatch.Rat : state == RatTrap.State.GlowingRat ? TrapCatch.GlowingRat : TrapCatch.Empty;
}
