// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Story;

namespace ObenFramework.Quests;

/// <summary>
/// Sends a mod's task back: new texts, a restart, a reset, removal, or a jump to one step. The game has no
/// calls for any of this, so the task's status, each step's status and counter, and the game's Lua lists are
/// rewritten directly, the texts restored from the remembered definition, and the task panel redrawn.
/// Callers have checked ownership (<see cref="QuestAuthoring.Owned"/>).
/// </summary>
internal static class QuestRewind
{
    public static QuestResult SetTaskTitle(TaskStatusInfo entry, string title)
    {
        if (!QuestIds.ValidText(title, QuestIds.MaxTitle, required: true, multiline: false))
            return QuestResult.Unchanged($"A title is 1 to {QuestIds.MaxTitle} characters on one line.");
        if (QuestAuthoring.Conflict(TaskController.instance, entry.Id, title, entry) != QuestRefusal.None)
            return QuestResult.Unchanged($"Another task is already called '{title}'.");
        entry.task.title = title;
        QuestRegistry.Book.RememberText(entry.Id, null, title, entry.task.description);
        return Finish(entry, QuestRewindKind.Title, null);
    }

    public static QuestResult SetObjectiveTitle(TaskStatusInfo entry, string objectiveKey, string title)
    {
        if (!QuestIds.ValidText(title, QuestIds.MaxTitle, required: true, multiline: false))
            return QuestResult.Unchanged($"A title is 1 to {QuestIds.MaxTitle} characters on one line.");
        var objective = QuestLog.FindObjective(entry, objectiveKey);
        if (objective == null)
            return QuestResult.Unchanged("The task has no such objective.");
        var saved = QuestRegistry.FindObjective(entry.Id, objectiveKey);
        bool hidden = objective.status == ObjectiveStatus.Hidden;
        // A hidden step keeps showing "???"; the new title is what Reveal and a restart will use.
        if (!hidden)
            objective.objective.title = title;
        QuestRegistry.Book.RememberText(entry.Id, objectiveKey, title, hidden && saved != null ? saved.Description : objective.objective.description);
        return Finish(entry, QuestRewindKind.Title, null);
    }

    public static QuestResult SetDescription(TaskStatusInfo entry, string description)
    {
        if (!QuestIds.ValidText(description, QuestIds.MaxDescription, required: false, multiline: true))
            return QuestResult.Unchanged($"A description is at most {QuestIds.MaxDescription} characters without control characters.");
        entry.task.description = description ?? "";
        QuestRegistry.Book.RememberText(entry.Id, null, entry.task.title, entry.task.description);
        return Finish(entry, QuestRewindKind.Description, null);
    }

    /// <summary>Back to the start: active, every step in its first status with an empty counter, the original texts.</summary>
    public static QuestResult Start(TaskStatusInfo entry, QuestRewindKind kind)
    {
        string from = ActiveKey(entry);
        var record = QuestRegistry.Find(entry.Id);
        var controller = TaskController.instance;
        entry.status = CurrentTaskStatus.Active;
        if (record != null && !string.IsNullOrEmpty(record.Title))
            entry.task.title = record.Title;
        if (record?.Description != null)
            entry.task.description = record.Description;
        TaskLists(controller, entry.Id, QuestRewindPlan.Active);
        foreach (var objective in entry.objectives)
        {
            var saved = QuestBook.FindObjective(record, objective.objective.Id);
            int start = QuestRewindPlan.StartStatus(saved != null && saved.Hidden);
            ApplyText(objective, saved, start, null, null);
            objective.status = (ObjectiveStatus)start;
            objective.currentAmount = 0;
            ObjectiveLists(controller, entry.Id, objective.objective.Id, start);
        }
        return Finish(entry, kind, from);
    }

