using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.NewGames;

namespace DevBridge.Commands;

/// <summary>The game's difficulty, mod factors and mod difficulty presets.</summary>
internal static class DifficultyCommands
{
    [BridgeCommand("difficulty.get", "The current difficulty with its multipliers, the levels this scene offers, mod factors and the active preset.")]
    private static object Get(JObject p)
    {
        var levels = new JArray();
        foreach (var level in GameDifficulty.Levels())
            levels.Add(new JObject { ["name"] = level.Name, ["level"] = level.Level.ToString() });

        var factors = new JArray();
        foreach (var entry in GameDifficulty.FactorList())
        {
            factors.Add(new JObject
            {
                ["owner"] = entry.OwnerId,
                ["multiplier"] = ((DifficultyMultiplier)entry.Channel).ToString(),
                ["key"] = entry.Key,
                ["value"] = entry.Value,
            });
        }

        var current = GameDifficulty.Current();
        return new JObject
        {
            ["current"] = current == null ? null : Describe(current),
            ["levels"] = levels,
            ["factors"] = factors,
            ["activePreset"] = GameDifficulty.ActivePreset,
        };
    }

    [BridgeCommand("difficulty.set", "Change the running game's difficulty: a bridge-owned factor on one multiplier, a mod preset, or one of the game's levels by name.",
        Params = new[]
        {
            "factor: multiplier name (NeedsSpeed, Prices, RenovationSpeed, ...) with 'value' 0 to 10",
            "value: the factor, or omit with factor to remove the bridge's factor",
            "preset: a preset key 'modId.id'",
            "level: a game level name such as Default or Casual",
            "clear: true to forget the recorded preset",
        })]
    private static object Set(JObject p)
    {
        string factor = p.Str("factor", null);
        string preset = p.Str("preset", null);
        string level = p.Str("level", null);

        if (factor != null)
        {
            if (!Enum.TryParse(factor, true, out DifficultyMultiplier multiplier) || !Enum.IsDefined(typeof(DifficultyMultiplier), multiplier))
                throw new ArgumentException($"'{factor}' is not a multiplier; use {string.Join(", ", Enum.GetNames(typeof(DifficultyMultiplier)))}.");
            if (p["value"] == null)
                GameDifficulty.RemoveFactor(DevBridgeMod.Instance, multiplier);
            else
                GameDifficulty.SetFactor(DevBridgeMod.Instance, multiplier, p.Float("value"));
        }
        else if (preset != null)
        {
            string refused = GameDifficulty.SetPreset(preset);
            if (refused != null)
                throw new InvalidOperationException(refused);
        }
        else if (level != null)
        {
            if (!GameDifficulty.SetLevel(level))
                throw new InvalidOperationException($"No level '{level}' here, or a save is running.");
        }
        else if (p.Bool("clear", false))
        {
            GameDifficulty.ClearPreset();
        }
        else
        {
            throw new ArgumentException("Pass 'factor', 'preset', 'level' or 'clear'.");
        }
        return Get(p);
    }

    [BridgeCommand("difficulty.presets", "The difficulty presets mods registered. With 'add', registers a DevBridge-owned test preset first.",
        Params = new[]
        {
            "add: optional preset id to register for DevBridge",
            "name: its name on the New Game screen, default the id",
            "basedOn: a game level, default Default",
            "needs: optional needs speed factor for the preset (makes it a Custom level)",
        })]
    private static object Presets(JObject p)
    {
        string add = p.Str("add", null);
        if (add != null)
        {
            if (!Enum.TryParse(p.Str("basedOn", "Default"), true, out Difficulty basedOn))
                throw new ArgumentException("'basedOn' is not a game level.");
            float? needs = p["needs"] == null ? (float?)null : p.Float("needs");
            Action<DifficultyPresetSettings> configure = null;
            if (needs != null)
                configure = s => s.NeedsSpeed = needs.Value;
            if (GameDifficulty.RegisterPreset(DevBridgeMod.Instance, add, p.Str("name", add), "A test preset from the dev bridge.", basedOn, configure) == null)
                throw new InvalidOperationException($"DevBridge already registered '{add}'.");
        }

        var presets = new JArray();
        foreach (var preset in GameDifficulty.Presets())
        {
            presets.Add(new JObject
            {
                ["key"] = preset.Key,
                ["name"] = preset.Name,
                ["description"] = preset.Description,
                ["basedOn"] = preset.BasedOn.ToString(),
                ["changesValues"] = preset.ChangesValues,
            });
        }
        return new JObject { ["presets"] = presets, ["active"] = GameDifficulty.ActivePreset };
    }

    internal static JObject Describe(DifficultySettings s) => new JObject
    {
        ["name"] = s.Name,
        ["level"] = s.Level.ToString(),
        ["userSavesDisabled"] = s.UserSavesDisabled,
        ["autosaveMode"] = s.AutosaveMode,
        ["mentalHealthDisabled"] = s.MentalHealthDisabled,
        ["startingAddictionsDisabled"] = s.MandatoryStartingAddictionsDisabled,
        ["rentPayments"] = s.RentPayments,
        ["needsSpeed"] = s.NeedsSpeed,
        ["addictionNeedsSpeed"] = s.AddictionNeedsSpeed,
        ["addictionRecoverySpeed"] = s.AddictionRecoverySpeed,
        ["renovationCost"] = s.RenovationCost,
        ["renovationResourceCosts"] = s.RenovationResourceCosts,
        ["renovationSpeed"] = s.RenovationSpeed,
        ["rentIncome"] = s.RentIncome,
        ["furnitureCraftingSpeed"] = s.FurnitureCraftingSpeed,
        ["itemCraftingSpeed"] = s.ItemCraftingSpeed,
        ["prices"] = s.Prices,
        ["respawningTime"] = s.RespawningTime,
        ["smvProgressionRate"] = s.SmvProgressionRate,
        ["traderWealth"] = float.IsInfinity(s.TraderWealth) ? (JToken)"infinite" : s.TraderWealth,
        ["stackSize"] = s.StackSize,
    };
}
