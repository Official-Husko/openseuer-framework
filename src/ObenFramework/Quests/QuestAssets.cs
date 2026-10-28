using System;
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Quests;

/// <summary>
/// Finds task and objective assets the game has in memory. Tasks the player hasn't started aren't in
/// the game's state at all; their ScriptableObjects are loaded through Addressables when a scene object
/// refers to them. These scans cover everything loaded and are only for explicit calls, never per frame.
/// </summary>
internal static class QuestAssets
{
    public static TaskInfo FindTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId))
            return null;
        foreach (var task in Resources.FindObjectsOfTypeAll<TaskInfo>())
        {
            if (task != null && string.Equals(task.Id, taskId, StringComparison.Ordinal))
                return task;
        }
        return null;
    }

    /// <summary>The objective asset with this id that belongs to the task: one of its first objectives, or any loaded one pointing back at it.</summary>
    public static TaskObjective FindObjective(TaskInfo task, string objectiveId)
    {
        if (task == null || string.IsNullOrEmpty(objectiveId))
            return null;

        foreach (var reference in task.firstObjectives)
        {
            if (reference != null && reference.objective != null && reference.objective.Id == objectiveId)
                return reference.objective;
        }
        foreach (var objective in Resources.FindObjectsOfTypeAll<TaskObjective>())
        {
            if (objective != null && objective.Id == objectiveId && objective.task != null && objective.task.Id == task.Id)
                return objective;
        }
        return null;
    }

    public static List<AvailableTask> Unstarted()
    {
        var result = new List<AvailableTask>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in Resources.FindObjectsOfTypeAll<TaskInfo>())
        {
            if (task == null || QuestLog.Find(task.Id) != null || !seen.Add(task.Id))
                continue;
            result.Add(new AvailableTask(task.Id, task.title));
        }
        return result;
    }
}
