// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using System.Collections.Generic;

namespace ObenFramework.Saves;

/// <summary>How a component handled an entry offered to its load phases.</summary>
internal enum ApplyOutcome
{
    /// <summary>The component took the entry and no phase threw.</summary>
    Applied,
    /// <summary>The component did not ask for the entry (wrong GUID, no load phase), so nothing changed.</summary>
    NotTaken,
    /// <summary>A load phase threw, so the object may be half applied.</summary>
    Threw,
}

/// <summary>A component to run save phases on, and which phases to run.</summary>
internal readonly struct SaveTarget
{
    public readonly SavableScript Script;
    public readonly SavePhases Phases;

    public SaveTarget(SavableScript script, SavePhases phases)
    {
        Script = script;
        Phases = phases;
    }
}

/// <summary>
/// Runs the game's own save and load callbacks on chosen components against private lists, so
/// the objects write and read their state exactly as in a real save, without a file being touched
/// and without disturbing a save or load of the game itself.
/// </summary>
internal static class SaveRunner
{
    // The game's lists are process-wide statics; a capture started from inside another one's
    // callback would swap them under it.
    private static bool _running;

    /// <summary>
    /// Runs the save phases (all targets per phase, like the game) and returns the entries they
    /// wrote, split into global and level ones. Null if another run is active or the game's members
    /// were not found.
    /// </summary>
    public static (IList Global, IList Level)? Capture(List<SaveTarget> targets)
    {
        if (_running || !GameSaveLists.Ready)
            return null;

        _running = true;
        var global = GameSaveLists.NewList();
        var level = GameSaveLists.NewList();
        try
        {
            using (new GameSaveLists.Swap(global, level))
            {
                foreach (var phase in SavePhaseTable.Order)
                {
                    foreach (var target in targets)
                        RunSave(target, phase);
                }
            }
            return (global, level);
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Offers <paramref name="header"/> to the component's load phases. The game marks an entry the
    /// component took by nulling its slot.
    /// </summary>
    public static ApplyOutcome Apply(SavableScript script, object header)
    {
        if (_running || !GameSaveLists.Ready)
            return ApplyOutcome.NotTaken;

        _running = true;
        var entries = GameSaveLists.NewList();
        entries.Add(header);
        bool threw = false;
        try
        {
            using (new GameSaveLists.Swap(entries, entries))
            {
                var mask = SavePhaseTable.Overridden(script.GetType(), saving: false);
                foreach (var phase in SavePhaseTable.Order)
                {
                    if ((mask & phase) == 0)
                        continue;
                    try
                    {
                        SavePhaseTable.RunLoad(script, phase);
                    }
                    catch (Exception e)
                    {
                        threw = true;
                        SaveState.Log.Error($"Applying state to {script.name} ({script.GetType().Name}) threw in {phase}", e);
                    }
                }
            }
        }
        finally
        {
            _running = false;
        }
        if (threw)
            return ApplyOutcome.Threw;
        return entries[0] == null ? ApplyOutcome.Applied : ApplyOutcome.NotTaken;
    }

    private static void RunSave(SaveTarget target, SavePhases phase)
    {
        if ((target.Phases & phase) == 0 || target.Script == null)
            return;
        if ((SavePhaseTable.Overridden(target.Script.GetType(), saving: true) & phase) == 0)
            return;
        try
        {
            SavePhaseTable.RunSave(target.Script, phase);
        }
        catch (Exception e)
        {
            SaveState.Log.Error($"Capturing {target.Script.name} ({target.Script.GetType().Name}) threw in {phase}", e);
        }
    }
}
