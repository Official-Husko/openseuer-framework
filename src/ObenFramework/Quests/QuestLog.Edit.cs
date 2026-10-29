using System;

namespace ObenFramework.Quests;

/// <summary>
/// Changing quests. Every method goes through the game's own <c>TaskController</c> code, so
/// notifications, the task panel, follow-up objectives and Lua lists stay right. That code does nothing
/// without saying so when a requirement isn't met, so each method compares the state before and after
/// and returns a <see cref="QuestResult"/>.
/// </summary>
public static partial class QuestLog
{
    /// <summary>
    /// Starts a task the game has loaded (see <see cref="Available"/>) by handing out its first objectives,
    /// like the scene triggers do.
    /// </summary>
    public static QuestResult Start(string taskId)
    {
        var blocked = EditBlocker();
        if (blocked != null)
            return QuestResult.Unchanged(blocked);

        var existing = Find(taskId);
        if (existing != null)
            return QuestResult.Unchanged($"The task is already started ({existing.status}).");

        var asset = QuestAssets.FindTask(taskId);
        if (asset == null)
            return QuestResult.Unchanged("The game has no task with that id in memory. It is loaded when the scene that uses it is.");
        if (asset.firstObjectives.Count == 0)
            return QuestResult.Unchanged("The task has no first objectives to hand out.");

        asset.GiveFirstObjectives();
        return Finish(Find(taskId) != null, "The game refused to start it: a required task is missing or an objective's requirement is not met.");
    }

    /// <summary>
    /// Sets a started task to Active, Finished, Failed or Abandoned. Moving a task out of Active abandons its
    /// active objectives, like the game does. The game's status lists (what the Lua functions
    /// <c>TaskActive</c>, <c>TaskFinished</c> ... read) follow the new status, also when a finished task is reopened.
    /// </summary>
    public static QuestResult SetTaskStatus(string taskId, CurrentTaskStatus status)
    {
        if (status == CurrentTaskStatus.AnyStatus)
            throw new ArgumentException("AnyStatus only works as a filter; pick Active, Finished, Failed or Abandoned.", nameof(status));

        var blocked = EditBlocker();
        if (blocked != null)
            return QuestResult.Unchanged(blocked);

        var entry = Find(taskId);
        if (entry == null)
            return QuestResult.Unchanged("The task is not started.");
        if (entry.status == status)
            return QuestResult.Unchanged($"The task is already {status}.");

        TaskController.instance.EditTaskStatus(entry, status);
        // In play a task only ever leaves Active, so the game's lists keep a reopened task under its old
        // status too (TaskFinished and TaskActive both true). Put it in the lists of its new status only.
        if (entry.status == status)
            QuestRewind.TaskLists(TaskController.instance, entry.Id, (int)status);
        return Finish(entry.status == status, "The game did not change the task.");
    }

    /// <summary>
    /// Sets an objective's status. Finishing, failing or abandoning needs an active objective in an active
    /// task, and the game's requirements for it. Activating also hands out an objective that was never given,
    /// if the game has it loaded, and reopens one that was closed. Finishing the last active objective can
    /// finish the whole task, as it does in play.
    /// </summary>
    public static QuestResult SetObjectiveStatus(string taskId, string objectiveId, ObjectiveStatus status)
    {
        if (status != ObjectiveStatus.Active && status != ObjectiveStatus.Finished
            && status != ObjectiveStatus.Failed && status != ObjectiveStatus.Abandoned)
            throw new ArgumentException("Pick Active, Finished, Failed or Abandoned.", nameof(status));

        var blocked = EditBlocker();
        if (blocked != null)
            return QuestResult.Unchanged(blocked);

        var task = Find(taskId);
        var objective = FindObjective(task, objectiveId);
        if (objective == null)
            return status == ObjectiveStatus.Active ? GiveObjective(taskId, objectiveId, task) : QuestResult.Unchanged("The objective is not started.");
        if (objective.status == status)
            return QuestResult.Unchanged($"The objective is already {status}.");
        if (task.status != CurrentTaskStatus.Active)
            return QuestResult.Unchanged($"The task is {task.status}, not Active.");

        if (status == ObjectiveStatus.Active)
        {
            TaskController.instance.EditTaskObjective(task, objective, status);
            // Same as for tasks: a reopened objective would still count as completed for the game's Lua.
            if (objective.status == ObjectiveStatus.Active)
                QuestRewind.ObjectiveLists(TaskController.instance, task.Id, objective.objective.Id, (int)ObjectiveStatus.Active);
            TaskPanel.instance.UpdateCurrentTaskInfo();
        }
        else if (objective.status != ObjectiveStatus.Active)
        {
            return QuestResult.Unchanged($"The objective is {objective.status}; only an active one can become {status}.");
        }
        else
        {
            TaskController.instance.EditTask(task.task, objective.objective, status, objective.optional,
                createNewTask: false, CurrentTaskStatus.Active, objectiveHasToExist: true);
        }
        return Finish(objective.status == status, "The game refused the change: a requirement of the task or objective is not met.");
    }

    /// <summary>
    /// Sets (or with <paramref name="add"/> adds to) the counter of a counting objective, clamped to its
    /// target. Reaching the target finishes the objective, as in play.
    /// </summary>
    public static QuestResult SetObjectiveProgress(string taskId, string objectiveId, int amount, bool add = false)
    {
        var blocked = EditBlocker();
        if (blocked != null)
            return QuestResult.Unchanged(blocked);

        var task = Find(taskId);
        var objective = FindObjective(task, objectiveId);
        if (objective == null)
            return QuestResult.Unchanged("The objective is not started.");
        if (objective.counterAmount <= 0)
            return QuestResult.Unchanged("The objective has no counter.");

        int amountBefore = objective.currentAmount;
        var statusBefore = objective.status;
        TaskController.instance.EditTaskCounter(task.task, objective.objective, amount, setamount: !add);
        return Finish(objective.currentAmount != amountBefore || objective.status != statusBefore,
            "The counter is already at that value, or the amount was clamped to where it was.");
    }

    private static QuestResult GiveObjective(string taskId, string objectiveId, TaskStatusInfo startedTask)
    {
        var taskAsset = startedTask != null ? startedTask.task : QuestAssets.FindTask(taskId);
        if (taskAsset == null)
            return QuestResult.Unchanged("The task is not started and the game has no task with that id in memory.");
        var objectiveAsset = QuestAssets.FindObjective(taskAsset, objectiveId);
        if (objectiveAsset == null)
            return QuestResult.Unchanged("The game has no objective with that id for the task in memory.");

        TaskController.instance.EditTask(taskAsset, objectiveAsset, ObjectiveStatus.Active, optional: false,
            createNewTask: true, CurrentTaskStatus.Active);
        var created = FindObjective(Find(taskId), objectiveId);
        return Finish(created != null, "The game refused the change: a requirement of the task or objective is not met.");
    }

    // The game raises its events only on some of these paths; make sure the watcher looks.
    private static QuestResult Finish(bool changed, string reasonIfNot)
    {
        QuestWatcher.Poke();
        return changed ? QuestResult.Done : QuestResult.Unchanged(reasonIfNot);
    }
}