    /// <summary>Makes one step the active one: the steps before it finished with full counters, the rest back at their start.</summary>
    public static QuestResult JumpTo(TaskStatusInfo entry, string objectiveKey)
    {
        int chosen = entry.objectives.FindIndex(o => o?.objective != null && o.objective.Id == objectiveKey);
        if (chosen < 0)
            return QuestResult.Unchanged("The task has no such objective.");

        string from = ActiveKey(entry);
        var record = QuestRegistry.Find(entry.Id);
        var controller = TaskController.instance;
        entry.status = CurrentTaskStatus.Active;
        TaskLists(controller, entry.Id, QuestRewindPlan.Active);
        for (int i = 0; i < entry.objectives.Count; i++)
        {
            var objective = entry.objectives[i];
            var saved = QuestBook.FindObjective(record, objective.objective.Id);
            int status = QuestRewindPlan.JumpStatus(i, chosen, QuestRewindPlan.StartStatus(saved != null && saved.Hidden));
            objective.currentAmount = QuestRewindPlan.JumpAmount(i, chosen, objective.counterAmount);
            ApplyText(objective, saved, status, null, null);
            objective.status = (ObjectiveStatus)status;
            ObjectiveLists(controller, entry.Id, objective.objective.Id, status);
        }
        return Finish(entry, QuestRewindKind.JumpTo, from);
    }

    /// <summary>Takes the task out of the log and the game's lists. It is gone from the save at the next save.</summary>
    public static QuestResult Remove(TaskStatusInfo entry)
    {
        var controller = TaskController.instance;
        string id = entry.Id;
        controller.allTasks.Remove(entry);
        TaskLists(controller, id, QuestRewindPlan.Gone);
        foreach (var objective in entry.objectives)
            ObjectiveLists(controller, id, objective.objective.Id, QuestRewindPlan.Gone);
        QuestCheckpoints.Forget(id);
        QuestRegistry.Book.Forget(id);
        QuestPanel.RemoveRow(entry);
        try
        {
            foreach (var objective in entry.objectives)
            {
                if (objective?.objective != null)
                    UnityEngine.Object.Destroy(objective.objective);
            }
            UnityEngine.Object.Destroy(entry.task);
        }
        catch (Exception e)
        {
            StoryLog.Failed("Removing a task's objects", e);
        }
        QuestWatcher.Poke();
        QuestLog.RaiseRewound(id, QuestRewindKind.Remove);
        return QuestResult.Done;
    }

    internal static QuestResult Finish(TaskStatusInfo entry, QuestRewindKind kind, string from)
    {
        QuestPanel.Refresh(entry);
        QuestWatcher.Poke();
        bool moved = kind != QuestRewindKind.Title && kind != QuestRewindKind.Description;
        QuestLog.RaiseRewound(entry.Id, kind, moved ? from : null, moved ? ActiveKey(entry) : null);
        return QuestResult.Done;
    }

    internal static string ActiveKey(TaskStatusInfo entry)
    {
        foreach (var objective in entry.objectives)
        {
            if (objective.status == ObjectiveStatus.Active)
                return objective.objective.Id;
        }
        return null;
    }

    internal static void ApplyText(ObjectiveStatusInfo objective, QuestObjectiveRecord saved, int status, string capturedTitle, string capturedDescription)
    {
        var plan = QuestRewindPlan.Text(status, saved != null, saved?.Title, saved?.Description, capturedTitle, capturedDescription);
        if (!plan.HasText)
            return;
        objective.objective.title = plan.Title;
        objective.objective.description = plan.Description;
    }

    internal static void TaskLists(TaskController c, string taskId, int status) =>
        QuestRewindPlan.TaskLists(c.anyStatusTasksIds, c.activeTasksIds, c.failedTasksIds, c.abandonedTasksIds, c.finishedTasksIds, taskId, status);

    internal static void ObjectiveLists(TaskController c, string taskId, string objectiveKey, int status) =>
        QuestRewindPlan.ObjectiveLists(c.anyStatusObjectives, c.activeObjectives, c.failedObjectives, c.abandonedObjectives,
            c.completedObjectives, QuestIds.ObjectiveEntry(taskId, objectiveKey), status);
}
