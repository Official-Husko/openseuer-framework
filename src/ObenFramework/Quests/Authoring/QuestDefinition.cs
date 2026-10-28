// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.Quests;

/// <summary>A task a mod wants to make with <see cref="QuestAuthoring.Create"/>.</summary>
public sealed class QuestDefinition
{
    /// <summary>The task's key: 1 to 48 letters, digits, '_' or '-'. The task id is "modId.key".</summary>
    public string Key { get; set; }

    /// <summary>Up to 100 characters. Must not be the title of another task.</summary>
    public string Title { get; set; }

    /// <summary>Up to 1000 characters, newlines allowed. Can be null.</summary>
    public string Description { get; set; }

    /// <summary>
    /// The task finishes by itself once none of its objectives is active, like most of the game's tasks.
    /// Hidden objectives don't count as active. When false, finish it with <see cref="QuestAuthoring.Finish"/>.
    /// </summary>
    public bool FinishWhenDone { get; set; }

    /// <summary>1 to 12 steps. They all start at once (hidden ones stay hidden until revealed).</summary>
    public List<QuestObjectiveDefinition> Objectives { get; set; } = new List<QuestObjectiveDefinition>();

    /// <summary>
    /// The key of a follow-up task of the same mod, started when this one is finished. Give the follow-up's
    /// definition in <see cref="NextDefinition"/> the first time; it is remembered in the save after that.
    /// </summary>
    public string Next { get; set; }

    /// <summary>The follow-up task's definition, which can have its own follow-up (8 tasks in a chain at most).</summary>
    public QuestDefinition NextDefinition { get; set; }
}

/// <summary>One step of a mod's task.</summary>
public sealed class QuestObjectiveDefinition
{
    /// <summary>The step's id inside the task: 1 to 48 letters, digits, '_' or '-'.</summary>
    public string Key { get; set; }

    /// <summary>The text the player sees, up to 100 characters. Can be null for a hidden step.</summary>
    public string Title { get; set; }

    /// <summary>More text, up to 1000 characters. Can be null.</summary>
    public string Description { get; set; }

    /// <summary>A counter target from 1 to 9999 ("collect 5"), or 0 for no counter.</summary>
    public int Target { get; set; }

    /// <summary>The player doesn't have to do this step.</summary>
    public bool Optional { get; set; }

    /// <summary>The step shows as "???" until <see cref="QuestAuthoring.Reveal"/> gives it its text.</summary>
    public bool Hidden { get; set; }

    /// <summary>An item the step asks for, handed in with <see cref="QuestAuthoring.HandIn"/>; 0 for none.</summary>
    public int ItemId { get; set; }

    /// <summary>How many of <see cref="ItemId"/> a hand-in takes (1 to 9999), 0 when there is no item.</summary>
    public int Count { get; set; }
}

/// <summary>Why <see cref="QuestAuthoring.Create"/> did not make a task.</summary>
public enum QuestRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No world with a task panel is loaded. Create quests from <see cref="Story.StoryPump.Ready"/>.</summary>
    NotReady,

    /// <summary>A save or load is running, or the call came from inside one of the game's quest calls. Try again later.</summary>
    Busy,

    /// <summary>A key, text, counter or item breaks the rules; the reason says which.</summary>
    BadInput,

    /// <summary>Another task already has this title; the task panel would show two of them.</summary>
    DuplicateTitle,

    /// <summary>The save already has a task with this id (tasks live in the save). Read it with <see cref="QuestLog.Get"/>.</summary>
    AlreadyExists,

    /// <summary>The game refused the first objective, so no task was made.</summary>
    Rejected,

    /// <summary>A game call threw before the task was made; it is logged.</summary>
    Failed,

    /// <summary>The task was made but a later objective was refused. Add it with <see cref="QuestAuthoring.AddObjective"/>.</summary>
    PartiallyCreated,
}
