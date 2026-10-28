using ObenFramework.Events;

namespace ObenFramework.Quests;

/// <summary>What a rewind of a mod's task did. See <see cref="QuestLog.Rewound"/>.</summary>
public enum QuestRewindKind
{
    /// <summary>The task's or a step's title changed.</summary>
    Title,

    /// <summary>The task's description changed.</summary>
    Description,

    /// <summary>A closed task was sent back to its start.</summary>
    Restart,

    /// <summary>A task was sent back to its start from any state.</summary>
    Reset,

    /// <summary>The task was taken out of the log.</summary>
    Remove,

    /// <summary>One step was made the active one, the steps before it finished.</summary>
    JumpTo,

    /// <summary>A kept checkpoint was put back.</summary>
    Checkpoint,
}

/// <summary>A mod's task was rewound. See <see cref="QuestLog.Rewound"/>.</summary>
public readonly struct QuestRewound
{
    internal QuestRewound(string taskId, QuestRewindKind kind, string from, string to)
    {
        TaskId = taskId;
        Kind = kind;
        From = from;
        To = to;
    }

    public string TaskId { get; }

    public QuestRewindKind Kind { get; }

    /// <summary>The first active step before the rewind; null for title, description and remove changes.</summary>
    public string From { get; }

    /// <summary>The first active step after the rewind; null for title, description and remove changes.</summary>
    public string To { get; }
}

public static partial class QuestLog
{
    /// <summary>
    /// A mod's task was rewound through <see cref="QuestAuthoring"/> (restart, reset, remove, jump, checkpoint,
    /// new title or description). These edits rewrite the game's lists directly, so <see cref="TaskChanged"/>
    /// and <see cref="ObjectiveChanged"/> may not describe them; this does. Raised right after the change.
    /// Each handler runs in its own try/catch.
    /// </summary>
    public static readonly GameEvent<QuestRewound> Rewound = new GameEvent<QuestRewound>(nameof(Rewound));

    internal static void RaiseRewound(string taskId, QuestRewindKind kind, string from = null, string to = null) =>
        Rewound.Raise(new QuestRewound(taskId, kind, from, to), GameEvents.Log);
}
