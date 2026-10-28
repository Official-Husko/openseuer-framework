// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Quests;

/// <summary>The text an objective gets after a rewind; <see cref="HasText"/> false leaves it alone.</summary>
internal struct ObjectiveTextPlan
{
    public bool HasText;
    public string Title;
    public string Description;
}

/// <summary>
/// What a rewind does to the game's task and objective lists. The game answers its Lua questions
/// (<c>TaskActive</c>, <c>ObjectiveCompleted</c> ...) from plain string lists it only ever appends to, so
/// sending a task back means rewriting those lists by hand. Status numbers are the game's own
/// (Active 0, Failed 1, Finished 2, Abandoned 3, Hidden 4). No Unity types, so it is unit tested on its own.
/// </summary>
internal static class QuestRewindPlan
{
    public const int Active = 0;
    public const int Failed = 1;
    public const int Finished = 2;
    public const int Abandoned = 3;
    public const int Hidden = 4;

    /// <summary>The task or objective is taken out of every list.</summary>
    public const int Gone = -1;

    /// <summary>Puts a task id into the lists that match <paramref name="status"/> and takes it out of the others.</summary>
    public static void TaskLists(IList<string> any, IList<string> active, IList<string> failed, IList<string> abandoned,
        IList<string> finished, string id, int status)
    {
        Rewrite(any, id, status != Gone);
        Rewrite(active, id, status == Active);
        Rewrite(failed, id, status == Failed);
        Rewrite(abandoned, id, status == Abandoned);
        Rewrite(finished, id, status == Finished);
    }

    /// <summary>The same for an objective entry ("taskId_objectiveId"). A hidden objective is in no list, as in the game.</summary>
    public static void ObjectiveLists(IList<string> any, IList<string> active, IList<string> failed, IList<string> abandoned,
        IList<string> completed, string entry, int status)
    {
        bool listed = status >= Active && status <= Abandoned;
        Rewrite(any, entry, listed);
        Rewrite(active, entry, status == Active);
        Rewrite(failed, entry, status == Failed);
        Rewrite(abandoned, entry, status == Abandoned);
        Rewrite(completed, entry, status == Finished);
    }

    /// <summary>Removes every copy of <paramref name="id"/> and adds one back when <paramref name="include"/>.</summary>
    public static void Rewrite(IList<string> list, string id, bool include)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (string.Equals(list[i], id, StringComparison.Ordinal))
                list.RemoveAt(i);
        }
        if (include)
            list.Add(id);
    }

    /// <summary>The status an objective starts in.</summary>
    public static int StartStatus(bool hidden) => hidden ? Hidden : Active;

    /// <summary>Jumping to objective <paramref name="chosen"/>: the ones before it are finished, it is active, the rest start over.</summary>
    public static int JumpStatus(int index, int chosen, int start)
    {
        if (index < chosen)
            return Finished;
        return index == chosen ? Active : start;
    }

    /// <summary>Counters of the objectives before the chosen one are full, the rest are empty.</summary>
    public static int JumpAmount(int index, int chosen, int target) => index < chosen ? target : 0;

    /// <summary>
    /// The text to give an objective after a rewind: the placeholder while hidden, else the remembered text
    /// (from the definition record), else the text captured in a checkpoint, else none.
    /// </summary>
    public static ObjectiveTextPlan Text(int status, bool fromRecord, string recordTitle, string recordDescription,
        string capturedTitle, string capturedDescription)
    {
        var plan = new ObjectiveTextPlan();
        if (status == Hidden)
        {
            plan.HasText = true;
            plan.Title = QuestIds.Placeholder;
            plan.Description = "";
            return plan;
        }
        if (fromRecord)
        {
            plan.HasText = true;
            plan.Title = recordTitle ?? "";
            plan.Description = recordDescription ?? "";
            return plan;
        }
        if (capturedTitle != null)
        {
            plan.HasText = true;
            plan.Title = capturedTitle;
            plan.Description = capturedDescription ?? "";
        }
        return plan;
    }
}
