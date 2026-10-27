using System;
using System.Collections.Generic;

namespace ObenFramework.Player;

/// <summary>
/// Every player stat the game exposes, named like the F1 console's <c>player_stats</c> command
/// (<c>player_stats thirst set 20</c> and <c>PlayerStat.Thirst.Set(20)</c> are the same thing).
/// Values are written straight to the game's fields as the console does, then the stat bars refresh.
/// </summary>
/// <remarks>
/// Scales: health 0-100 (100 is full), hunger and thirst -100 to 100 (100 is starving), bladder and
/// bowel 0-100 (100 is worst), tiredness -100 to 100 (100 passes out), hygiene 0-100 (100 is clean),
/// depression 0-100 (100 is worst), alcohol in blood above 15 shows drunk effects and above 100 passes out.
/// </remarks>
public static class PlayerStat
{
    private static readonly List<PlayerStatRef> Registry = new List<PlayerStatRef>();
    private static readonly Dictionary<string, PlayerStatRef> ByName = new Dictionary<string, PlayerStatRef>(StringComparer.OrdinalIgnoreCase);

    // Health.
    public static readonly PlayerStatRef Health = Def("health", "health", "Health, 0 to 100.", s => s.health.currentStatus, (s, v) => s.health.currentStatus = v, 0f, 100f);
    public static readonly PlayerStatRef Bleeding = Def("bleeding", "health", "Bleeding, 0 to 100. Drains health while above 0.", s => s.health.Bleeding, (s, v) => s.health.Bleeding = v, 0f, 100f);
    public static readonly PlayerStatRef HealthToRegenerate = Def("health_to_regenerate", "health", "Health still to be regained over time (negative loses health).", s => s.health.HealthToRegenerate, (s, v) => s.health.HealthToRegenerate = v);

    // Needs.
    public static readonly PlayerStatRef Hunger = Def("hunger", "needs", "Hunger, -100 to 100. At 100 you take starvation damage.", s => s.hungerAndThirst.Hunger, (s, v) => s.hungerAndThirst.Hunger = v, -100f, 100f);
    public static readonly PlayerStatRef Thirst = Def("thirst", "needs", "Thirst, -100 to 100. At 100 you take dehydration damage.", s => s.hungerAndThirst.Thirst, (s, v) => s.hungerAndThirst.Thirst = v, -100f, 100f);
    public static readonly PlayerStatRef Bladder = Def("bladder", "needs", "Bladder, 0 to 100.", s => s.hungerAndThirst.Bladder, (s, v) => s.hungerAndThirst.Bladder = v, 0f, 100f);
    public static readonly PlayerStatRef Bowel = Def("bowel", "needs", "Bowel, 0 to 100.", s => s.hungerAndThirst.Bowel, (s, v) => s.hungerAndThirst.Bowel = v, 0f, 100f);
    public static readonly PlayerStatRef Tiredness = Def("tiredness", "needs", "Tiredness, -100 to 100. At 100 you pass out.", s => s.tiredness.currentStatus, (s, v) => s.tiredness.currentStatus = v, -100f, 100f);
    public static readonly PlayerStatRef Hygiene = Def("hygiene", "needs", "Hygiene, 0 to 100 (100 is clean).", s => s.hygiene.Hygiene, (s, v) => s.hygiene.Hygiene = v, 0f, 100f);
    public static readonly PlayerStatRef Duck = Def("duck", "needs", "The duck need.", s => s.duck.currentStatus, (s, v) => s.duck.currentStatus = v);

    // Mind and body conditions.
    public static readonly PlayerStatRef Depression = Def("depression", "mind", "Depression (mental health), 0 fine to 100 worst.", s => s.mentalHealth.currentStatus, (s, v) => s.mentalHealth.currentStatus = v, 0f, 100f);
    public static readonly PlayerStatRef DepressionTarget = Def("depression_target", "mind", "The depression level the mind drifts toward, 0 to 100.", s => s.mentalHealth.mentalHealthTarget, (s, v) => s.mentalHealth.mentalHealthTarget = v, 0f, 100f);
    public static readonly PlayerStatRef ExpectationLevel = Def("expectation_level", "mind", "How good things must be to lift the mood.", s => s.mentalHealth.expectationLevel, (s, v) => s.mentalHealth.expectationLevel = v);
    public static readonly PlayerStatRef Overheat = Def("overheat", "body", "Sauna overheating, from 0.", s => s.sauna.Overheat, (s, v) => s.sauna.Overheat = v, 0f, null);
    public static readonly PlayerStatRef Dose = Def("dose", "body", "Radiation dose, from 0.", s => s.radiation.Dose, (s, v) => s.radiation.Dose = v, 0f, null);
    public static readonly PlayerStatRef SmvProgression = Def("smv_progression", "body", "SMV infection progress, 0 to 100 (death above 100).", s => s.mushrooms.SMVProgression, (s, v) => s.mushrooms.SMVProgression = v, 0f, 100f);

