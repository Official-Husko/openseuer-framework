// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Story;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Quests;

/// <summary>
/// Named copies of a mod task's state (task status, each step's status, counter and text) that can be put
/// back later. They live in memory for the session: a load, a new game or the main menu drops them.
/// </summary>
internal static class QuestCheckpoints
{
    public const int MaxPerTask = 16;

    private sealed class Point
    {
        public string Name;
        public CurrentTaskStatus TaskStatus;
        public readonly List<string> Keys = new List<string>();
        public readonly List<ObjectiveStatus> Statuses = new List<ObjectiveStatus>();
        public readonly List<int> Amounts = new List<int>();
        public readonly List<string> Titles = new List<string>();
        public readonly List<string> Descriptions = new List<string>();
        public Action Restored;
        public ObenMod RestoredOwner;
    }

    private static readonly Dictionary<string, List<Point>> Points = new Dictionary<string, List<Point>>(StringComparer.Ordinal);
    private static readonly HashSet<ObenMod> TrackedOwners = new HashSet<ObenMod>();

    public static void Init(ObenMod framework)
    {
        StoryPump.Ready.Subscribe(framework, _ => Clear());
        framework.Track(Disposable.Create(Clear));
    }

    /// <summary>Keeps the task's state under <paramref name="name"/>, replacing an older copy of that name.</summary>
    public static QuestResult Save(ObenMod owner, TaskStatusInfo entry, string name, Action restored)
    {
        if (!QuestIds.ValidCheckpointName(name))
            return QuestResult.Unchanged($"A checkpoint name is 1 to {QuestIds.MaxCheckpointName} letters, digits or '-'.");
        if (!Points.TryGetValue(entry.Id, out var list))
            Points[entry.Id] = list = new List<Point>();

        var point = list.Find(p => p.Name == name);
        if (point == null)
        {
            point = new Point { Name = name };
            list.Add(point);
            if (list.Count > MaxPerTask)
                list.RemoveAt(0);
        }
        point.TaskStatus = entry.status;
        point.Keys.Clear();
        point.Statuses.Clear();
        point.Amounts.Clear();
        point.Titles.Clear();
        point.Descriptions.Clear();
        foreach (var objective in entry.objectives)
        {
            point.Keys.Add(objective.objective.Id);
            point.Statuses.Add(objective.status);
            point.Amounts.Add(objective.currentAmount);
            point.Titles.Add(objective.objective.title);
            point.Descriptions.Add(objective.objective.description);
        }
        point.Restored = restored;
        point.RestoredOwner = restored != null ? owner : null;
        if (restored != null && TrackedOwners.Add(owner))
            owner.Track(Disposable.Create(() => DropCallbacks(owner)));
        return QuestResult.Done;
    }

    /// <summary>Puts a kept copy back, then runs its callback (once per restore).</summary>
    public static QuestResult Restore(TaskStatusInfo entry, string name)
    {
        var point = Points.TryGetValue(entry.Id, out var list) ? list.Find(p => p.Name == name) : null;
        if (point == null)
            return QuestResult.Unchanged($"No checkpoint '{name}' was kept for this task in this session.");

        string from = QuestRewind.ActiveKey(entry);
        var record = QuestRegistry.Find(entry.Id);
        var controller = TaskController.instance;
        entry.status = point.TaskStatus;
        QuestRewind.TaskLists(controller, entry.Id, (int)point.TaskStatus);
        foreach (var objective in entry.objectives)
        {
            int saved = point.Keys.IndexOf(objective.objective.Id);
            if (saved >= 0)
            {
                objective.status = point.Statuses[saved];
                objective.currentAmount = point.Amounts[saved];
                QuestRewind.ApplyText(objective, QuestBook.FindObjective(record, objective.objective.Id), (int)objective.status,
                    point.Titles[saved], point.Descriptions[saved]);
            }
            QuestRewind.ObjectiveLists(controller, entry.Id, objective.objective.Id, (int)objective.status);
        }
        var result = QuestRewind.Finish(entry, QuestRewindKind.Checkpoint, from);
        if (point.Restored != null)
        {
            try { point.Restored(); }
            catch (Exception e) { StoryLog.Failed($"Checkpoint '{name}' callback", e); }
        }
        return result;
    }

    /// <summary>The names kept for a task, oldest first.</summary>
    public static List<string> Names(string taskId)
    {
        var names = new List<string>();
        if (Points.TryGetValue(taskId, out var list))
        {
            foreach (var point in list)
                names.Add(point.Name);
        }
        return names;
    }

    public static void Forget(string taskId) => Points.Remove(taskId);

    private static void Clear()
    {
        Points.Clear();
        TrackedOwners.Clear();
    }

    private static void DropCallbacks(ObenMod owner)
    {
        TrackedOwners.Remove(owner);
        foreach (var list in Points.Values)
        {
            foreach (var point in list)
            {
                if (point.RestoredOwner == owner)
                {
                    point.Restored = null;
                    point.RestoredOwner = null;
                }
            }
        }
    }
}
