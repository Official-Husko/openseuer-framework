// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.NewGames;

/// <summary>
/// Lists the mod presets after the game's own levels in the New Game screen's difficulty selector, and
/// remembers which one the player is on. Only the main menu's controller is extended: levels have their
/// own controller with the game's list, so a saved level index never points at a mod preset. The game's
/// list is put back when the framework unloads.
/// </summary>
internal static class DifficultySelector
{
    private static readonly AccessTools.FieldRef<DifficultyController, DifficultyInfo[]> List =
        AccessTools.FieldRefAccess<DifficultyController, DifficultyInfo[]>("difficulties");
    private static readonly AccessTools.FieldRef<DifficultyController, int> Index =
        AccessTools.FieldRefAccess<DifficultyController, int>("currentDifficultyIndex");

    private static DifficultyController _controller;
    private static DifficultyInfo[] _original;
    private static DifficultyPreset[] _listed = Array.Empty<DifficultyPreset>();
    private static DifficultyPreset _chosen;

    /// <summary>Whether the presets are listed at all (<see cref="GameDifficulty.PresetsOnNewGameScreen"/>).</summary>
    public static bool Listed = true;

    public static void Init(ObenMod framework) => framework.Track(Disposable.Create(Restore));

    /// <summary>The preset picked on the New Game screen, if the selected level is still that preset.</summary>
    public static DifficultyPreset Chosen
    {
        get
        {
            var controller = DifficultyController.instance;
            if (_chosen == null || controller == null || controller != _controller)
                return null;
            return DifficultyInfos.Holds(_chosen, controller.currentDifficultyInfo) ? _chosen : null;
        }
    }

    /// <summary>Called when the New Game screen's difficulty page shows: adds the presets to its controller once.</summary>
    public static void Extend()
    {
        var controller = DifficultyController.instance;
        if (controller == null || controller == _controller)
            return;
        Restore();
        var current = List(controller);
        if (current == null || current.Length == 0)
            return;
        _controller = controller;
        _original = current;
        Build();
    }

    /// <summary>Rebuilds the listed presets after a mod registered or removed one.</summary>
    public static void Refresh()
    {
        if (_controller != null && _original != null)
            Build();
    }

    /// <summary>Called after the player moved the selector: notes whether a mod preset is selected.</summary>
    public static void Moved(DifficultyController controller)
    {
        if (controller != _controller || _original == null)
            return;
        int index = Index(controller);
        _chosen = DifficultyPresetMap.IsListed(index, _original.Length, _listed.Length) ? _listed[index - _original.Length] : null;
    }

    private static void Build()
    {
        var entries = new List<DifficultyInfo>(_original);
        var listed = new List<DifficultyPreset>();
        foreach (var preset in Listed ? DifficultyPresets.All() : Array.Empty<DifficultyPreset>())
        {
            DifficultyInfo info = null;
            try { info = DifficultyInfos.Build(preset, _original); }
            catch (Exception e) { GameDifficulty.Failed("building the preset " + preset.Key, e); }
            if (info == null)
                continue;
            entries.Add(info);
            listed.Add(preset);
        }
        _listed = listed.ToArray();
        List(_controller) = entries.ToArray();
        KeepIndexInside(_controller, entries.Count);
        if (_chosen != null && Array.IndexOf(_listed, _chosen) < 0)
            _chosen = null;
    }

    private static void Restore()
    {
        if (_controller != null && _original != null)
        {
            try
            {
                List(_controller) = _original;
                KeepIndexInside(_controller, _original.Length);
            }
            catch (Exception e)
            {
                GameDifficulty.Failed("putting back the game's difficulty list", e);
            }
        }
        _controller = null;
        _original = null;
        _listed = Array.Empty<DifficultyPreset>();
        _chosen = null;
    }

    // An index past the end (the selected preset went away) falls back to the game's Custom level, else the first one.
    private static void KeepIndexInside(DifficultyController controller, int count)
    {
        int index = Index(controller);
        if (index >= 0 && index < count)
            return;
        var all = List(controller);
        int custom = DifficultyPresetMap.IndexOf(DifficultyInfos.Levels(all), (int)Difficulty.Custom);
        controller.ChangeDifficulty(all[DifficultyPresetMap.Clamp(index, all.Length, custom)].difficulty);
        _chosen = null;
    }
}