    // Alcohol and mushrooms.
    public static readonly PlayerStatRef AlcoholInBlood = Def("alcohol_in_blood", "alcohol", "Blood alcohol. Drunk effects above 15, pass out above 100.", s => s.alcohol.AlcoholInBlood, (s, v) => s.alcohol.AlcoholInBlood = v, 0f, null);
    public static readonly PlayerStatRef AlcoholDrunk = Def("alcohol_drunk", "alcohol", "Alcohol in the stomach, not yet absorbed.", s => s.alcohol.AlcoholDrunk, (s, v) => s.alcohol.AlcoholDrunk = v, 0f, null);
    public static readonly PlayerStatRef MushroomHigh = Def("mushroom_high", "mushrooms", "How high on mushrooms.", s => s.mushrooms.MushroomsHigh, (s, v) => s.mushrooms.MushroomsHigh = v, 0f, null);
    public static readonly PlayerStatRef MushroomsUsed = Def("mushrooms_used", "mushrooms", "Mushrooms eaten and not yet absorbed.", s => s.mushrooms.MushroomsUsed, (s, v) => s.mushrooms.MushroomsUsed = v, 0f, null);

    // Addictions and their cravings, 0 to 100.
    public static readonly PlayerStatRef AlcoholAddiction = Def("alcohol_addiction", "addictions", "Alcohol addiction, 0 to 100.", s => s.addictions.AlcoholAddiction, (s, v) => s.addictions.AlcoholAddiction = v, 0f, 100f);
    public static readonly PlayerStatRef AlcoholNeed = Def("alcohol_need", "addictions", "Alcohol craving, 0 to 100.", s => s.addictions.AlcoholNeed, (s, v) => s.addictions.AlcoholNeed = v, 0f, 100f);
    public static readonly PlayerStatRef MushroomAddiction = Def("mushroom_addiction", "addictions", "Mushroom addiction, 0 to 100.", s => s.addictions.MushroomAddiction, (s, v) => s.addictions.MushroomAddiction = v, 0f, 100f);
    public static readonly PlayerStatRef MushroomNeed = Def("mushroom_need", "addictions", "Mushroom craving, 0 to 100.", s => s.addictions.MushroomNeed, (s, v) => s.addictions.MushroomNeed = v, 0f, 100f);
    public static readonly PlayerStatRef SmokingAddiction = Def("smoking_addiction", "addictions", "Smoking addiction, 0 to 100.", s => s.addictions.SmokingAddiction, (s, v) => s.addictions.SmokingAddiction = v, 0f, 100f);
    public static readonly PlayerStatRef SmokingNeed = Def("smoking_need", "addictions", "Smoking craving, 0 to 100.", s => s.addictions.SmokingNeed, (s, v) => s.addictions.SmokingNeed = v, 0f, 100f);
    public static readonly PlayerStatRef GamblingAddiction = Def("gambling_addiction", "addictions", "Gambling addiction, 0 to 100.", s => s.addictions.GamblingAddiction, (s, v) => s.addictions.GamblingAddiction = v, 0f, 100f);
    public static readonly PlayerStatRef GamblingNeed = Def("gambling_need", "addictions", "Gambling craving, 0 to 100.", s => s.addictions.GamblingNeed, (s, v) => s.addictions.GamblingNeed = v, 0f, 100f);

    /// <summary>Every stat, in a stable order (health, needs, conditions, addictions, then the rates).</summary>
    public static IReadOnlyList<PlayerStatRef> All
    {
        get
        {
            PlayerStatRates.EnsureLoaded();
            return Registry;
        }
    }

    /// <summary>Finds a stat by its console name; dashes, spaces and case don't matter. Null when unknown.</summary>
    public static PlayerStatRef Find(string name)
    {
        PlayerStatRates.EnsureLoaded();
        if (string.IsNullOrWhiteSpace(name))
            return null;
        ByName.TryGetValue(name.Trim().Replace('-', '_').Replace(' ', '_'), out var stat);
        return stat;
    }

    /// <summary>Like <see cref="Find"/> but throws an error listing the valid names.</summary>
    public static PlayerStatRef Require(string name) =>
        Find(name) ?? throw new ArgumentException($"Unknown player stat '{name}'. Known: {string.Join(", ", Names())}");

    public static IEnumerable<string> Names()
    {
        foreach (var stat in All)
            yield return stat.Name;
    }

    internal static PlayerStatRef Def(string name, string group, string description, Func<PlayerStats, float> get,
        Action<PlayerStats, float> set, float? min = null, float? max = null)
    {
        var stat = new PlayerStatRef(name, group, description, get, set, min, max);
        Registry.Add(stat);
        ByName[name] = stat;
        return stat;
    }
}
