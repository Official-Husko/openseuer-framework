// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.Quests;

/// <summary>A remembered objective definition.</summary>
internal sealed class QuestObjectiveRecord
{
    public string Key;
    public string Title;
    public string Description;
    public int Target;
    public bool Optional;
    public bool Hidden;
    public int ItemId;
    public int Count;
}

/// <summary>A remembered task definition, keyed by task id.</summary>
internal sealed class QuestRecord
{
    public string TaskId;
    public string ModId;
    public string Key;
    public string Title;
    public string Description;
    public string NextKey;
    public bool FinishWhenDone;
    public List<QuestObjectiveRecord> Objectives = new List<QuestObjectiveRecord>();
}

/// <summary>
/// The definitions of every task mods made in one save, stored next to the save by the framework (see
/// <see cref="QuestRegistry"/>). The game's own save keeps the task itself; this keeps what a rewind needs to
/// put it back (the original texts, which steps were hidden, what a hand-in takes) and the follow-up chain.
/// No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class QuestBook
{
    public List<QuestRecord> Quests = new List<QuestRecord>();

    internal QuestRecord Find(string taskId)
    {
        if (string.IsNullOrEmpty(taskId) || Quests == null)
            return null;
        foreach (var record in Quests)
        {
            if (record != null && record.TaskId == taskId)
                return record;
        }
        return null;
    }

    internal static QuestObjectiveRecord FindObjective(QuestRecord record, string key)
    {
        if (record?.Objectives == null || string.IsNullOrEmpty(key))
            return null;
        foreach (var objective in record.Objectives)
        {
            if (objective != null && objective.Key == key)
                return objective;
        }
        return null;
    }

    /// <summary>Stores a definition and the follow-ups given with it that aren't stored yet.</summary>
    internal void Remember(string modId, QuestDefinition definition)
    {
        if (definition == null)
            return;
        Store(modId, definition);
        var current = definition;
        for (int depth = 1; depth < QuestPlan.MaxChain; depth++)
        {
            var next = current.NextDefinition;
            string key = QuestPlan.NextKey(current);
            if (next == null || string.IsNullOrEmpty(key) || key != next.Key || Find(modId + "." + key) != null)
                return;
            Store(modId, next);
            current = next;
        }
    }

    internal void RememberObjective(string taskId, QuestObjectiveDefinition source)
    {
        var record = Record(taskId, create: true);
        record.Objectives ??= new List<QuestObjectiveRecord>();
        var existing = FindObjective(record, source.Key);
        if (existing != null)
            record.Objectives.Remove(existing);
        record.Objectives.Add(Copy(source));
    }

    /// <summary>Changes the remembered text of a task (null <paramref name="objectiveKey"/>) or one of its objectives.</summary>
    internal void RememberText(string taskId, string objectiveKey, string title, string description)
    {
        var record = Record(taskId, create: false);
        if (record == null)
            return;
        if (string.IsNullOrEmpty(objectiveKey))
        {
            record.Title = title;
            record.Description = description;
            return;
        }
        var objective = FindObjective(record, objectiveKey);
        if (objective == null)
            return;
        objective.Title = title;
        objective.Description = description;
    }

    internal void Forget(string taskId)
    {
        var record = Record(taskId, create: false);
        if (record != null)
            Quests.Remove(record);
    }

    /// <summary>Builds the definition back from a record, with the stored follow-ups attached.</summary>
    internal QuestDefinition ToDefinition(QuestRecord record) => ToDefinition(record, QuestPlan.MaxChain);

    private QuestDefinition ToDefinition(QuestRecord record, int depth)
    {
        if (record == null || string.IsNullOrEmpty(record.Key) || string.IsNullOrEmpty(record.Title))
            return null;
        var definition = new QuestDefinition
        {
            Key = record.Key,
            Title = record.Title,
            Description = record.Description,
            Next = record.NextKey,
            FinishWhenDone = record.FinishWhenDone,
        };
        if (record.Objectives != null)
        {
            foreach (var saved in record.Objectives)
            {
                if (saved == null)
                    continue;
                definition.Objectives.Add(new QuestObjectiveDefinition
                {
                    Key = saved.Key,
                    Title = saved.Title,
                    Description = saved.Description,
                    Target = saved.Target,
                    Optional = saved.Optional,
                    Hidden = saved.Hidden,
                    ItemId = saved.ItemId,
                    Count = saved.Count,
                });
            }
        }
        if (depth > 1 && !string.IsNullOrEmpty(record.NextKey) && !string.IsNullOrEmpty(record.ModId))
        {
            var next = Find(record.ModId + "." + record.NextKey);
            if (next != null && next != record && next.Key == record.NextKey)
                definition.NextDefinition = ToDefinition(next, depth - 1);
        }
        return definition;
    }

    private void Store(string modId, QuestDefinition definition)
    {
        var record = Record(modId + "." + definition.Key, create: true);
        record.ModId = modId;
        record.Key = definition.Key;
        record.Title = definition.Title;
        record.Description = definition.Description;
        record.NextKey = QuestPlan.NextKey(definition);
        record.FinishWhenDone = definition.FinishWhenDone;
        record.Objectives ??= new List<QuestObjectiveRecord>();
        record.Objectives.Clear();
        if (definition.Objectives == null)
            return;
        foreach (var source in definition.Objectives)
        {
            if (source != null)
                record.Objectives.Add(Copy(source));
        }
    }

    private QuestRecord Record(string taskId, bool create)
    {
        Quests ??= new List<QuestRecord>();
        var found = Find(taskId);
        if (found != null || !create)
            return found;
        var record = new QuestRecord { TaskId = taskId };
        Quests.Add(record);
        return record;
    }

    private static QuestObjectiveRecord Copy(QuestObjectiveDefinition source) => new QuestObjectiveRecord
    {
        Key = source.Key,
        Title = source.Title,
        Description = source.Description,
        Target = source.Target,
        Optional = source.Optional,
        Hidden = source.Hidden,
        ItemId = source.ItemId,
        Count = source.Count,
    };
}
