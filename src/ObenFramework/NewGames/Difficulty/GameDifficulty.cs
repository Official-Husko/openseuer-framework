// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Player;
using ObenLoader;

namespace ObenFramework.NewGames;

/// <summary>
/// The game's difficulty: reading the level and its settings, scaling its multipliers from a mod, and
/// difficulty presets that mods add to the New Game screen (see the Presets part). The game keeps the
/// difficulty in a <c>DifficultyController</c> per scene and in the save.
/// </summary>
public static partial class GameDifficulty
{
    /// <summary>The largest factor one mod can set on a multiplier.</summary>
    public const float MaxFactor = 10f;

    private static readonly int Channels = Enum.GetValues(typeof(DifficultyMultiplier)).Length;

    internal static readonly MultiplierTable Factors = new MultiplierTable(Channels, MaxFactor);

    private static readonly OwnerWatch Owners = new OwnerWatch(id =>
    {
        if (Factors.RemoveOwner(id) > 0)
            RenovationCountdownPatch.Reset();
    });

    /// <summary>The difficulty of the running game (or the one selected on the New Game screen), or null.</summary>
    public static DifficultySettings Current()
    {
        var controller = DifficultyController.instance;
        var info = controller != null ? controller.currentDifficultyInfo : null;
        return info != null ? new DifficultySettings(info) : null;
    }

    /// <summary>The levels the game offers in this scene, in the New Game screen's order (mod presets included there).</summary>
    public static List<DifficultySettings> Levels()
    {
        var levels = new List<DifficultySettings>();
        var controller = DifficultyController.instance;
        var all = controller != null ? controller.GetDifficulties() : null;
        if (all == null)
            return levels;
        foreach (var info in all)
        {
            if (info != null)
                levels.Add(new DifficultySettings(info));
        }
        return levels;
    }

    /// <summary>
    /// Scales one of the game's difficulty multipliers by <paramref name="factor"/> (0 to <see cref="MaxFactor"/>)
    /// on top of the level's own value. Factors from several mods multiply. Prices and renovation speed must stay
    /// above 0. The factor goes when the mod unloads or calls <see cref="RemoveFactor"/>; it is not saved, so set
    /// it again each session (for example from <see cref="Sessions.WhenStarted"/>).
    /// </summary>
    public static void SetFactor(ObenMod owner, DifficultyMultiplier multiplier, float factor, string key = null)
    {
        if (!Enum.IsDefined(typeof(DifficultyMultiplier), multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        if (float.IsNaN(factor) || factor < 0f || factor > MaxFactor)
            throw new ArgumentOutOfRangeException(nameof(factor), $"A factor must be from 0 to {MaxFactor}.");
        if (factor == 0f && (multiplier == DifficultyMultiplier.Prices || multiplier == DifficultyMultiplier.RenovationSpeed))
            throw new ArgumentOutOfRangeException(nameof(factor), "Prices and renovation speed can't be scaled to 0.");

        string id = Owners.Watch(owner);
        if (Factors.Set(id, (int)multiplier, key, factor) && multiplier == DifficultyMultiplier.RenovationSpeed)
            RenovationCountdownPatch.Reset();
    }

    /// <summary>Takes back a factor this mod set. False when it had none there.</summary>
    public static bool RemoveFactor(ObenMod owner, DifficultyMultiplier multiplier, string key = null) =>
        owner != null && Factors.Remove(owner.Info.Id, (int)multiplier, key);

    /// <summary>Takes back every factor this mod set. Returns how many went.</summary>
    public static int ClearFactors(ObenMod owner) => owner == null ? 0 : Factors.RemoveOwner(owner.Info.Id);

    /// <summary>All factors on a multiplier multiplied together; 1 when none.</summary>
    public static float FactorOf(DifficultyMultiplier multiplier) => Factors.Product((int)multiplier);

    /// <summary>Every factor mods have set, for tools.</summary>
    public static MultiplierEntry[] FactorList() => Factors.Entries;

    internal static void Init(ObenMod framework)
    {
        framework.Track(ObenLoader.Runtime.Disposable.Create(() =>
        {
            Owners.Clear();
            Factors.Clear();
        }));
        DifficultyPresets.Init(framework);
        DifficultySelector.Init(framework);
    }

    internal static float Scaled(DifficultyMultiplier multiplier, float value)
    {
        if (Factors.IsEmpty)
            return value;
        float factor = Factors.Product((int)multiplier);
        // The game divides by renovation speed (a higher speed is a shorter time), so scale it the same way.
        return multiplier == DifficultyMultiplier.RenovationSpeed ? value / factor : value * factor;
    }

    internal static int Scaled(DifficultyMultiplier multiplier, int value, int minimum)
    {
        if (Factors.IsEmpty)
            return value;
        float scaled = value * Factors.Product((int)multiplier);
        int result = scaled >= int.MaxValue ? int.MaxValue : (int)Math.Round(scaled);
        return value >= minimum && result < minimum ? minimum : result;
    }

    internal static void Failed(string where, Exception e) => GameEvents.Log.Error("Difficulty: " + where + " failed", e);
}
