using System;
using System.Collections.Generic;

namespace ObenFramework.Quests;

/// <summary>
/// A remembered copy of the quest state, compared against the live state to find what changed. The
/// game's TaskUpdated and ObjectiveUpdated events carry nothing, so this is the only way to know.
/// Entries are reused between passes; memory is only allocated when a task or objective appears.
/// </summary>
internal sealed class QuestSnapshot
{
    private sealed class ObjectiveEntry
    {
        public ObjectiveStatus Status;
        public int Amount;
        public int Seen;
    }

    private sealed class TaskEntry
    {
        public CurrentTaskStatus Status;
        public int Seen;
        public readonly Dictionary<string, ObjectiveEntry> Objectives = new Dictionary<string, ObjectiveEntry>(StringComparer.Ordinal);
    }

    private readonly Dictionary<string, TaskEntry> _tasks = new Dictionary<string, TaskEntry>(StringComparer.Ordinal);
    private readonly List<string> _stale = new List<string>();
    private int _pass;

    /// <summary>Forgets everything; the next <see cref="Update"/> only records.</summary>
    public void Clear() => _tasks.Clear();

    /// <summary>
    /// Compares the controller's state with the remembered one and remembers it. Changes go into the two
    /// lists (cleared first) when they are given, otherwise this just records the state.
    /// </summary>
    public void Update(TaskController controller, List<TaskChange> taskChanges, List<ObjectiveChange> objectiveChanges)
    {
        taskChanges?.Clear();
        objectiveChanges?.Clear();
        _pass++;

        var tasks = controller.allTasks;
        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            if (task == null || task.task == null)
                continue;
            UpdateTask(task, taskChanges, objectiveChanges);
        }
        RemoveUnseen();
    }

    private void UpdateTask(TaskStatusInfo task, List<TaskChange> taskChanges, List<ObjectiveChange> objectiveChanges)
    {
        string id = task.Id;
        bool known = _tasks.TryGetValue(id, out var entry);
        if (!known)
        {
            entry = new TaskEntry { Status = task.status };
            _tasks[id] = entry;
        }
        entry.Seen = _pass;

        if (taskChanges != null && (!known || entry.Status != task.status))
            taskChanges.Add(new TaskChange(id, task.task.title, !known, entry.Status, task.status));
        entry.Status = task.status;

        var objectives = task.objectives;
        for (int i = 0; i < objectives.Count; i++)
        {
            var objective = objectives[i];
            if (objective == null || objective.objective == null)
                continue;
            UpdateObjective(id, entry, objective, objectiveChanges);
        }

        if (entry.Objectives.Count > 0)
            RemoveUnseenObjectives(entry);
    }

    private void UpdateObjective(string taskId, TaskEntry task, ObjectiveStatusInfo objective, List<ObjectiveChange> changes)
    {
        string id = objective.objective.Id;
        bool known = task.Objectives.TryGetValue(id, out var entry);
        if (!known)
        {
            entry = new ObjectiveEntry { Status = objective.status, Amount = objective.currentAmount };
            task.Objectives[id] = entry;
        }
        entry.Seen = _pass;

        if (changes != null && (!known || entry.Status != objective.status || entry.Amount != objective.currentAmount))
        {
            changes.Add(new ObjectiveChange(taskId, id, objective.objective.title, !known, entry.Status, objective.status,
                entry.Amount, objective.currentAmount, objective.counterAmount));
        }
        entry.Status = objective.status;
        entry.Amount = objective.currentAmount;
    }

    // Tasks and objectives that vanished (a save loaded over them) are dropped without a report.
    private void RemoveUnseen()
    {
        _stale.Clear();
        foreach (var pair in _tasks)
        {
            if (pair.Value.Seen != _pass)
                _stale.Add(pair.Key);
        }
        for (int i = 0; i < _stale.Count; i++)
            _tasks.Remove(_stale[i]);
    }

    private void RemoveUnseenObjectives(TaskEntry task)
    {
        _stale.Clear();
        foreach (var pair in task.Objectives)
        {
            if (pair.Value.Seen != _pass)
                _stale.Add(pair.Key);
        }
        for (int i = 0; i < _stale.Count; i++)
            task.Objectives.Remove(_stale[i]);
    }
}
