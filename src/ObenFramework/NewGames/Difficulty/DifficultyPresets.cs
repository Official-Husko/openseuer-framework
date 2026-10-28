// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.NewGames;

/// <summary>
/// The registered presets, applying one to the running game, and remembering which one a game uses.
/// The game itself keeps a preset that changes values as its Custom level (in the save's DifficultyController
/// data), so a save stays playable without the mod; the preset's key is kept beside it in the framework's
/// newgame file only to report <see cref="GameDifficulty.ActivePreset"/>.
/// </summary>
internal static class DifficultyPresets
{
    private static readonly Dictionary<string, DifficultyPreset> Registered = new Dictionary<string, DifficultyPreset>(StringComparer.Ordinal);

    /// <summary>The registered presets sorted by key, so the New Game screen lists them in a stable order.</summary>
    public static DifficultyPreset[] All()
    {
        var all = new DifficultyPreset[Registered.Count];
        Registered.Values.CopyTo(all, 0);
        Array.Sort(all, (a, b) => string.CompareOrdinal(a.Key, b.Key));
        return all;
    }

    public static DifficultyPreset Find(string key) =>
        key != null && Registered.TryGetValue(key, out var preset) ? preset : null;

    public static void Init(ObenMod framework) =>
        framework.Track(Disposable.Create(Registered.Clear));

    public static DifficultyPreset Register(ObenMod owner, DifficultyPreset preset)
    {
        if (Registered.ContainsKey(preset.Key))
        {
            GameEvents.Log.Warn($"The difficulty preset {preset.Key} is registered twice; the first one is kept.");
            return null;
        }
        Registered.Add(preset.Key, preset);
        owner.Track(Disposable.Create(() =>
        {
            if (Registered.TryGetValue(preset.Key, out var current) && current == preset)
                Registered.Remove(preset.Key);
            DifficultySelector.Refresh();
        }));
        DifficultySelector.Refresh();
        return preset;
    }

    /// <summary>The preset the running game uses, if it is registered and the game's difficulty still matches it.</summary>
    public static string Active()
    {
        var preset = Find(NewGameSave.Data?.DifficultyPreset);
        var controller = DifficultyController.instance;
        if (preset == null || controller == null)
            return null;
        return DifficultyInfos.Holds(preset, controller.currentDifficultyInfo) ? preset.Key : null;
    }

    /// <summary>Records the preset a new game was started with (or none).</summary>
    public static void RecordNewGame(DifficultyPreset preset)
    {
        var data = NewGameSave.Data;
        if (data != null)
            data.DifficultyPreset = preset != null ? preset.Key : "";
    }

    /// <summary>Switches the running game to a preset. Refused during a save or load and outside a scene with a difficulty.</summary>
    public static string Apply(string key)
    {
        var preset = Find(key);
        if (preset == null)
            return $"No difficulty preset '{key}' is registered.";
        if (SaveGate.IsBusy)
            return "A save is being written or loaded.";
        var controller = DifficultyController.instance;
        var all = controller != null ? controller.GetDifficulties() : null;
        if (all == null || all.Length == 0)
            return "There is no difficulty here (no DifficultyController).";

        int[] levels = DifficultyInfos.Levels(all);
        var target = preset.ChangesValues ? Difficulty.Custom : preset.BasedOn;
        if (DifficultyPresetMap.IndexOf(levels, (int)target) < 0)
            return $"The game has no {target} level here.";
        var info = DifficultyInfos.Build(preset, all);
        if (info == null)
            return $"The game has no {preset.BasedOn} level here to base the preset on.";

        try
        {
            controller.ChangeDifficulty(target);
            controller.currentDifficultyInfo = info;
        }
        catch (Exception e)
        {
            GameDifficulty.Failed("applying the preset " + key, e);
            return "The game refused the change: " + e.Message;
        }
        if (NewGameSave.Data != null)
            NewGameSave.Data.DifficultyPreset = preset.Key;
        return null;
    }

    /// <summary>Forgets the running game's preset. The difficulty values stay as they are.</summary>
    public static bool Clear()
    {
        var data = NewGameSave.Data;
        if (data == null || string.IsNullOrEmpty(data.DifficultyPreset))
            return false;
        data.DifficultyPreset = "";
        return true;
    }
}
