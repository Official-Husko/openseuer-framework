// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Clock;
using ObenFramework.Game;
using ObenFramework.NewGames;
using ObenFramework.Quests;
using ObenFramework.Saves;
using ObenFramework.Story;
using ObenLoader;
using ObenLoader.Logging;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// Where the game is in its own life cycle, read live: the scene and world, the play session, the story
/// pump's ready gate and queue (what mods deferred to run outside the game's calls), the save gate, and
/// what the logs held back. Read only; the lines update four times a second while shown.
/// </summary>
internal static class SessionSection
{
    private const int QueueShown = 8;

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("World and session");
        ui.Live(() => $"Scene {WorldState.SceneName}: in a level {Yes(WorldState.InWorld)}, ready {Yes(WorldState.IsReady)}, paused {Yes(WorldState.IsPaused)}");
        ui.Live(() => $"Session {Sessions.Current}: running {Yes(Sessions.IsRunning)}, new game {Yes(Sessions.IsNewGame)}");
        ui.Live(() => $"Save gate: saving {Yes(SaveGate.IsSaving)}, loading {Yes(SaveGate.IsLoading)}, save loading {Yes(WorldState.IsLoadingSave)}");
        ui.Live(() => GameClock.IsAvailable ? $"Day {GameClock.Day}, {GameClock.TimeText}, time speed {GameClock.TimeSpeed:0.##}" : "No clock here.");
        ui.Live(() => QuestLog.IsAvailable ? $"{QuestLog.All().Count} tasks started" : "No task log here.");
        ui.Live(() => DevMarks.Modified ? "This save was changed from the developer menu." : "This save wasn't changed from the developer menu.");

        ui.Header("Story queue");
        ui.Live(() => $"Ready {Yes(StoryPump.IsReady)}, waiting for the quest system {Yes(StoryPump.WaitingForReady)}, {StoryPump.Pending} actions queued");
        ui.Live(() => Queue(StoryPump.PendingNames()), muted: true);

        ui.Header("Logs");
        ui.Live(() => $"{LogHub.DroppedLines} lines dropped over line limits and {LogHub.HeldBackErrors} repeated errors held back this session; verbose logging {Yes(LogHub.Verbose)}");
        // The menu font has no angle brackets.
        ui.Label($"Files in {LoaderPaths.LogsDir}: latest.log, errors.log, and mods/(mod id).log for each mod", muted: true);
    }

    private static string Yes(bool value) => value ? "yes" : "no";

    private static string Queue(List<string> names)
    {
        if (names.Count == 0)
            return "Nothing is waiting.";
        int shown = System.Math.Min(names.Count, QueueShown);
        string text = string.Join(", ", names.GetRange(0, shown));
        return names.Count > shown ? $"{text} and {names.Count - shown} more" : text;
    }
}
