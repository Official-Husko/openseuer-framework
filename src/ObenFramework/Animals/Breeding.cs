// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.Animals;

/// <summary>What a rat trap caught.</summary>
public enum TrapCatch
{
    Empty,
    Rat,
    GlowingRat,
}

/// <summary>A breeding pen (an <c>AnimalBreeder</c> on a growing station) in the scene you are in.</summary>
public sealed class BreederRecord
{
    /// <summary>Stable address of the pen (see <see cref="ObjectAddress"/>).</summary>
    public string Address { get; }

    public Vector3 Position { get; }

    /// <summary>Animals in the pen now.</summary>
    public int Animals { get; }

    /// <summary>Places in the pen.</summary>
    public int Places { get; }

    /// <summary>Chance per game hour, 0 to 1, that a pair produces young.</summary>
    public float ChancePerHour { get; }

    internal BreederRecord(AnimalBreeder breeder)
    {
        Address = ObjectAddress.Of(breeder).ToString();
        Position = breeder.transform.position;
        ChancePerHour = breeder.breedChancePerHour;
        var growing = breeder.GetComponent<Growing>();
        if (growing == null)
            return;
        foreach (var spot in growing.GrowingSlots)
        {
            if (spot == null)
                continue;
            Places++;
            if (spot.Plant != null)
                Animals++;
        }
    }
}

/// <summary>Young born in a breeding pen (see <see cref="Breeding.Bred"/>).</summary>
public sealed class BredAnimal
{
    /// <summary>The pen's address.</summary>
    public string Breeder { get; }

    /// <summary>Item id of the young animal.</summary>
    public int ItemId { get; }

    /// <summary>Its item name.</summary>
    public string ItemName { get; }

    /// <summary>Born while the player was away: the game catching up after a load.</summary>
    public bool CatchUp { get; }

    internal BredAnimal(string breeder, Item item, bool catchUp)
    {
        Breeder = breeder;
        ItemId = item.ID;
        ItemName = item.Title;
        CatchUp = catchUp;
    }
}

/// <summary>A rat trap snapped (see <see cref="Breeding.TrapSprung"/>).</summary>
public sealed class TrapSnap
{
    /// <summary>The trap's address.</summary>
    public string Trap { get; }

    public Vector3 Position { get; }

    public TrapCatch Catch { get; }

    internal TrapSnap(RatTrap trap, TrapCatch caught)
    {
        Trap = ObjectAddress.Of(trap).ToString();
        Position = trap.transform.position;
        Catch = caught;
    }
}

/// <summary>Breeding pens and rat traps: list the pens, hear about young being born and traps snapping.</summary>
public static class Breeding
{
    /// <summary>A pen produced young. Each handler runs in its own try/catch; the hook does nothing while nobody listens.</summary>
    public static readonly WorldEvent<BredAnimal> Bred = new WorldEvent<BredAnimal>(nameof(Bred));

    /// <summary>A rat trap snapped shut, on a rat or on nothing.</summary>
    public static readonly WorldEvent<TrapSnap> TrapSprung = new WorldEvent<TrapSnap>(nameof(TrapSprung));

    /// <summary>The breeding pens in the scene. Looks the scene over; not for every frame.</summary>
    public static List<BreederRecord> Breeders()
    {
        var result = new List<BreederRecord>();
        if (!Game.WorldState.InWorld)
            return result;
        foreach (var breeder in Object.FindObjectsByType<AnimalBreeder>(FindObjectsSortMode.None))
        {
            if (breeder != null && breeder.isActiveAndEnabled)
                result.Add(new BreederRecord(breeder));
        }
        return result;
    }
}
