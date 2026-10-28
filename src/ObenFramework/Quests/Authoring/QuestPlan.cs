// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.Quests;

/// <summary>
/// The rules a task definition has to meet before anything touches the game, with a reason a modder can
/// act on. No Unity types, so it is unit tested on its own.
/// </summary>
internal static class QuestPlan
{
    /// <summary>The game's objective status numbers (ObjectiveStatus), which these rules work with.</summary>
    public const int Active = 0;
    public const int Hidden = 4;

    /// <summary>Most tasks in one follow-up chain, the first one included.</summary>
    public const int MaxChain = 8;

    /// <summary>Checks a definition and its follow-up chain. Returns null when it is fine, else why not.</summary>
    public static string Check(string modId, QuestDefinition definition)
    {
        if (!QuestIds.ValidModId(modId))
            return $"the mod id '{modId}' can't be part of a task id; it needs 1 to {QuestIds.MaxModId} letters, digits, '_', '-' or '.'";
        if (definition == null)
            return "the definition is null";
        var keys = new List<string>();
        return CheckChain(definition, 1, keys);
    }

    private static string CheckChain(QuestDefinition definition, int depth, List<string> keys)
    {
        if (depth > MaxChain)
            return $"the follow-up chain is longer than {MaxChain} tasks";
        string own = CheckOwn(definition);
        if (own != null)
            return depth == 1 ? own : $"follow-up '{definition.Key}': {own}";
        if (keys.Contains(definition.Key))
            return null; // a chain that comes back to an earlier task ends there
        keys.Add(definition.Key);
        return definition.NextDefinition == null ? null : CheckChain(definition.NextDefinition, depth + 1, keys);
    }

    private static string CheckOwn(QuestDefinition definition)
    {
        if (!QuestIds.ValidKey(definition.Key))
            return $"the key '{definition.Key}' needs 1 to {QuestIds.MaxKey} letters, digits, '_' or '-'";
        if (!QuestIds.ValidText(definition.Title, QuestIds.MaxTitle, required: true, multiline: false))
            return $"the title must be 1 to {QuestIds.MaxTitle} characters on one line";
        if (!QuestIds.ValidText(definition.Description, QuestIds.MaxDescription, required: false, multiline: true))
            return $"the description must be at most {QuestIds.MaxDescription} characters without control characters";
        if (!string.IsNullOrEmpty(definition.Next) && !QuestIds.ValidKey(definition.Next))
            return $"the follow-up key '{definition.Next}' is not a valid key";
        if (definition.NextDefinition != null && !string.IsNullOrEmpty(definition.Next) && definition.Next != definition.NextDefinition.Key)
            return "Next and NextDefinition.Key name different tasks";

        var objectives = definition.Objectives;
        if (objectives == null || objectives.Count < 1 || objectives.Count > QuestIds.MaxObjectives)
            return $"a task needs 1 to {QuestIds.MaxObjectives} objectives";
        var keys = new List<string>(objectives.Count);
        foreach (var objective in objectives)
        {
            string problem = CheckObjective(objective);
            if (problem != null)
                return problem;
            if (keys.Contains(objective.Key))
                return $"two objectives use the key '{objective.Key}'";
            keys.Add(objective.Key);
        }
        return null;
    }

    /// <summary>Checks one objective. Null when it is fine.</summary>
    public static string CheckObjective(QuestObjectiveDefinition objective)
    {
        if (objective == null)
            return "an objective is null";
        if (!QuestIds.ValidKey(objective.Key))
            return $"the objective key '{objective.Key}' needs 1 to {QuestIds.MaxKey} letters, digits, '_' or '-'";
        if (!QuestIds.ValidText(objective.Title, QuestIds.MaxTitle, required: !objective.Hidden, multiline: false))
            return $"objective '{objective.Key}' needs a title of 1 to {QuestIds.MaxTitle} characters on one line (only hidden ones may leave it out)";
        if (!QuestIds.ValidText(objective.Description, QuestIds.MaxDescription, required: false, multiline: true))
            return $"objective '{objective.Key}' has a description over {QuestIds.MaxDescription} characters or with control characters";
        if (objective.Target < 0 || objective.Target > QuestIds.MaxTarget)
            return $"objective '{objective.Key}' needs a counter target from 0 to {QuestIds.MaxTarget}";
        if (!QuestIds.ValidItem(objective.ItemId, objective.Count))
            return $"objective '{objective.Key}' needs both an item id and a count from 1 to {QuestIds.MaxTarget}, or neither";
        return null;
    }

    /// <summary>The follow-up's key: <see cref="QuestDefinition.Next"/>, else the key of <see cref="QuestDefinition.NextDefinition"/>.</summary>
    public static string NextKey(QuestDefinition definition)
    {
        if (definition == null)
            return null;
        if (!string.IsNullOrEmpty(definition.Next))
            return definition.Next;
        return definition.NextDefinition?.Key;
    }

    /// <summary>The title the game stores for an objective: the placeholder while it is hidden.</summary>
    public static string StoredTitle(bool hidden, string title) => hidden ? QuestIds.Placeholder : title;

    public static string StoredDescription(bool hidden, string description) => hidden ? "" : description ?? "";

    /// <summary>Only a hidden objective can be revealed.</summary>
    public static bool CanReveal(int status) => status == Hidden;

    /// <summary>A revealed counting objective that already reached its target finishes, like the game's counter does.</summary>
    public static bool ShouldFinishOnReveal(int current, int target, bool noSelfComplete) =>
        target > 0 && current >= target && !noSelfComplete;
}
