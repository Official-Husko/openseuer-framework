namespace ObenFramework.Quests;

/// <summary>A step of a task, as it is right now. A copy: it doesn't follow later changes.</summary>
public sealed class QuestObjective
{
    internal QuestObjective(string id, string title, string description, ObjectiveStatus status, bool optional, int current, int target)
    {
        Id = id;
        Title = title;
        Description = description;
        Status = status;
        Optional = optional;
        Current = current;
        Target = target;
    }

    /// <summary>The id the game's Lua functions and console use (<c>ObjectiveActive(task, id)</c>).</summary>
    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public ObjectiveStatus Status { get; }

    public bool Optional { get; }

    /// <summary>Progress of a counting objective ("collect 3 things"); 0 when it doesn't count.</summary>
    public int Current { get; }

    /// <summary>The goal of a counting objective, 0 when it doesn't count.</summary>
    public int Target { get; }

    public bool HasCounter => Target > 0;
}

/// <summary>A task (quest) the player has started, with its objectives. A copy: it doesn't follow later changes.</summary>
public sealed class QuestTask
{
    internal QuestTask(string id, string title, string description, CurrentTaskStatus status, QuestObjective[] objectives)
    {
        Id = id;
        Title = title;
        Description = description;
        Status = status;
        Objectives = objectives;
    }

    /// <summary>The id the game's Lua functions and console use (<c>TaskActive(id)</c>).</summary>
    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public CurrentTaskStatus Status { get; }

    /// <summary>The objectives the task has handed out so far, in the game's order (active ones first).</summary>
    public QuestObjective[] Objectives { get; }
}

/// <summary>A task the game has loaded but the player has not started.</summary>
public readonly struct AvailableTask
{
    internal AvailableTask(string id, string title)
    {
        Id = id;
        Title = title;
    }

    public string Id { get; }

    public string Title { get; }
}

/// <summary>A task started or changed status. See <see cref="Quests.TaskChanged"/>.</summary>
public readonly struct TaskChange
{
    internal TaskChange(string taskId, string title, bool isNew, CurrentTaskStatus previous, CurrentTaskStatus current)
    {
        TaskId = taskId;
        Title = title;
        IsNew = isNew;
        Previous = previous;
        Current = current;
    }

    public string TaskId { get; }

    public string Title { get; }

    /// <summary>The task was just started; <see cref="Previous"/> means nothing then.</summary>
    public bool IsNew { get; }

    public CurrentTaskStatus Previous { get; }

    public CurrentTaskStatus Current { get; }
}

/// <summary>An objective appeared, changed status or made progress. See <see cref="Quests.ObjectiveChanged"/>.</summary>
public readonly struct ObjectiveChange
{
    internal ObjectiveChange(string taskId, string objectiveId, string title, bool isNew,
        ObjectiveStatus previous, ObjectiveStatus current, int previousAmount, int amount, int target)
    {
        TaskId = taskId;
        ObjectiveId = objectiveId;
        Title = title;
        IsNew = isNew;
        Previous = previous;
        Current = current;
        PreviousAmount = previousAmount;
        Amount = amount;
        Target = target;
    }

    public string TaskId { get; }

    public string ObjectiveId { get; }

    public string Title { get; }

    /// <summary>The objective was just handed out; <see cref="Previous"/> and <see cref="PreviousAmount"/> mean nothing then.</summary>
    public bool IsNew { get; }

    public ObjectiveStatus Previous { get; }

    public ObjectiveStatus Current { get; }

    public int PreviousAmount { get; }

    /// <summary>Progress of a counting objective now; see <see cref="QuestObjective.Current"/>.</summary>
    public int Amount { get; }

    public int Target { get; }

    /// <summary>False when only the counter moved.</summary>
    public bool StatusChanged => IsNew || Previous != Current;
}

/// <summary>
/// Whether a change to the quest state happened. The game's own task methods return nothing or only
/// whether they ran, and silently do nothing when a requirement isn't met, so the framework checks the
/// state afterwards and says so here.
/// </summary>
public readonly struct QuestResult
{
    private QuestResult(bool changed, string reason)
    {
        Changed = changed;
        Reason = reason;
    }

    /// <summary>The state is different now.</summary>
    public bool Changed { get; }

    /// <summary>Why nothing changed; null when <see cref="Changed"/>.</summary>
    public string Reason { get; }

    internal static QuestResult Done => new QuestResult(true, null);

    internal static QuestResult Unchanged(string reason) => new QuestResult(false, reason);

    public override string ToString() => Changed ? "changed" : "unchanged: " + Reason;
}
