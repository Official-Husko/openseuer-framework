// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader;

namespace ObenFramework.NewGames;

public static partial class GameDifficulty
{
    /// <summary>
    /// Adds a difficulty preset to the New Game screen's selector, after the game's own levels. It starts
    /// from <paramref name="basedOn"/>, which must be a level the game offers (0.5.13 has Default, Casual and
    /// Custom); <paramref name="configure"/> changes its values, and a preset with values becomes a Custom
    /// level, which is how the game saves it. Without <paramref name="configure"/> the preset is the base
    /// level under another name. Returns null when the mod already registered this id. The preset leaves the
    /// selector when the mod unloads; games started with it keep its values.
    /// </summary>
    public static DifficultyPreset RegisterPreset(ObenMod owner, string id, string name, string description, Difficulty basedOn,
        Action<DifficultyPresetSettings> configure = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (!OptionIds.IsValidKey(id))
            throw new ArgumentException($"'{id}' is not a usable preset id (1 to 64 letters, digits, '_' or '-').", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A preset needs a name.", nameof(name));
        if (basedOn == Difficulty.Custom || !Enum.IsDefined(typeof(Difficulty), basedOn))
            throw new ArgumentOutOfRangeException(nameof(basedOn), "A preset is based on one of the game's levels other than Custom.");
        return DifficultyPresets.Register(owner, new DifficultyPreset(owner.Info.Id, id, name, description, basedOn, configure));
    }

    /// <summary>
    /// Whether mod presets are listed in the New Game screen's difficulty selector; on by default. ObenUIKit
    /// follows its "Show mod difficulty presets" setting with it. Turning it off while the screen is up takes
    /// them out at once (a selected preset falls back to the game's Custom level); games started with a
    /// preset keep it either way.
    /// </summary>
    public static bool PresetsOnNewGameScreen
    {
        get => DifficultySelector.Listed;
        set
        {
            if (DifficultySelector.Listed == value)
                return;
            DifficultySelector.Listed = value;
            DifficultySelector.Refresh();
        }
    }

    /// <summary>The registered presets, sorted by key.</summary>
    public static List<DifficultyPreset> Presets() => new List<DifficultyPreset>(DifficultyPresets.All());

    /// <summary>
    /// The key ("modId.id") of the preset the running game uses, or null: none was chosen, its mod isn't
    /// loaded, or the difficulty was changed since.
    /// </summary>
    public static string ActivePreset => DifficultyPresets.Active();

    /// <summary>
    /// Switches the running game to a preset now. Returns null when it worked, otherwise why not (unknown
    /// preset, a save in progress, no difficulty in this scene). The change is saved with the game.
    /// </summary>
    public static string SetPreset(string key) => DifficultyPresets.Apply(key);

    /// <summary>Forgets which preset the running game uses; the difficulty values stay. False when none was recorded.</summary>
    public static bool ClearPreset() => DifficultyPresets.Clear();

    /// <summary>
    /// Switches the running game to one of the game's own levels by its name ("Default", "Casual"), like the
    /// console's <c>set_difficulty</c>. False when this scene has no such level.
    /// </summary>
    public static bool SetLevel(string name)
    {
        var controller = DifficultyController.instance;
        if (controller == null || string.IsNullOrEmpty(name) || Saves.SaveGate.IsBusy)
            return false;
        if (!controller.ChangeDifficultyByName(name))
            return false;
        DifficultyPresets.Clear();
        return true;
    }
}
