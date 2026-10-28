// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenFramework.Saves;

/// <summary>
/// Reads and restores the saved state of live game objects without writing a save. State is the
/// same JSON the game puts in its own save files for that object (<c>{"GUID":..,"Data":{..}}</c>),
/// produced by running the object's own save callbacks and the game's serializer, and restored by
/// running its own load callbacks, so it carries exactly what a real save would: nothing more, and
/// nothing a save drops (see docs/modding/framework/save-state.md). Nothing here touches the save
/// files on disk, and every call refuses while <see cref="SaveGate.IsBusy"/>.
/// </summary>
public static partial class SaveState
{
    internal static ModLogger Log;

    internal static void Init(ObenMod framework)
    {
        Log = framework.Log;
        framework.Track(Disposable.Create(SavePhaseTable.Clear));
    }

    /// <summary>
    /// The saved state of one object as game save JSON, or null when it cannot be captured: not a
    /// <c>SavableScript</c>, no GUID, a save or load in flight, or the object wrote nothing.
    /// </summary>
    public static string Capture(UnityEngine.Object savable)
    {
        try
        {
            var script = Preflight(savable, out var guid);
            if (script == null)
                return null;

            var captured = SaveRunner.Capture(new List<SaveTarget> { new SaveTarget(script, SavePhases.All) });
            if (captured == null)
                return null;
            var entry = SaveEntries.Find(captured.Value.Level, guid) ?? SaveEntries.Find(captured.Value.Global, guid);
            return entry == null ? null : SaveEntries.Write(entry);
        }
        catch (Exception e)
        {
            Log.Error("Capturing object state failed", e);
            return null;
        }
    }

    /// <summary>
    /// Restores <paramref name="blob"/> onto the live object through its own load callbacks. True
    /// when the object took it. Blobs are checked first (size, nesting, the GUID and the types they
    /// name), the object's current state is captured as a fallback, and that state is put back if
    /// applying throws halfway.
    /// </summary>
    public static bool Apply(UnityEngine.Object savable, string blob)
    {
        try
        {
            var script = Preflight(savable, out var guid);
            if (script == null)
                return false;

            var header = SaveEntries.Read(script, guid, blob, out var problem);
            if (header == null)
            {
                Log.Warn($"Refused state for {guid}: {problem}");
                return false;
            }

            var fallback = Capture(script);
            var outcome = SaveRunner.Apply(script, header);
            if (outcome == ApplyOutcome.Threw)
                Restore(script, guid, fallback);
            return outcome == ApplyOutcome.Applied;
        }
        catch (Exception e)
        {
            Log.Error("Applying object state failed", e);
            return false;
        }
    }

    // Common checks for the single-object calls: a savable with a GUID, outside saves and loads.
    private static SavableScript Preflight(UnityEngine.Object savable, out string guid)
    {
        guid = null;
        var script = savable as SavableScript;
        if (script == null || SaveGate.IsBusy || !GameSaveLists.Ready)
            return null;
        guid = SaveController.GetObjectGUID(script);
        return string.IsNullOrEmpty(guid) ? null : script;
    }

    private static void Restore(SavableScript script, string guid, string fallback)
    {
        try
        {
            var header = fallback == null ? null : SaveEntries.Read(script, guid, fallback, out _);
            if (header != null && SaveRunner.Apply(script, header) == ApplyOutcome.Applied)
                return;
            Log.Error($"Restoring {guid} after a failed apply did not work; the object may be half applied.");
        }
        catch (Exception e)
        {
            Log.Error($"Restoring {guid} after a failed apply threw; the object may be half applied.", e);
        }
    }
}
