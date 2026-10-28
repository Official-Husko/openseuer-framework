// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.NewGames;

/// <summary>Builds the game's <c>DifficultyInfo</c> objects for presets: copies, levels and the value steps.</summary>
internal static class DifficultyInfos
{
    private static readonly DefaultMultiplier[] StepLevels;
    private static readonly float[] Steps;

    static DifficultyInfos()
    {
        // Every finite step the game knows, sorted by value (Default is 1 and sits between ThreeQuarters and OneAndQuarter).
        var all = (DefaultMultiplier[])Enum.GetValues(typeof(DefaultMultiplier));
        var finite = Array.FindAll(all, m => m != DefaultMultiplier.Infinite);
        var values = Array.ConvertAll(finite, DifficultyInfo.GetMultiplierValue);
        int[] order = DifficultyPresetMap.OrderByValue(values);
        StepLevels = new DefaultMultiplier[order.Length];
        Steps = new float[order.Length];
        for (int i = 0; i < order.Length; i++)
        {
            StepLevels[i] = finite[order[i]];
            Steps[i] = values[order[i]];
        }
    }

    /// <summary>The level of each entry, -1 for a missing one.</summary>
    public static int[] Levels(DifficultyInfo[] all)
    {
        if (all == null)
            return Array.Empty<int>();
        int[] levels = new int[all.Length];
        for (int i = 0; i < all.Length; i++)
            levels[i] = all[i] == null ? -1 : (int)all[i].difficulty;
        return levels;
    }

    /// <summary>The info a preset stands for, built from its base level in <paramref name="all"/>; null when the game has no such level.</summary>
    public static DifficultyInfo Build(DifficultyPreset preset, DifficultyInfo[] all)
    {
        int basedOn = DifficultyPresetMap.IndexOf(Levels(all), (int)preset.BasedOn);
        if (basedOn < 0)
            return null;
        var info = Copy(all[basedOn]);
        if (preset.Configure != null)
        {
            var settings = new DifficultyPresetSettings(new DifficultySettings(all[basedOn]));
            preset.Configure(settings);
            Apply(info, settings);
            info.difficulty = Difficulty.Custom;
        }
        info.difficultyName = preset.Name;
        info.description = preset.Description;
        return info;
    }

    /// <summary>
    /// True when <paramref name="info"/> is still what the preset made. A preset with its own values is a Custom
    /// level carrying the preset's name (the game renames it "Custom" when the player edits a value); one
    /// without is just its base level, whose name the game puts back after a load.
    /// </summary>
    public static bool Holds(DifficultyPreset preset, DifficultyInfo info)
    {
        if (info == null)
            return false;
        return preset.Configure != null
            ? info.difficulty == Difficulty.Custom && info.difficultyName == preset.Name
            : info.difficulty == preset.BasedOn;
    }

    public static DifficultyInfo Copy(DifficultyInfo source) => new DifficultyInfo
    {
        difficultyName = source.difficultyName,
        difficulty = source.difficulty,
        description = source.description,
        userSavesDisabled = source.userSavesDisabled,
        autosaveMode = source.autosaveMode,
        mandatoryStartingAddictionsDisabled = source.mandatoryStartingAddictionsDisabled,
        generalNeedsSpeed = source.generalNeedsSpeed,
        addictionNeedsSpeed = source.addictionNeedsSpeed,
        addictionRecoverySpeed = source.addictionRecoverySpeed,
        mentalHealthDisabled = source.mentalHealthDisabled,
        renovationCost = source.renovationCost,
        renovationResourceCosts = source.renovationResourceCosts,
        renovationSpeed = source.renovationSpeed,
        rentIncome = source.rentIncome,
        rentPayments = source.rentPayments,
        furnitureCraftingSpeed = source.furnitureCraftingSpeed,
        itemCraftingSpeed = source.itemCraftingSpeed,
        prices = source.prices,
        respawningTime = source.respawningTime,
        smvProgressionRateSpeed = source.smvProgressionRateSpeed,
        traderWealth = source.traderWealth,
        stackSize = source.stackSize,
    };

    private static void Apply(DifficultyInfo info, DifficultyPresetSettings settings)
    {
        info.userSavesDisabled = settings.UserSavesDisabled;
        info.mentalHealthDisabled = settings.MentalHealthDisabled;
        info.mandatoryStartingAddictionsDisabled = settings.MandatoryStartingAddictionsDisabled;
        info.generalNeedsSpeed = Step(settings.NeedsSpeed, info.generalNeedsSpeed);
        info.addictionNeedsSpeed = Step(settings.AddictionNeedsSpeed, info.addictionNeedsSpeed);
        info.addictionRecoverySpeed = Step(settings.AddictionRecoverySpeed, info.addictionRecoverySpeed);
        info.renovationCost = Step(settings.RenovationCost, info.renovationCost);
        info.renovationResourceCosts = Step(settings.RenovationResourceCosts, info.renovationResourceCosts);
        info.renovationSpeed = Positive(settings.RenovationSpeed, info.renovationSpeed);
        info.rentIncome = Step(settings.RentIncome, info.rentIncome);
        info.furnitureCraftingSpeed = Step(settings.FurnitureCraftingSpeed, info.furnitureCraftingSpeed);
        info.itemCraftingSpeed = Step(settings.ItemCraftingSpeed, info.itemCraftingSpeed);
        info.prices = Positive(settings.Prices, info.prices);
        info.respawningTime = Step(settings.RespawningTime, info.respawningTime);
        info.smvProgressionRateSpeed = Step(settings.SmvProgressionRate, info.smvProgressionRateSpeed);
        info.traderWealth = float.IsPositiveInfinity(settings.TraderWealth) ? DefaultMultiplier.Infinite : Step(settings.TraderWealth, info.traderWealth);
        info.stackSize = Step(settings.StackSize, info.stackSize);
    }

    private static DefaultMultiplier Step(float value, DefaultMultiplier fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;
        int index = DifficultyPresetMap.Nearest(value, Steps);
        return index < 0 ? fallback : StepLevels[index];
    }

    private static DefaultMultiplier Positive(float value, DefaultMultiplier fallback)
    {
        var step = Step(value, fallback);
        return DifficultyInfo.GetMultiplierValue(step) > 0f ? step : fallback;
    }
}
