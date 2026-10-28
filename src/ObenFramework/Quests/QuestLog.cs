using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Game;

namespace ObenFramework.Quests;

/// <summary>
/// The player's tasks (the game's own quest system, <c>TaskController</c>): which are started, what
/// state each is in, and changes to them. Tasks and objectives are addressed by the string ids the
/// game's Lua functions and F1 console use (<c>list_tracked_tasks</c> prints them).
/// Reads and edits need a loaded world; <see cref="IsAvailable"/> says whether there is one.
/// Reading builds copies, so call it when something happened (see the events), not every frame.
/// </summary>
public static partial class QuestLog
{
    /// <summary>
    /// A task was started or changed status (finished, failed, abandoned, started again). Raised on the
    /// frame after the change, not inside the game's call, so handlers may edit the quests themselves.
    /// Changes made while a save loads are not reported. Each handler runs in its own try/catch.
    /// </summary>
    public static readonly GameEvent<TaskChange> TaskChanged = new GameEvent<TaskChange>(nameof(TaskChanged));

    /// <summary>
    /// An objective was handed out, changed status, or its counter moved. Same timing as <see cref="TaskChanged"/>.
    /// </summary>
    public static readonly GameEvent<ObjectiveChange> ObjectiveChanged = new GameEvent<ObjectiveChange>(nameof(ObjectiveChanged));

    /// <summary>The game's task state exists (a gameplay scene is loaded).</summary>
    public static bool IsAvailable => TaskController.instance != null;

    /// <summary>Every task the player has started, in the order the game keeps them.</summary>
    public static List<QuestTask> All()
    {
        var result = new List<QuestTask>();
        var controller = TaskController.instance;
        if (controller == null)
            return result;

        var tasks = controller.allTasks;
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i] != null && tasks[i].task != null)
                result.Add(Describe(tasks[i]));
        }
        return result;
    }

    /// <summary>One started task, or null when it wasn't started (or there is no world).</summary>
    public static QuestTask Get(string taskId)
    {
        var entry = Find(taskId);
        return entry == null ? null : Describe(entry);
    }

    /// <summary>The task's status, or null when it wasn't started.</summary>
    public static CurrentTaskStatus? StatusOf(string taskId) => Find(taskId)?.status;

    /// <summary>The objective's status, or null when the task or the objective wasn't started.</summary>
    public static ObjectiveStatus? StatusOf(string taskId, string objectiveId) => FindObjective(Find(taskId), objectiveId)?.status;

    /// <summary>
    /// The objective was handed out at some point in this save, whatever its status now, as the game's Lua
    /// <c>ObjectiveAnyStatus</c> answers it (from the game's lists rather than the task log).
    /// </summary>
    public static bool Reached(string taskId, string objectiveId)
    {
        var controller = TaskController.instance;
        return controller != null && !string.IsNullOrEmpty(taskId) && !string.IsNullOrEmpty(objectiveId)
            && controller.ObjectiveAnyStatus(taskId, objectiveId);
    }

    /// <summary>
    /// Tasks the game has in memory that the player hasn't started, which <see cref="Start"/> can begin.
    /// The game loads task assets on demand (through scene objects), so this is not a complete list.
    /// </summary>
    public static List<AvailableTask> Available() => QuestAssets.Unstarted();

    internal static TaskStatusInfo Find(string taskId)
    {
        var controller = TaskController.instance;
        if (controller == null || string.IsNullOrEmpty(taskId))
            return null;

        var tasks = controller.allTasks;
        for (int i = 0; i < tasks.Count; i++)
        {
            var entry = tasks[i];
            if (entry != null && entry.task != null && string.Equals(entry.Id, taskId, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    internal static ObjectiveStatusInfo FindObjective(TaskStatusInfo task, string objectiveId)
    {
        if (task == null || string.IsNullOrEmpty(objectiveId))
            return null;

        var list = task.objectives;
        for (int i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry != null && entry.objective != null && string.Equals(entry.objective.Id, objectiveId, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    private static QuestTask Describe(TaskStatusInfo entry)
    {
        var objectives = new List<QuestObjective>(entry.objectives.Count);
        foreach (var o in entry.objectives)
        {
            if (o == null || o.objective == null)
                continue;
            objectives.Add(new QuestObjective(o.objective.Id, o.objective.title, o.objective.description,
                o.status, o.optional, o.currentAmount, o.counterAmount));
        }
        return new QuestTask(entry.Id, entry.task.title, entry.task.description, entry.status, objectives.ToArray());
    }

    // Quest changes are only safe when the world is up and no save is being applied over them.
    internal static string EditBlocker()
    {
        if (WorldState.IsLoadingSave || Saves.SaveGate.IsBusy)
            return "A save is being written or loaded; the game would overwrite the change.";
        if (TaskController.instance == null || TaskPanel.instance == null || Notifications.instance == null)
            return "Quests can only be changed in a loaded world.";
        if (QuestPatches.InGameCall)
            return "Called from inside one of the game's own task edits; defer it (StoryPump.Defer) or use the QuestLog events.";
        return null;
    }
}
