namespace ObenFramework.Player;

/// <summary>
/// The tuning side of <see cref="PlayerStat"/>: how fast needs grow, how alcohol and mushrooms
/// are absorbed, how addictions move. Named like the console's <c>player_stats</c> entries and
/// listed in <see cref="PlayerStat.All"/> after the stats themselves. Rates are per in-game hour
/// or minute as the game defines them; changing one lasts until a save with the old value loads.
/// </summary>
public static class PlayerStatRates
{
    // An explicit static constructor makes EnsureLoaded() run the initializers below right away.
    static PlayerStatRates() { }

    public static readonly PlayerStatRef HungerRate = Rate("hunger_rate", "Hunger gained per in-game hour.", s => s.hungerAndThirst.HungerRate, (s, v) => s.hungerAndThirst.HungerRate = v);
    public static readonly PlayerStatRef HungerRateBowelFactor = Rate("hunger_rate_bowel_factor", "How much of eaten food reaches the bowel.", s => s.hungerAndThirst.HungeRateBowelFactor, (s, v) => s.hungerAndThirst.HungeRateBowelFactor = v);
    public static readonly PlayerStatRef ThirstRate = Rate("thirst_rate", "Thirst gained per in-game hour.", s => s.hungerAndThirst.ThirstRate, (s, v) => s.hungerAndThirst.ThirstRate = v);
    public static readonly PlayerStatRef ThirstRateBladderFactor = Rate("thirst_rate_bladder_factor", "How much of what is drunk reaches the bladder.", s => s.hungerAndThirst.ThirstRateBladderFactor, (s, v) => s.hungerAndThirst.ThirstRateBladderFactor = v);
    public static readonly PlayerStatRef ThirstStarvationDamage = Rate("thirst_starvation_damage", "Damage per in-game hour at full hunger or thirst.", s => s.hungerAndThirst.ThristStarvationDamage, (s, v) => s.hungerAndThirst.ThristStarvationDamage = v);
    public static readonly PlayerStatRef SleepHungerThirstRateFactor = Rate("sleep_hunger_thirst_rate_factor", "Hunger and thirst rate multiplier while asleep.", s => s.hungerAndThirst.SleepHungerThristRateFactor, (s, v) => s.hungerAndThirst.SleepHungerThristRateFactor = v);
    public static readonly PlayerStatRef RestRate = Rate("rest_rate", "Tiredness lost per in-game hour while resting.", s => s.tiredness.RestRate, (s, v) => s.tiredness.RestRate = v);
    public static readonly PlayerStatRef SleepTimeSpeed = Rate("sleep_time_speed", "Clock speed while sleeping.", s => s.tiredness.SleepTimeSpeed, (s, v) => s.tiredness.SleepTimeSpeed = v);
    public static readonly PlayerStatRef HealthRegenerationRate = Rate("health_regeneration_rate", "Health regained per in-game minute while healing.", s => s.health.HealthRegenerationRate, (s, v) => s.health.HealthRegenerationRate = v);
    public static readonly PlayerStatRef DepressionChangeRate = Rate("depression_change_rate", "How fast depression moves toward its target.", s => s.mentalHealth.mentalHealthChangeRate, (s, v) => s.mentalHealth.mentalHealthChangeRate = v);
    public static readonly PlayerStatRef OverheatCooldown = Rate("overheat_cooldown", "Overheat lost per tick outside the sauna.", s => s.sauna.OverheatCooldown, (s, v) => s.sauna.OverheatCooldown = v);
    public static readonly PlayerStatRef AlcoholAbsorptionRate = Rate("alcohol_absorption_rate", "Alcohol moved from stomach to blood per in-game minute.", s => s.alcohol.AlcoholAbsorbionRate, (s, v) => s.alcohol.AlcoholAbsorbionRate = v);
    public static readonly PlayerStatRef AlcoholBurnRate = Rate("alcohol_burn_rate", "Blood alcohol burned per in-game minute.", s => s.alcohol.AlcoholBurnRate, (s, v) => s.alcohol.AlcoholBurnRate = v);
    public static readonly PlayerStatRef MushroomAbsorptionRate = Rate("mushroom_absorption_rate", "How fast eaten mushrooms take effect.", s => s.mushrooms.MushroomAbsorbionRate, (s, v) => s.mushrooms.MushroomAbsorbionRate = v);
    public static readonly PlayerStatRef MushroomDecreaseRate = Rate("mushroom_decrease_rate", "How fast the mushroom high fades.", s => s.mushrooms.MushroomDecreaseRate, (s, v) => s.mushrooms.MushroomDecreaseRate = v);
    public static readonly PlayerStatRef SmvProgressionRate = Rate("smv_progression_rate", "How fast the SMV infection advances.", s => s.mushrooms.SMVProgressionRate, (s, v) => s.mushrooms.SMVProgressionRate = v);
    public static readonly PlayerStatRef AlcoholAddictionChange = Rate("alcohol_addiction_change", "Alcohol addiction drift per tick.", s => s.addictions.AlcoholAddictionChange, (s, v) => s.addictions.AlcoholAddictionChange = v);
    public static readonly PlayerStatRef AlcoholNeedBaseRate = Rate("alcohol_need_base_rate", "Base speed of the alcohol craving.", s => s.addictions.AlcoholNeedBaserate, (s, v) => s.addictions.AlcoholNeedBaserate = v);
    public static readonly PlayerStatRef MushroomAddictionChange = Rate("mushroom_addiction_change", "Mushroom addiction drift per tick.", s => s.addictions.MushroomAddictionChange, (s, v) => s.addictions.MushroomAddictionChange = v);
    public static readonly PlayerStatRef MushroomNeedBaseRate = Rate("mushroom_need_base_rate", "Base speed of the mushroom craving.", s => s.addictions.MushroomNeedBaserate, (s, v) => s.addictions.MushroomNeedBaserate = v);
    public static readonly PlayerStatRef SmokingAddictionChange = Rate("smoking_addiction_change", "Smoking addiction drift per tick.", s => s.addictions.SmokingAddictionChange, (s, v) => s.addictions.SmokingAddictionChange = v);
    public static readonly PlayerStatRef SmokingNeedBaseRate = Rate("smoking_need_base_rate", "Base speed of the smoking craving.", s => s.addictions.SmokingNeedBaserate, (s, v) => s.addictions.SmokingNeedBaserate = v);
    public static readonly PlayerStatRef WithdrawalSymptomDamage = Rate("withdrawal_symptom_dmg", "Mental health lost to withdrawal.", s => s.addictions.WithdrawalsymptomMentalhealthLoss, (s, v) => s.addictions.WithdrawalsymptomMentalhealthLoss = v);
    public static readonly PlayerStatRef GamblingSatisfaction = Rate("gambling_satisfaction", "Satisfaction gained per gambling win.", s => s.gambling.GamblingSatisfactionByOCWon, (s, v) => s.gambling.GamblingSatisfactionByOCWon = v);

    /// <summary>Forces the rates to register. Called by the lookups in <see cref="PlayerStat"/>.</summary>
    internal static void EnsureLoaded() { }

    private static PlayerStatRef Rate(string name, string description, System.Func<PlayerStats, float> get,
        System.Action<PlayerStats, float> set) => PlayerStat.Def(name, "rates", description, get, set);
}
