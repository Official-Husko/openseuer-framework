// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenFramework.Story;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.Quests;

/// <summary>The outcome of <see cref="QuestAuthoring.Create"/>.</summary>
public readonly struct QuestCreateResult
{
    internal QuestCreateResult(string taskId, QuestRefusal refusal, string reason)
    {
        TaskId = taskId;
        Refusal = refusal;
        Reason = reason;
    }

    /// <summary>The new task's id; also set for <see cref="QuestRefusal.PartiallyCreated"/>. Null when nothing was made.</summary>
    public string TaskId { get; }

    public QuestRefusal Refusal { get; }

    /// <summary>Why it was refused, in words; null when it was made.</summary>
    public string Reason { get; }

    public bool Created => TaskId != null;

    public override string ToString() => Refusal == QuestRefusal.None ? "created " + TaskId : Refusal + ": " + Reason;
}

/// <summary>
/// Tasks made by mods. A mod's task is a real game task (it shows in the task panel, raises the game's
/// notifications, answers the game's Lua checks such as <c>TaskActive("MyMod.key")</c>) whose id is
/// "modId.key". It is saved by the game with everything else, so make it once (see
/// <see cref="StoryPump.Ready"/>) and read it back with <see cref="QuestLog.Get"/> later. Only the mod whose
/// id starts the task id can change it through this class. Call everything on the main thread.
/// </summary>
public static partial class QuestAuthoring
{
    /// <summary>A world with a task panel is loaded, so tasks can be made.</summary>
    public static bool IsAvailable => QuestLog.IsAvailable && TaskPanel.instance != null;

    /// <summary>The task id a key of this mod gets, or null when the mod id or the key is not allowed.</summary>
    public static string TaskId(ObenMod owner, string key) =>
        QuestIds.TaskId(owner?.Info.Id ?? throw new ArgumentNullException(nameof(owner)), key);

    /// <summary>
    /// Makes a task. Every objective starts at once (hidden ones as "???"), the player sees the game's
    /// "New Task" notification, and the definition is remembered in the save so the task can be rewound and its
    /// follow-up started later. Refused while a save runs, outside a world, for a task id that already exists in
    /// this save and for a title another task already has.
    /// </summary>
    public static QuestCreateResult Create(ObenMod owner, QuestDefinition definition)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        return Create(owner.Info.Id, definition);
    }

    internal static QuestCreateResult Create(string modId, QuestDefinition definition)
    {
        string problem = QuestPlan.Check(modId, definition);
        if (problem != null)
            return Refuse(QuestRefusal.BadInput, problem);

        var blocked = Blocker(out string why);
        if (blocked != QuestRefusal.None)
            return Refuse(blocked, why);

        var controller = TaskController.instance;
        string id = modId + "." + definition.Key;
        var conflict = Conflict(controller, id, definition.Title, null);
        if (conflict != QuestRefusal.None)
            return Refuse(conflict, conflict == QuestRefusal.AlreadyExists
                ? $"the save already has the task {id}"
                : $"another task is already called '{definition.Title}'");

        return Build(controller, modId, id, definition);
    }

    private static QuestCreateResult Build(TaskController controller, string modId, string id, QuestDefinition definition)
    {
        var task = ScriptableObject.CreateInstance<TaskInfo>();
        task.id = id;
        task.title = definition.Title;
        task.description = definition.Description ?? "";
        var objectives = new List<TaskObjective>(definition.Objectives.Count);
        foreach (var objective in definition.Objectives)
            objectives.Add(NewObjective(task, objective));

        int handed = 0;
        bool exists = false;
        try
        {
            for (int i = 0; i < objectives.Count; i++)
            {
                var source = definition.Objectives[i];
                bool ok = controller.EditTask(task, objectives[i], source.Hidden ? ObjectiveStatus.Hidden : ObjectiveStatus.Active,
                    source.Optional, createNewTask: i == 0, CurrentTaskStatus.Active);
                exists |= ok && i == 0;
                if (ok)
                {
                    handed = i + 1;
                    continue;
                }
                if (!exists)
                    return Refuse(QuestRefusal.Rejected, "the game refused the first objective");
                return Partial(id, $"the game refused objective '{source.Key}'");
            }
            return new QuestCreateResult(id, QuestRefusal.None, null);
        }
        catch (Exception e)
        {
            StoryLog.Failed("QuestAuthoring.Create", e);
            exists |= QuestLog.Find(id) != null;
            return exists ? Partial(id, "a game call threw while the objectives were handed out") : Refuse(QuestRefusal.Failed, "a game call threw: " + e.Message);
        }
        finally
        {
            if (!exists)
            {
                handed = 0;
                UnityEngine.Object.Destroy(task);
            }
            else
            {
                // Set only now: with it on, a first hidden objective would have finished the task at once.
                task.finishTaskWhenTheresNoActiveTasks = definition.FinishWhenDone;
                QuestRegistry.Book.Remember(modId, definition);
                QuestWatcher.Poke();
            }
            for (int i = handed; i < objectives.Count; i++)
                UnityEngine.Object.Destroy(objectives[i]);
        }
    }

    private static TaskObjective NewObjective(TaskInfo task, QuestObjectiveDefinition source)
    {
        var objective = ScriptableObject.CreateInstance<TaskObjective>();
        objective.id = source.Key;
        objective.title = QuestPlan.StoredTitle(source.Hidden, source.Title);
        objective.description = QuestPlan.StoredDescription(source.Hidden, source.Description);
        objective.counterAmount = source.Target;
        objective.task = task;
        return objective;
    }

    /// <summary>Whether quests can be written right now, and why not.</summary>
    internal static QuestRefusal Blocker(out string reason)
    {
        if (WorldState.IsLoadingSave || SaveGate.IsBusy)
        {
            reason = "a save is being written or loaded";
            return QuestRefusal.Busy;
        }
        if (QuestPatches.InGameCall)
        {
            reason = "called from inside one of the game's own task edits; use StoryPump.Defer";
            return QuestRefusal.Busy;
        }
        if (TaskController.instance == null || TaskPanel.instance == null || Notifications.instance == null)
        {
            reason = "no world with a task panel is loaded";
            return QuestRefusal.NotReady;
        }
        reason = null;
        return QuestRefusal.None;
    }

    /// <summary>Another task with this id, or another task (started or loaded) with this title.</summary>
    internal static QuestRefusal Conflict(TaskController controller, string id, string title, TaskStatusInfo same)
    {
        foreach (var entry in controller.allTasks)
        {
            if (entry == null || entry.task == null || entry == same)
                continue;
            if (entry.Id == id)
                return QuestRefusal.AlreadyExists;
            if (entry.task.title == title)
                return QuestRefusal.DuplicateTitle;
        }
        foreach (var other in Resources.FindObjectsOfTypeAll<TaskInfo>())
        {
            if (other != null && other.title == title && other.Id != id)
                return QuestRefusal.DuplicateTitle;
        }
        return QuestRefusal.None;
    }

    private static QuestCreateResult Refuse(QuestRefusal refusal, string reason)
    {
        StoryLog.Refused("QuestAuthoring.Create", reason);
        return new QuestCreateResult(null, refusal, reason);
    }

    private static QuestCreateResult Partial(string id, string reason)
    {
        StoryLog.Refused("QuestAuthoring.Create " + id, reason);
        return new QuestCreateResult(id, QuestRefusal.PartiallyCreated, reason);
    }
}
