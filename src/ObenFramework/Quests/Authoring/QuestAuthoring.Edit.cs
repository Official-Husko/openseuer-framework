// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Items;
using ObenFramework.Story;
using ObenLoader;

namespace ObenFramework.Quests;

public static partial class QuestAuthoring
{
    /// <summary>Adds a step to a running task of this mod. Refused when the key is taken or the task has 12 steps.</summary>
    public static QuestResult AddObjective(ObenMod owner, string taskId, QuestObjectiveDefinition objective)
    {
        if (!Owned(owner, taskId, out var entry, out var refused))
            return refused;
        string problem = QuestPlan.CheckObjective(objective);
        if (problem != null)
            return QuestResult.Unchanged(problem);
        if (entry.status != CurrentTaskStatus.Active)
            return QuestResult.Unchanged($"The task is {entry.status}, not Active.");
        if (entry.objectives.Count >= QuestIds.MaxObjectives)
            return QuestResult.Unchanged($"The task already has {QuestIds.MaxObjectives} objectives.");
        if (QuestLog.FindObjective(entry, objective.Key) != null)
            return QuestResult.Unchanged($"The task already has an objective '{objective.Key}'.");

        var built = NewObjective(entry.task, objective);
        bool ok = false;
        try
        {
            ok = TaskController.instance.EditTask(entry.task, built, objective.Hidden ? ObjectiveStatus.Hidden : ObjectiveStatus.Active,
                objective.Optional, createNewTask: false, CurrentTaskStatus.Active);
        }
        catch (Exception e)
        {
            StoryLog.Failed("QuestAuthoring.AddObjective", e);
        }
        if (!ok)
        {
            if (QuestLog.FindObjective(entry, objective.Key) == null)
                UnityEngine.Object.Destroy(built);
            return QuestResult.Unchanged("The game refused the objective.");
        }
        QuestRegistry.Book.RememberObjective(taskId, objective);
        QuestWatcher.Poke();
        return QuestResult.Done;
    }

    /// <summary>Finishes, fails or abandons an active step of a task of this mod, like <see cref="QuestLog.SetObjectiveStatus"/>.</summary>
    public static QuestResult SetObjective(ObenMod owner, string taskId, string objectiveKey, ObjectiveStatus status)
    {
        if (!Owned(owner, taskId, out _, out var refused))
            return refused;
        if (status != ObjectiveStatus.Finished && status != ObjectiveStatus.Failed && status != ObjectiveStatus.Abandoned)
            return QuestResult.Unchanged("Pick Finished, Failed or Abandoned; Reveal shows a hidden step.");
        return QuestLog.SetObjectiveStatus(taskId, objectiveKey, status);
    }

    /// <summary>Sets a step's counter to a number from 0 to its target. Reaching the target finishes the step.</summary>
    public static QuestResult SetCounter(ObenMod owner, string taskId, string objectiveKey, int amount)
    {
        if (!Owned(owner, taskId, out var entry, out var refused))
            return refused;
        var objective = QuestLog.FindObjective(entry, objectiveKey);
        if (objective == null)
            return QuestResult.Unchanged("The task has no such objective.");
        if (amount < 0 || amount > objective.counterAmount)
            return QuestResult.Unchanged($"The amount must be from 0 to {objective.counterAmount}.");
        if (objective.status != ObjectiveStatus.Active && objective.status != ObjectiveStatus.Hidden)
            return QuestResult.Unchanged($"The objective is {objective.status}.");
        return QuestLog.SetObjectiveProgress(taskId, objectiveKey, amount);
    }

    /// <summary>Ends a running task of this mod as finished; the game abandons steps that are still active.</summary>
    public static QuestResult Finish(ObenMod owner, string taskId) => Close(owner, taskId, CurrentTaskStatus.Finished);

    /// <summary>Ends a running task of this mod as failed.</summary>
    public static QuestResult Fail(ObenMod owner, string taskId) => Close(owner, taskId, CurrentTaskStatus.Failed);

