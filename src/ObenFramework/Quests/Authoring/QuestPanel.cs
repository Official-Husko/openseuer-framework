// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Story;
using UnityEngine;

namespace ObenFramework.Quests;

/// <summary>
/// Keeps the game's task panel in step after a rewind changed a task behind its back: moves the row to the
/// active or finished part, relabels it, and takes the row of a removed task out.
/// </summary>
internal static class QuestPanel
{
    private static readonly AccessTools.FieldRef<TaskPanel, List<GameObject>> Rows = AccessTools.FieldRefAccess<TaskPanel, List<GameObject>>("taskList");
    private static readonly AccessTools.FieldRef<TaskPanel, List<GameObject>> ActiveRows = AccessTools.FieldRefAccess<TaskPanel, List<GameObject>>("activeTasks");
    private static readonly AccessTools.FieldRef<TaskPanel, List<GameObject>> RenovationRows = AccessTools.FieldRefAccess<TaskPanel, List<GameObject>>("renovateTasks");
    private static readonly AccessTools.FieldRef<TaskPanel, GameObject> ActiveTitle = AccessTools.FieldRefAccess<TaskPanel, GameObject>("activeTaskTitle");
    private static readonly AccessTools.FieldRef<TaskPanel, GameObject> RenovationTitle = AccessTools.FieldRefAccess<TaskPanel, GameObject>("renovationTaskTitle");

    /// <summary>Puts the task's row where its status belongs and redraws it and the details pane.</summary>
    public static void Refresh(TaskStatusInfo entry)
    {
        var panel = TaskPanel.instance;
        if (panel == null)
            return;
        try
        {
            if (entry.status == CurrentTaskStatus.Active)
            {
                panel.MoveActiveTaskUp(entry);
                var title = entry.renovation ? RenovationTitle(panel) : ActiveTitle(panel);
                if (title != null)
                    title.SetActive(true);
            }
            else
            {
                panel.MoveToFinishedTasks(entry);
            }
        }
        catch (Exception e)
        {
            StoryLog.Failed("Task panel order", e);
        }
        Relabel(panel, entry);
        try { panel.UpdateCurrentTaskInfo(); }
        catch (Exception e) { StoryLog.Failed("Task panel details", e); }
    }

    /// <summary>Takes a removed task's row out of the panel.</summary>
    public static void RemoveRow(TaskStatusInfo entry)
    {
        var panel = TaskPanel.instance;
        if (panel == null)
            return;
        try
        {
            var rows = Rows(panel);
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                var row = rows[i];
                var ui = row != null ? row.GetComponent<TaskUIPanel>() : null;
                if (ui == null || ui.task != entry)
                    continue;
                rows.RemoveAt(i);
                ActiveRows(panel).Remove(row);
                RenovationRows(panel).Remove(row);
                UnityEngine.Object.Destroy(row);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var ui = rows[i] != null ? rows[i].GetComponent<TaskUIPanel>() : null;
                if (ui != null && ui.task != null)
                    ui.SetTask(ui.task, i);
            }
            panel.UnselectTask();
            panel.UpdateCurrentTaskInfo();
        }
        catch (Exception e)
        {
            StoryLog.Failed("Task panel remove", e);
        }
    }

    private static void Relabel(TaskPanel panel, TaskStatusInfo entry)
    {
        try
        {
            foreach (var row in Rows(panel))
            {
                var ui = row != null ? row.GetComponent<TaskUIPanel>() : null;
                if (ui != null && ui.task == entry)
                    ui.SetTask(entry, ui.taskIndex);
            }
        }
        catch (Exception e)
        {
            StoryLog.Failed("Task panel label", e);
        }
    }
}
