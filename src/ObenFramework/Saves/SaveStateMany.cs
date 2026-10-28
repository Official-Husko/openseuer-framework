using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Saves;

/// <summary>What <see cref="SaveState.ApplyTree"/> and <see cref="SaveState.ApplyGlobals"/> did with a set of blobs.</summary>
public sealed class SaveApplyResult
{
    /// <summary>Blobs an object took.</summary>
    public int Applied { get; internal set; }

    /// <summary>Blobs passed over: no live object with that GUID, not on the allow list, or the object ignored it.</summary>
    public int Skipped { get; internal set; }

    /// <summary>GUIDs whose blob was refused as unsafe or whose object threw while loading it.</summary>
    public List<string> Failed { get; } = new List<string>();

    public override string ToString() => $"{Applied} applied, {Skipped} skipped, {Failed.Count} failed";
}

public static partial class SaveState
{
    /// <summary>
    /// The state of every savable under <paramref name="root"/>, keyed by GUID, with each phase run
    /// the way the game runs them (all objects per phase). Inactive objects are left out unless
    /// <paramref name="includeInactive"/> is set, as a real save leaves them out. Empty when a save
    /// or load is in flight.
    /// </summary>
    public static Dictionary<string, string> CaptureTree(GameObject root, bool includeInactive = false)
    {
        var result = new Dictionary<string, string>();
        if (root == null || SaveGate.IsBusy)
            return result;
        var captured = RunCapture(SaveTargets.Under(root, includeInactive));
        if (captured != null)
        {
            AddAll(result, captured.Value.Level);
            AddAll(result, captured.Value.Global);
        }
        return result;
    }

    /// <summary>
    /// Restores blobs from <see cref="CaptureTree"/> onto the savables under <paramref name="root"/>,
    /// matched by GUID. When <paramref name="allowedGuids"/> is given, only those GUIDs are applied;
    /// pass it for blobs that came from another player.
    /// </summary>
    public static SaveApplyResult ApplyTree(GameObject root, IReadOnlyDictionary<string, string> blobs, ICollection<string> allowedGuids = null)
    {
        if (root == null)
            return new SaveApplyResult();
        var scripts = new List<SavableScript>(root.GetComponentsInChildren<SavableScript>(true));
        return ApplyMany(scripts, blobs, allowedGuids);
    }

    /// <summary>
    /// The state of everything a game save would put in <c>Globals.tnmt</c> right now: the managers
    /// and singletons that live across levels (money, relationships, crime and so on), keyed by
    /// GUID. Level objects are not included. Empty when a save or load is in flight. This runs the
    /// save callbacks of the whole scene, like an autosave does, but writes no file.
    /// </summary>
    public static Dictionary<string, string> CaptureGlobals()
    {
        var result = new Dictionary<string, string>();
        if (SaveGate.IsBusy)
            return result;
        var captured = RunCapture(SaveTargets.WholeGame());
        if (captured != null)
            AddAll(result, captured.Value.Global);
        return result;
    }

    /// <summary>
    /// Restores blobs from <see cref="CaptureGlobals"/> (or the same managers on another machine)
    /// onto the live singletons, matched by GUID. Pass <paramref name="allowedGuids"/> for blobs
    /// that came from another player.
    /// </summary>
    public static SaveApplyResult ApplyGlobals(IReadOnlyDictionary<string, string> blobs, ICollection<string> allowedGuids = null)
    {
        var scripts = new List<SavableScript>();
        foreach (var target in SaveTargets.WholeGame())
        {
            if (!scripts.Contains(target.Script))
                scripts.Add(target.Script);
        }
        return ApplyMany(scripts, blobs, allowedGuids);
    }

    private static (IList Global, IList Level)? RunCapture(List<SaveTarget> targets)
    {
        try
        {
            return GameSaveLists.Ready ? SaveRunner.Capture(targets) : null;
        }
        catch (Exception e)
        {
            Log.Error("Capturing object state failed", e);
            return null;
        }
    }

    private static void AddAll(Dictionary<string, string> into, IList entries)
    {
        foreach (var entry in entries)
        {
            if (entry == null)
                continue;
            var guid = GameSaveLists.GuidOf(entry);
            var json = SaveEntries.Write(entry);
            if (json == null)
                continue;
            if (into.ContainsKey(guid))
                Log.Warn($"Two objects saved under the GUID {guid}; the later one wins.");
            into[guid] = json;
        }
    }

    private static SaveApplyResult ApplyMany(List<SavableScript> scripts, IReadOnlyDictionary<string, string> blobs, ICollection<string> allowedGuids)
    {
        var result = new SaveApplyResult();
        if (blobs == null || SaveGate.IsBusy)
            return result;

        var byGuid = new Dictionary<string, SavableScript>();
        foreach (var script in scripts)
        {
            var guid = script == null ? null : SaveController.GetObjectGUID(script);
            if (!string.IsNullOrEmpty(guid) && !byGuid.ContainsKey(guid))
                byGuid[guid] = script;
        }

        // Managers that load in an early phase go first, so objects that depend on them see them restored.
        var order = new List<KeyValuePair<string, string>>(blobs);
        order.Sort((a, b) => PhaseRank(byGuid, a.Key).CompareTo(PhaseRank(byGuid, b.Key)));

        foreach (var pair in order)
        {
            if (allowedGuids != null && !allowedGuids.Contains(pair.Key))
            {
                result.Skipped++;
                continue;
            }
            if (!byGuid.TryGetValue(pair.Key, out var script))
            {
                result.Skipped++;
                continue;
            }
            ApplyOne(script, pair.Key, pair.Value, result);
        }
        return result;
    }

    private static void ApplyOne(SavableScript script, string guid, string blob, SaveApplyResult result)
    {
        try
        {
            var header = SaveEntries.Read(script, guid, blob, out var problem);
            if (header == null)
            {
                Log.Warn($"Refused state for {guid}: {problem}");
                result.Failed.Add(guid);
                return;
            }
            var fallback = Capture(script);
            switch (SaveRunner.Apply(script, header))
            {
                case ApplyOutcome.Applied:
                    result.Applied++;
                    break;
                case ApplyOutcome.Threw:
                    Restore(script, guid, fallback);
                    result.Failed.Add(guid);
                    break;
                default:
                    result.Skipped++;
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Applying state to {guid} failed", e);
            result.Failed.Add(guid);
        }
    }

    private static int PhaseRank(Dictionary<string, SavableScript> byGuid, string guid)
    {
        if (!byGuid.TryGetValue(guid, out var script))
            return int.MaxValue;
        var mask = SavePhaseTable.Overridden(script.GetType(), saving: false);
        for (int i = 0; i < SavePhaseTable.Order.Length; i++)
        {
            if ((mask & SavePhaseTable.Order[i]) != 0)
                return i;
        }
        return SavePhaseTable.Order.Length;
    }
}
