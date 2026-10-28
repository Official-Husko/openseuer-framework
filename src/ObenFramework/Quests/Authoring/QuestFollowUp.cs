// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Story;
using ObenLoader;
using ObenLoader.Mods;

namespace ObenFramework.Quests;

/// <summary>
/// Starts a mod task's follow-up (<see cref="QuestDefinition.Next"/>) when the task is finished. The
/// follow-up's definition comes from the save, so it works in a later session too, but only while the mod
/// that made it is loaded. At most 8 follow-ups start in one session, which stops a chain that loops.
/// </summary>
internal static class QuestFollowUp
{
    private static readonly List<string> Started = new List<string>();

    public static void Init(ObenMod framework)
    {
        QuestLog.TaskChanged.Subscribe(framework, OnTaskChanged);
        StoryPump.Ready.Subscribe(framework, _ => Started.Clear());
    }

    /// <summary>The follow-ups started in this session, oldest first.</summary>
    public static IReadOnlyList<string> StartedThisSession => Started;

    private static void OnTaskChanged(TaskChange change)
    {
        if (change.Current != CurrentTaskStatus.Finished)
            return;
        var record = QuestRegistry.Find(change.TaskId);
        if (record == null || string.IsNullOrEmpty(record.ModId) || string.IsNullOrEmpty(record.NextKey))
            return;
        // Out of the event, so the follow-up's own "New Task" comes after the game is done with this one.
        StoryPump.Enqueue(null, "Quest follow-up of " + change.TaskId, () => StartNext(change.TaskId));
    }

    private static void StartNext(string finishedId)
    {
        var record = QuestRegistry.Find(finishedId);
        if (record == null)
            return;
        string targetId = QuestFollowUpPlan.TargetId(record.ModId, record.NextKey);
        string skip = QuestFollowUpPlan.Skip(record.Key, record.NextKey, QuestLog.Find(targetId) != null,
            Started.Contains(targetId), Started.Count);
        if (skip != null)
        {
            if (QuestFollowUpPlan.IsWorthLogging(skip))
                StoryLog.Refused("Follow-up of " + finishedId, $"{targetId} was not started ({skip})");
            return;
        }
        if (!ModRegistry.IsLoaded(record.ModId))
        {
            StoryLog.Refused("Follow-up of " + finishedId, $"{record.ModId} is not loaded, so {targetId} was not started");
            return;
        }

        var book = QuestRegistry.Book;
        var definition = book.ToDefinition(book.Find(targetId));
        if (definition == null)
        {
            StoryLog.Refused("Follow-up of " + finishedId, $"the save has no definition for {targetId}");
            return;
        }
        var result = QuestAuthoring.Create(record.ModId, definition);
        if (result.Created)
            Started.Add(targetId);
    }
}
