// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Npcs;
using ObenFramework.Player;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Animals;

/// <summary>
/// The animals in the scene you are in: cats, chickens and chicks, pigs, rats. Reads look the scene over
/// each time, so call them when something happened, not every frame. To make an animal do something see
/// <see cref="AnimalActions"/>; breeding pens and rat traps are in <see cref="Breeding"/>.
/// </summary>
public static class Animals
{
    /// <summary>
    /// An animal died (killed, starved). The record is read just before its object goes. Each handler runs
    /// in its own try/catch; the hook returns at once while nobody listens.
    /// </summary>
    public static readonly WorldEvent<AnimalRecord> Died = new WorldEvent<AnimalRecord>(nameof(Died));

    /// <summary>How many animals the game counts as alive (it stops spawning more at <see cref="Limit"/>).</summary>
    public static int Count => AnimalController.instance != null ? AnimalController.instance.animalCounter : 0;

    /// <summary>The game's cap on animals (eggs stop hatching above it).</summary>
    public static int Limit => AnimalController.instance != null ? AnimalController.instance.animalLimit : 0;

    /// <summary>Every animal in the scene, switched off ones left out.</summary>
    public static List<AnimalRecord> All()
    {
        var result = new List<AnimalRecord>();
        var following = Following();
        foreach (var animal in Scan())
            result.Add(new AnimalRecord(animal, following != null && following.Contains(animal)));
        return result;
    }

    /// <summary>The animal with this id, or null.</summary>
    public static AnimalRecord Get(int id)
    {
        var animal = Find(id);
        if (animal == null)
            return null;
        var following = Following();
        return new AnimalRecord(animal, following != null && following.Contains(animal));
    }

    /// <summary>
    /// The animal closest to <paramref name="from"/> (the player when omitted), of <paramref name="kind"/>
    /// when given, within <paramref name="maxDistance"/> metres when that is above zero. Null when there is none.
    /// </summary>
    public static AnimalRecord Nearest(Vector3? from = null, AnimalKind? kind = null, float maxDistance = 0f)
    {
        var origin = from ?? PlayerRef.Position;
        if (!origin.HasValue)
            return null;
        Animal best = null;
        float bestSqr = maxDistance > 0f ? maxDistance * maxDistance : float.MaxValue;
        foreach (var animal in Scan())
        {
            if (kind.HasValue && AnimalRecord.KindOf(animal) != kind.Value)
                continue;
            float sqr = (animal.transform.position - origin.Value).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                best = animal;
                bestSqr = sqr;
            }
        }
        return best != null ? Get(best.GetInstanceID()) : null;
    }

    /// <summary>
    /// Hurts the animal the way a hit does (it flees, a tame one turns wild) and kills it when its health runs
    /// out. False when it is gone, already dead, or a save or load runs.
    /// </summary>
    public static bool Damage(int id, float amount)
    {
        if (!(amount > 0f) || float.IsInfinity(amount))
            throw new ArgumentException("The amount must be a finite number above 0.", nameof(amount));
        var animal = Find(id);
        var stats = animal != null ? animal.GetComponent<AnimalStats>() : null;
        if (stats == null || stats.animalData == null || stats.animalData.currentHealth <= 0f || SaveGate.IsBusy || DamageController.instance == null)
            return false;
        try
        {
            stats.Damage(DamageController.instance.GetDamageType(DamageTypeNames.Violence), amount);
            return true;
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Animals.Damage", e);
            return false;
        }
    }

    /// <summary>A live, active animal by id, or null. For the other animal classes.</summary>
    internal static Animal Find(int id)
    {
        if (SaveGate.IsBusy)
            return null;
        foreach (var animal in Scan())
        {
            if (animal.GetInstanceID() == id)
                return animal;
        }
        return null;
    }

    internal static Animal[] Scan()
    {
        if (!Game.WorldState.InWorld)
            return Array.Empty<Animal>();
        var found = UnityEngine.Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int live = 0;
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].isActiveAndEnabled)
                found[live++] = found[i];
        }
        if (live < found.Length)
            Array.Resize(ref found, live);
        return found;
    }

    private static List<Animal> Following()
    {
        var controller = AnimalController.instance;
        return controller != null ? controller.followingAnimals : null;
    }
}
