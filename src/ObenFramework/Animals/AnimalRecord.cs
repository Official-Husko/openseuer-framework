// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using UnityEngine;

namespace ObenFramework.Animals;

/// <summary>The kinds of animal the game has.</summary>
public enum AnimalKind
{
    Other,
    Cat,
    Chick,
    Chicken,
    Pig,
    Rat,
}

/// <summary>
/// A copy of what is known about one animal in the scene you are in at the moment it was read. Nothing here
/// updates; read again for fresh values.
/// </summary>
public sealed class AnimalRecord
{
    private static readonly AccessTools.FieldRef<Animal, Animal.AnimalState> StateOf = AccessTools.FieldRefAccess<Animal, Animal.AnimalState>("currentState");

    /// <summary>The animal's id for this session (its object's instance id). Use <see cref="Guid"/> to recognise it across loads.</summary>
    public int Id { get; }

    public AnimalKind Kind { get; }

    /// <summary>The object's name.</summary>
    public string Name { get; }

    /// <summary>The save id the game keeps its stats under, or empty for an animal that is not saved.</summary>
    public string Guid { get; }

    public Vector3 Position { get; }

    /// <summary>Tame animals do not bite the player and can follow them.</summary>
    public bool Tame { get; }

    /// <summary>The animal follows the player (cat ears, a carried animal let go).</summary>
    public bool Following { get; }

    /// <summary>Running: fleeing, hunting, or told to by <see cref="AnimalActions.Run"/>.</summary>
    public bool Running { get; }

    /// <summary>What it is doing: Default (walking about), Attacking, Eating, Sleeping or Sitting.</summary>
    public Animal.AnimalState State { get; }

    public float Health { get; }
    public int MaxHealth { get; }

    /// <summary>How hungry it is, 0 (full) up to <see cref="MaxHunger"/>.</summary>
    public float Hunger { get; }
    public int MaxHunger { get; }

    /// <summary>Age in game seconds; a chick becomes a chicken at <see cref="FullGrownAge"/>.</summary>
    public float Age { get; }
    public int FullGrownAge { get; }

    /// <summary>The game's animal component. Check for Unity null before use.</summary>
    public Animal Live { get; }

    internal AnimalRecord(Animal animal, bool following)
    {
        Live = animal;
        Id = animal.GetInstanceID();
        Kind = KindOf(animal);
        Name = animal.name;
        Position = animal.transform.position;
        Tame = animal.tame;
        Following = following;
        Running = animal.Running();
        State = StateOf(animal);

        var stats = animal.GetComponent<AnimalStats>();
        var data = stats != null ? stats.animalData : null;
        Guid = stats != null && stats.GUID != null ? stats.GUID : string.Empty;
        if (data == null)
            return;
        Health = data.currentHealth;
        MaxHealth = data.maxHealth;
        Hunger = data.currentHunger;
        MaxHunger = data.maxHunger;
        Age = data.currentAge;
        FullGrownAge = data.fullGrownAge;
    }

    internal static Animal.AnimalState CurrentState(Animal animal) => StateOf(animal);

    internal static AnimalKind KindOf(Animal animal)
    {
        if (animal is Cat)
            return AnimalKind.Cat;
        if (animal is Chick)
            return AnimalKind.Chick;
        if (animal is Chicken)
            return AnimalKind.Chicken;
        if (animal is Pig)
            return AnimalKind.Pig;
        if (animal is Rat)
            return AnimalKind.Rat;
        return AnimalKind.Other;
    }

    public override string ToString() => $"{Kind} {Name} ({Id}) at {Position}";
}
