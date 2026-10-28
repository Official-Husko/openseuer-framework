// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.NewGames;

/// <summary>A difficulty preset a mod registered with <see cref="GameDifficulty.RegisterPreset"/>.</summary>
public sealed class DifficultyPreset
{
    internal DifficultyPreset(string modId, string id, string name, string description, Difficulty basedOn,
        Action<DifficultyPresetSettings> configure)
    {
        ModId = modId;
        Id = id;
        Key = modId + "." + id;
        Name = name;
        Description = description ?? "";
        BasedOn = basedOn;
        Configure = configure;
    }

    public string ModId { get; }

    public string Id { get; }

    /// <summary>"modId.id", the name <see cref="GameDifficulty.SetPreset"/> and the save use.</summary>
    public string Key { get; }

    /// <summary>The name shown on the New Game screen.</summary>
    public string Name { get; }

    public string Description { get; }

    /// <summary>The game level the preset starts from.</summary>
    public Difficulty BasedOn { get; }

    /// <summary>True when the preset changes values; the game then keeps it as a Custom level.</summary>
    public bool ChangesValues => Configure != null;

    internal Action<DifficultyPresetSettings> Configure { get; }
}

/// <summary>
/// The values a preset's configure callback may change, filled with the level it is based on. Multipliers
/// are numbers (1 is normal); the game only knows steps of a quarter from 0 to 4, plus infinite for trader
/// wealth, so each is moved to the nearest step. Prices and renovation speed can't be 0.
/// </summary>
public sealed class DifficultyPresetSettings
{
    internal DifficultyPresetSettings(DifficultySettings basis)
    {
        UserSavesDisabled = basis.UserSavesDisabled;
        MentalHealthDisabled = basis.MentalHealthDisabled;
        MandatoryStartingAddictionsDisabled = basis.MandatoryStartingAddictionsDisabled;
        NeedsSpeed = basis.NeedsSpeed;
        AddictionNeedsSpeed = basis.AddictionNeedsSpeed;
        AddictionRecoverySpeed = basis.AddictionRecoverySpeed;
        RenovationCost = basis.RenovationCost;
        RenovationResourceCosts = basis.RenovationResourceCosts;
        RenovationSpeed = basis.RenovationSpeed;
        RentIncome = basis.RentIncome;
        FurnitureCraftingSpeed = basis.FurnitureCraftingSpeed;
        ItemCraftingSpeed = basis.ItemCraftingSpeed;
        Prices = basis.Prices;
        RespawningTime = basis.RespawningTime;
        SmvProgressionRate = basis.SmvProgressionRate;
        TraderWealth = basis.TraderWealth;
        StackSize = basis.StackSize;
    }

    public bool UserSavesDisabled { get; set; }

    public bool MentalHealthDisabled { get; set; }

    public bool MandatoryStartingAddictionsDisabled { get; set; }

    public float NeedsSpeed { get; set; }

    public float AddictionNeedsSpeed { get; set; }

    public float AddictionRecoverySpeed { get; set; }

    public float RenovationCost { get; set; }

    public float RenovationResourceCosts { get; set; }

    public float RenovationSpeed { get; set; }

    public float RentIncome { get; set; }

    /// <summary>0 means instant crafting.</summary>
    public float FurnitureCraftingSpeed { get; set; }

    /// <summary>0 means instant crafting.</summary>
    public float ItemCraftingSpeed { get; set; }

    public float Prices { get; set; }

    public float RespawningTime { get; set; }

    public float SmvProgressionRate { get; set; }

    /// <summary><c>float.PositiveInfinity</c> gives traders unlimited money.</summary>
    public float TraderWealth { get; set; }

    public float StackSize { get; set; }
}
