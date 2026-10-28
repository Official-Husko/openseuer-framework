// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.NewGames;

/// <summary>
/// The game's multipliers that mods can scale with <see cref="GameDifficulty.SetFactor"/>. Each is one of
/// the getters on the game's <c>DifficultyInfo</c>.
/// </summary>
public enum DifficultyMultiplier
{
    NeedsSpeed,
    AddictionNeedsSpeed,
    AddictionRecoverySpeed,
    RenovationCost,
    RenovationResourceCosts,
    RenovationSpeed,
    RentIncome,
    Prices,
    RespawningTime,
    SmvProgressionRate,
    TraderWealth,
}

/// <summary>A copy of a difficulty level's settings, with each multiplier as a number (1 is normal).</summary>
public sealed class DifficultySettings
{
    internal DifficultySettings(DifficultyInfo info)
    {
        Name = info.difficultyName;
        Level = info.difficulty;
        Description = info.description;
        UserSavesDisabled = info.userSavesDisabled;
        AutosaveMode = info.autosaveMode.ToString();
        MentalHealthDisabled = info.mentalHealthDisabled;
        MandatoryStartingAddictionsDisabled = info.mandatoryStartingAddictionsDisabled;
        RentPayments = info.rentPayments.ToString();
        NeedsSpeed = Value(info.generalNeedsSpeed);
        AddictionNeedsSpeed = Value(info.addictionNeedsSpeed);
        AddictionRecoverySpeed = Value(info.addictionRecoverySpeed);
        RenovationCost = Value(info.renovationCost);
        RenovationResourceCosts = Value(info.renovationResourceCosts);
        RenovationSpeed = Value(info.renovationSpeed);
        RentIncome = Value(info.rentIncome);
        FurnitureCraftingSpeed = Value(info.furnitureCraftingSpeed);
        ItemCraftingSpeed = Value(info.itemCraftingSpeed);
        Prices = Value(info.prices);
        RespawningTime = Value(info.respawningTime);
        SmvProgressionRate = Value(info.smvProgressionRateSpeed);
        TraderWealth = Value(info.traderWealth);
        StackSize = Value(info.stackSize);
    }

    /// <summary>The name shown on the New Game screen ("Default", "Casual", "Custom", or a mod preset's name).</summary>
    public string Name { get; }

    /// <summary>The game's level. Mod presets that change values are <c>Custom</c>.</summary>
    public Difficulty Level { get; }

    public string Description { get; }

    public bool UserSavesDisabled { get; }

    public string AutosaveMode { get; }

    public bool MentalHealthDisabled { get; }

    public bool MandatoryStartingAddictionsDisabled { get; }

    public string RentPayments { get; }

    public float NeedsSpeed { get; }

    public float AddictionNeedsSpeed { get; }

    public float AddictionRecoverySpeed { get; }

    public float RenovationCost { get; }

    public float RenovationResourceCosts { get; }

    public float RenovationSpeed { get; }

    public float RentIncome { get; }

    public float FurnitureCraftingSpeed { get; }

    public float ItemCraftingSpeed { get; }

    public float Prices { get; }

    public float RespawningTime { get; }

    public float SmvProgressionRate { get; }

    /// <summary>Infinity when traders have unlimited money.</summary>
    public float TraderWealth { get; }

    /// <summary>Only used by the game on the Custom level.</summary>
    public float StackSize { get; }

    private static float Value(DefaultMultiplier multiplier) => DifficultyInfo.GetMultiplierValue(multiplier);
}
