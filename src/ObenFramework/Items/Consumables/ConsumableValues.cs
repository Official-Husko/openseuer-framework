// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using OS.Items;

namespace ObenFramework.Items;

/// <summary>
/// What eating, drinking or using an item does to the player: the values of the game's <c>ItemConsumable</c>
/// action. Positive food and drink fill the bars; the other values follow the game's own signs. Start from
/// <see cref="ItemConsumables.Get"/> and <see cref="Copy"/> rather than a new object, or the values you don't
/// set become 0.
/// </summary>
public sealed class ConsumableValues
{
    /// <summary>What kind of consumable the item is; it decides what <see cref="Amount"/> fills. Read only.</summary>
    public ItemConsumable.ConsumableType Type { get; internal set; }

    /// <summary>
    /// The item's main value, most items' whole effect: food for food, drink for drinks, nicotine for smokes, and
    /// nothing for the other types. <see cref="Food"/>, <see cref="Drink"/> and <see cref="Nicotine"/> add to it.
    /// </summary>
    public int Amount { get; set; }

    public bool DestroyAfterUse { get; set; } = true;
    public float Food { get; set; }
    public float Drink { get; set; }
    public float Nicotine { get; set; }
    public float Intoxication { get; set; }
    public float Smv { get; set; }
    public float SmvProgressionRate { get; set; }
    public float Depression { get; set; }
    public float Health { get; set; }
    public float Bleeding { get; set; }
    public float High { get; set; }
    public float HighOrange { get; set; }
    public float HighOrangeBad { get; set; }
    public float Tiredness { get; set; }
    public float Hygiene { get; set; }
    public float AlcoholAddiction { get; set; }
    public float MushroomAddiction { get; set; }
    public float SmokingAddiction { get; set; }

    /// <summary>An extra effect such as coffee's or sleeping pills', with <see cref="SpecialEffectAmount"/> as its strength.</summary>
    public ItemConsumable.SpecialEffect SpecialEffect { get; set; }

    public float SpecialEffectAmount { get; set; }

    /// <summary>A copy, to change some values of an item and keep the rest.</summary>
    public ConsumableValues Copy() => (ConsumableValues)MemberwiseClone();

    internal bool IsFinite() =>
        Finite(Food) && Finite(Drink) && Finite(Nicotine) && Finite(Intoxication) && Finite(Smv) && Finite(SmvProgressionRate)
        && Finite(Depression) && Finite(Health) && Finite(Bleeding) && Finite(High) && Finite(HighOrange) && Finite(HighOrangeBad)
        && Finite(Tiredness) && Finite(Hygiene) && Finite(AlcoholAddiction) && Finite(MushroomAddiction) && Finite(SmokingAddiction)
        && Finite(SpecialEffectAmount);

    internal bool SameAs(ConsumableValues o) =>
        o != null && Amount == o.Amount && DestroyAfterUse == o.DestroyAfterUse && Food == o.Food && Drink == o.Drink
        && Nicotine == o.Nicotine && Intoxication == o.Intoxication && Smv == o.Smv && SmvProgressionRate == o.SmvProgressionRate
        && Depression == o.Depression && Health == o.Health && Bleeding == o.Bleeding && High == o.High && HighOrange == o.HighOrange
        && HighOrangeBad == o.HighOrangeBad && Tiredness == o.Tiredness && Hygiene == o.Hygiene && AlcoholAddiction == o.AlcoholAddiction
        && MushroomAddiction == o.MushroomAddiction && SmokingAddiction == o.SmokingAddiction && SpecialEffect == o.SpecialEffect
        && SpecialEffectAmount == o.SpecialEffectAmount;

    internal static ConsumableValues Read(ItemConsumable c) => new ConsumableValues
    {
        Type = c.Type,
        Amount = c.Amount,
        DestroyAfterUse = c.DestroyAfterUse,
        Food = c.Food,
        Drink = c.Drink,
        Nicotine = c.Nicotine,
        Intoxication = c.Intoxication,
        Smv = c.SMV,
        SmvProgressionRate = c.SMVProgressionRate,
        Depression = c.Depression,
        Health = c.Health,
        Bleeding = c.Bleeding,
        High = c.High,
        HighOrange = c.HighOrange,
        HighOrangeBad = c.HighOrangeBad,
        Tiredness = c.Tiredness,
        Hygiene = c.Hygiene,
        AlcoholAddiction = c.AlcoholAddiction,
        MushroomAddiction = c.MushroomAddiction,
        SmokingAddiction = c.SmokingAddiction,
        SpecialEffect = c.SpecialEffectName,
        SpecialEffectAmount = c.SpecialEffectAmount,
    };

    // Everything but the type, which changes how the game uses the item.
    internal void WriteTo(ItemConsumable c)
    {
        c.Amount = Amount;
        c.DestroyAfterUse = DestroyAfterUse;
        c.Food = Food;
        c.Drink = Drink;
        c.Nicotine = Nicotine;
        c.Intoxication = Intoxication;
        c.SMV = Smv;
        c.SMVProgressionRate = SmvProgressionRate;
        c.Depression = Depression;
        c.Health = Health;
        c.Bleeding = Bleeding;
        c.High = High;
        c.HighOrange = HighOrange;
        c.HighOrangeBad = HighOrangeBad;
        c.Tiredness = Tiredness;
        c.Hygiene = Hygiene;
        c.AlcoholAddiction = AlcoholAddiction;
        c.MushroomAddiction = MushroomAddiction;
        c.SmokingAddiction = SmokingAddiction;
        c.SpecialEffectName = SpecialEffect;
        c.SpecialEffectAmount = SpecialEffectAmount;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
