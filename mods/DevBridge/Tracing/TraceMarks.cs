// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ObenFramework.Clock;
using ObenFramework.Player;
using ObenFramework.Saves;
using ObenLoader.Mods;

namespace DevBridge.Tracing;

/// <summary>
/// What the tracer writes about the game around the calls, like the reference's debug mode did: where the
/// game is when a scene loads or a save is written or read (day and time, the player's position, the save
/// gate), and when a trace starts the mods loaded and what each Harmony id had patched already. Main thread.
/// </summary>
internal static class TraceMarks
{
    private const int OwnersShown = 20;

    /// <summary>"day 3 14:05, player at 97.5, -89.9, 23.5, saving no, loading no"; what isn't there is left out.</summary>
    public static string State()
    {
        var parts = new List<string>();
        if (GameClock.IsAvailable)
            parts.Add($"day {GameClock.Day} {GameClock.TimeText}");
        var at = PlayerRef.Position;
        if (at.HasValue)
            parts.Add($"player at {TraceFormat.Value(at.Value)}");
        parts.Add($"saving {Yes(SaveGate.IsSaving)}, loading {Yes(SaveGate.IsLoading)}");
        return string.Join(", ", parts);
    }

    /// <summary>The lines for the start of a trace: the mods and what was patched before it.</summary>
    public static List<string> Snapshot()
    {
        var lines = new List<string>();
        var mods = ModRegistry.Mods.Where(m => m.State == ModState.Loaded).Select(m => $"{m.Id} {m.Manifest.Version}");
        lines.Add("mods loaded: " + string.Join(", ", mods));

        var counts = new Dictionary<string, int>();
        int patched = 0;
        foreach (var original in Harmony.GetAllPatchedMethods())
        {
            var info = Harmony.GetPatchInfo(original);
            // The trace's own patches are already on when this is read; they aren't what was there before.
            if (info == null || !info.Owners.Any(id => id != TraceHooks.HarmonyId))
                continue;
            patched++;
            foreach (string id in info.Owners)
            {
                if (id != TraceHooks.HarmonyId)
                    counts[id] = counts.TryGetValue(id, out int n) ? n + 1 : 1;
            }
        }
        var owners = counts.OrderByDescending(p => p.Value).Take(OwnersShown).Select(p => $"{p.Key} {p.Value}");
        lines.Add($"patched before the trace: {patched} methods; by owner " + string.Join(", ", owners)
            + (counts.Count > OwnersShown ? $" and {counts.Count - OwnersShown} more" : "") + " (patches.list has each method)");
        return lines;
    }

    private static string Yes(bool value) => value ? "yes" : "no";
}