    /// <summary>
    /// Takes the items a step asks for (<see cref="QuestObjectiveDefinition.ItemId"/> and Count) from the
    /// player and moves the step on: a counting step gains Count, any other finishes. The items are given back
    /// when the step can't move.
    /// </summary>
    public static QuestResult HandIn(ObenMod owner, string taskId, string objectiveKey)
    {
        if (!Owned(owner, taskId, out var entry, out var refused))
            return refused;
        var objective = QuestLog.FindObjective(entry, objectiveKey);
        if (entry.status != CurrentTaskStatus.Active || objective == null || objective.status != ObjectiveStatus.Active)
            return QuestResult.Unchanged("The task or the objective is not active.");
        var saved = QuestRegistry.FindObjective(taskId, objectiveKey);
        if (saved == null || saved.ItemId <= 0 || saved.Count <= 0)
            return QuestResult.Unchanged("The objective doesn't ask for items.");
        if (!PlayerItems.Has(saved.ItemId, saved.Count))
            return QuestResult.Unchanged($"The player doesn't carry {saved.Count} of item {saved.ItemId}.");
        if (!PlayerItems.Take(saved.ItemId, saved.Count))
            return QuestResult.Unchanged("The items could not be taken.");

        QuestResult moved;
        try
        {
            moved = objective.counterAmount > 0
                ? QuestLog.SetObjectiveProgress(taskId, objectiveKey, saved.Count, add: true)
                : QuestLog.SetObjectiveStatus(taskId, objectiveKey, ObjectiveStatus.Finished);
        }
        catch (Exception e)
        {
            StoryLog.Failed("QuestAuthoring.HandIn", e);
            moved = QuestResult.Unchanged("The step could not be moved on.");
        }
        if (!moved.Changed && !PlayerItems.Give(saved.ItemId, saved.Count))
            StoryLog.Refused("QuestAuthoring.HandIn", $"the {saved.Count} of item {saved.ItemId} could not be given back");
        return moved;
    }

    /// <summary>Gives a hidden step of a running task its real text and makes it active.</summary>
    public static QuestResult Reveal(ObenMod owner, string taskId, string objectiveKey, string title, string description = null)
    {
        if (!Owned(owner, taskId, out var entry, out var refused))
            return refused;
        if (!QuestIds.ValidText(title, QuestIds.MaxTitle, required: true, multiline: false)
            || !QuestIds.ValidText(description, QuestIds.MaxDescription, required: false, multiline: true))
            return QuestResult.Unchanged("The title or the description breaks the text rules.");
        var objective = QuestLog.FindObjective(entry, objectiveKey);
        if (entry.status != CurrentTaskStatus.Active || objective == null || !QuestPlan.CanReveal((int)objective.status))
            return QuestResult.Unchanged("Only a hidden step of an active task can be revealed.");

        var controller = TaskController.instance;
        try
        {
            objective.objective.title = title;
            objective.objective.description = description ?? "";
            controller.EditTaskObjective(entry, objective, ObjectiveStatus.Active);
            if (objective.status != ObjectiveStatus.Active)
            {
                Hide(objective);
                return QuestResult.Unchanged("The game did not activate the step.");
            }
            QuestRegistry.Book.RememberText(taskId, objectiveKey, title, description ?? "");
            if (QuestPlan.ShouldFinishOnReveal(objective.currentAmount, objective.counterAmount, objective.objective.dontSelfCompleteWithCounter))
            {
                controller.EditTaskObjective(entry, objective, ObjectiveStatus.Finished);
                controller.FinishIfNoActiveObjectives(entry);
            }
            TaskPanel.instance.UpdateCurrentTaskInfo();
        }
        catch (Exception e)
        {
            StoryLog.Failed("QuestAuthoring.Reveal", e);
            if (objective.status == ObjectiveStatus.Hidden)
                Hide(objective);
            return QuestResult.Unchanged("A game call threw; it is logged.");
        }
        QuestWatcher.Poke();
        return QuestResult.Done;
    }

    private static QuestResult Close(ObenMod owner, string taskId, CurrentTaskStatus status)
    {
        if (!Owned(owner, taskId, out var entry, out var refused))
            return refused;
        if (entry.status != CurrentTaskStatus.Active)
            return QuestResult.Unchanged($"The task is {entry.status}, not Active.");
        return QuestLog.SetTaskStatus(taskId, status);
    }

    private static void Hide(ObjectiveStatusInfo objective)
    {
        objective.objective.title = QuestIds.Placeholder;
        objective.objective.description = "";
    }

    /// <summary>The task is this mod's, started, and quests can be written now.</summary>
    internal static bool Owned(ObenMod owner, string taskId, out TaskStatusInfo entry, out QuestResult refused)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        entry = null;
        if (!QuestIds.Owns(owner.Info.Id, taskId))
        {
            refused = QuestResult.Unchanged($"The task '{taskId}' doesn't belong to {owner.Info.Id}; a mod can only change tasks whose id starts with its own id.");
            return false;
        }
        if (Blocker(out string why) != QuestRefusal.None)
        {
            refused = QuestResult.Unchanged(char.ToUpperInvariant(why[0]) + why.Substring(1) + ".");
            return false;
        }
        entry = QuestLog.Find(taskId);
        if (entry == null)
        {
            refused = QuestResult.Unchanged("The task is not in this save.");
            return false;
        }
        refused = default;
        return true;
    }
}
