using System;
using HarmonyLib;
using ObenFramework.Npcs;

namespace ObenFramework.Animals;

/// <summary>Raises <see cref="Animals.Died"/> from <c>AnimalStats.Death</c>, which every animal death goes through.</summary>
[HarmonyPatch]
internal static class AnimalPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AnimalStats), nameof(AnimalStats.Death))]
    private static void BeforeDeath(AnimalStats __instance)
    {
        if (!Animals.Died.HasSubscribers)
            return;
        try
        {
            var animal = __instance.GetComponent<Animal>();
            if (animal != null)
                Animals.Died.Raise(new AnimalRecord(animal, AnimalController.instance != null && AnimalController.instance.followingAnimals.Contains(animal)));
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Animal died hook", e);
        }
    }
}
