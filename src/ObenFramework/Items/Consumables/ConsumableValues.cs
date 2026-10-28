// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using OS.Items;

namespace ObenFramework.Items;

/// <summary>
/// What eating, drinking or using an item does to the player: the values of the game's <c>ItemConsumable</c>
/// action. Positive food and drink fill the bars; the other values follow the game's own signs.
/// </summary>
public sealed class ConsumableValues
{
    public bool DestroyAfterUse { get; set; } = true;
    public float Food { get; set; }
    public float Drink { get; set; }
    public float Nicotine { get; set; }
    public float Intoxication { get; set; }
    public float Smv { get; set; }
    public float Depression { get; set; }
    public float Health { get; set; }
    public float Bleeding { get; set; }
    public float High { get; set; }
    public float Tiredness { get; set; }
    public float Hygiene { get; set; }
    public float AlcoholAddiction { get; set; }
    public float MushroomAddiction { get; set; }
    public float SmokingAddiction { get; set; }

    /// <summary>A copy, to change some values of an item and keep the rest.</summary>
    public ConsumableValues Copy() => (ConsumableValues)MemberwiseClone();

    internal bool IsFinite() =>
        Finite(Food) && Finite(Drink) && Finite(Nicotine) && Finite(Intoxication) && Finite(Smv) && Finite(Depression) && Finite(Health)
        && Finite(Bleeding) && Finite(High) && Finite(Tiredness) && Finite(Hygiene) && Finite(AlcoholAddiction) && Finite(MushroomAddiction)
        && Finite(SmokingAddiction);

    internal static ConsumableValues Read(ItemConsumable c) => new ConsumableValues
    {
        DestroyAfterUse = c.DestroyAfterUse,
        Food = c.Food,
        Drink = c.Drink,
        Nicotine = c.Nicotine,
        Intoxication = c.Intoxication,
        Smv = c.SMV,
        Depression = c.Depression,
        Health = c.Health,
        Bleeding = c.Bleeding,
        High = c.High,
        Tiredness = c.Tiredness,
        Hygiene = c.Hygiene,
        AlcoholAddiction = c.AlcoholAddiction,
        MushroomAddiction = c.MushroomAddiction,
        SmokingAddiction = c.SmokingAddiction,
    };

    internal void WriteTo(ItemConsumable c)
    {
        c.DestroyAfterUse = DestroyAfterUse;
        c.Food = Food;
        c.Drink = Drink;
        c.Nicotine = Nicotine;
        c.Intoxication = Intoxication;
        c.SMV = Smv;
        c.Depression = Depression;
        c.Health = Health;
        c.Bleeding = Bleeding;
        c.High = High;
        c.Tiredness = Tiredness;
        c.Hygiene = Hygiene;
        c.AlcoholAddiction = AlcoholAddiction;
        c.MushroomAddiction = MushroomAddiction;
        c.SmokingAddiction = SmokingAddiction;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
