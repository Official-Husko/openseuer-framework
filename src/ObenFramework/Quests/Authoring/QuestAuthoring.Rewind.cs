// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Story;
using ObenLoader;

namespace ObenFramework.Quests;

/// <summary>
/// Rewinding a mod's own task: new texts, starting over, removal, jumping to a step, and named checkpoints.
/// Each raises <see cref="QuestLog.Rewound"/>. They work on a task of this mod in the loaded save, never during
/// a save or a load.
/// </summary>
public static partial class QuestAuthoring
{
    /// <summary>Changes the task's title. Refused when another task already has that title.</summary>
    public static QuestResult SetTitle(ObenMod owner, string taskId, string title) =>
        Rewind(owner, taskId, entry => QuestRewind.SetTaskTitle(entry, title));

    /// <summary>Changes a step's title. A hidden step keeps showing "???" and gets the new title when revealed.</summary>
    public static QuestResult SetTitle(ObenMod owner, string taskId, string objectiveKey, string title) =>
        Rewind(owner, taskId, entry => QuestRewind.SetObjectiveTitle(entry, objectiveKey, title));

    /// <summary>Changes the task's description.</summary>
    public static QuestResult SetDescription(ObenMod owner, string taskId, string description) =>
        Rewind(owner, taskId, entry => QuestRewind.SetDescription(entry, description));

    /// <summary>Sends a finished, failed or abandoned task back to its start. Use <see cref="Reset"/> for an active one.</summary>
    public static QuestResult Restart(ObenMod owner, string taskId) =>
        Rewind(owner, taskId, entry => entry.status == CurrentTaskStatus.Active
            ? QuestResult.Unchanged("The task is still active; Reset starts it over.")
            : QuestRewind.Start(entry, QuestRewindKind.Restart));

    /// <summary>Sends the task back to its start from any state: active, steps at their start, counters empty, original texts.</summary>
    public static QuestResult Reset(ObenMod owner, string taskId) =>
        Rewind(owner, taskId, entry => QuestRewind.Start(entry, QuestRewindKind.Reset));

    /// <summary>Takes the task out of the task panel and the log. It disappears from the save at the next save.</summary>
    public static QuestResult Remove(ObenMod owner, string taskId) =>
        Rewind(owner, taskId, QuestRewind.Remove);

    /// <summary>Makes one step the active one; the steps before it are finished, the ones after it start over.</summary>
    public static QuestResult JumpTo(ObenMod owner, string taskId, string objectiveKey) =>
        Rewind(owner, taskId, entry => QuestRewind.JumpTo(entry, objectiveKey));

    /// <summary>
    /// Keeps a named copy of the task's state (up to 16 per task, names of 1 to 32 letters, digits or '-').
    /// <paramref name="restored"/> runs each time the copy is put back with <see cref="RestoreCheckpoint"/>, and
    /// is dropped when <paramref name="owner"/> unloads. Copies last for the session only.
    /// </summary>
    public static QuestResult SaveCheckpoint(ObenMod owner, string taskId, string name, Action restored = null) =>
        Rewind(owner, taskId, entry => QuestCheckpoints.Save(owner, entry, name, restored));

    /// <summary>Puts a kept copy back.</summary>
    public static QuestResult RestoreCheckpoint(ObenMod owner, string taskId, string name) =>
        Rewind(owner, taskId, entry => QuestCheckpoints.Restore(entry, name));

    /// <summary>The checkpoint names kept for a task in this session, oldest first.</summary>
    public static List<string> Checkpoints(string taskId) => QuestCheckpoints.Names(taskId);

    private static QuestResult Rewind(ObenMod owner, string taskId, Func<TaskStatusInfo, QuestResult> change)
    {
        if (!Owned(owner, taskId, out var entry, out var refused))
            return refused;
        try
        {
            return change(entry);
        }
        catch (Exception e)
        {
            StoryLog.Failed("Rewinding " + taskId, e);
            return QuestResult.Unchanged("A game call threw; it is logged.");
        }
    }
}
