// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Quests;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>
/// The player's tasks: pick one, set its state or an objective's, move a counter, and for tasks made by
/// mods rewind them (<see cref="QuestRewindRows"/>). Tasks the game has loaded but not started can be
/// started. Everything goes through the framework's <see cref="QuestLog"/> and <see cref="QuestAuthoring"/>,
/// which refuse with a reason instead of breaking the save.
/// </summary>
internal static class QuestsSection
{
    private static readonly string[] TaskStates = { "Active", "Finished", "Failed", "Abandoned" };
    private static readonly CurrentTaskStatus[] TaskValues =
        { CurrentTaskStatus.Active, CurrentTaskStatus.Finished, CurrentTaskStatus.Failed, CurrentTaskStatus.Abandoned };
    private static readonly ObjectiveStatus[] ObjectiveValues =
        { ObjectiveStatus.Active, ObjectiveStatus.Finished, ObjectiveStatus.Failed, ObjectiveStatus.Abandoned };

    private static string _taskId;
    private static string _objectiveId;
    private static int _start;

    public static void Build(UiBuilder ui, CheatContext ctx)
    {
        ui.Header("Quests");
        if (!QuestLog.IsAvailable)
        {
            ui.Label(CheatContext.NeedsGame, muted: true);
            return;
        }

        var tasks = QuestLog.All();
        if (tasks.Count == 0)
            ui.Label("No task is started in this save.", muted: true);
        else
            BuildTask(ui, ctx, tasks);
        BuildStart(ui, ctx);
    }

    private static void BuildTask(UiBuilder ui, CheatContext ctx, List<QuestTask> tasks)
    {
        int index = tasks.FindIndex(t => t.Id == _taskId);
        if (index < 0)
            index = 0;
        var task = tasks[index];
        _taskId = task.Id;

        var names = new string[tasks.Count];
        for (int i = 0; i < tasks.Count; i++)
            names[i] = $"{tasks[i].Title} [{tasks[i].Status}]";
        ui.Cycle("Task", names, index, i =>
        {
            _taskId = tasks[i].Id;
            _objectiveId = null;
            ui.Window.Rebuild();
        });
        ui.Label($"{task.Id}: {DevRules.Cut(task.Description, 200)}", muted: true);

        ui.Row(r =>
        {
            for (int i = 0; i < TaskValues.Length; i++)
            {
                var status = TaskValues[i];
                r.Button(TaskStates[i], () => DevActions.Quest(ctx, ui, $"{task.Id} to {status}", () => QuestLog.SetTaskStatus(task.Id, status)));
            }
        });

        BuildObjective(ui, ctx, task);
        QuestRewindRows.Build(ui, ctx, task, _objectiveId);
    }

    private static void BuildObjective(UiBuilder ui, CheatContext ctx, QuestTask task)
    {
        var objectives = task.Objectives;
        if (objectives.Length == 0)
        {
            ui.Label("The task has no objectives yet.", muted: true);
            return;
        }

        int index = System.Array.FindIndex(objectives, o => o.Id == _objectiveId);
        if (index < 0)
            index = 0;
        var objective = objectives[index];
        _objectiveId = objective.Id;

        var names = new string[objectives.Length];
        for (int i = 0; i < objectives.Length; i++)
        {
            var o = objectives[i];
            names[i] = $"{o.Title} [{o.Status}]" + (o.HasCounter ? $" {o.Current}/{o.Target}" : "");
        }
        ui.Cycle("Objective", names, index, i =>
        {
            _objectiveId = objectives[i].Id;
            ui.Window.Rebuild();
        });
        ui.Label($"{objective.Id}{(objective.Optional ? ", optional" : "")}: {DevRules.Cut(objective.Description, 200)}", muted: true);

        ui.Row(r =>
        {
            for (int i = 0; i < ObjectiveValues.Length; i++)
            {
                var status = ObjectiveValues[i];
                r.Button(TaskStates[i], () => DevActions.Quest(ctx, ui, $"{objective.Id} to {status}",
                    () => QuestLog.SetObjectiveStatus(task.Id, objective.Id, status)));
            }
        });

        if (!objective.HasCounter)
            return;
        ui.Row(r =>
        {
            r.Button("Counter -1", () => DevActions.Quest(ctx, ui, "Counter -1", () => QuestLog.SetObjectiveProgress(task.Id, objective.Id, -1, add: true)));
            r.Button("+1", () => DevActions.Quest(ctx, ui, "Counter +1", () => QuestLog.SetObjectiveProgress(task.Id, objective.Id, 1, add: true)));
            r.Button("Fill", () => DevActions.Quest(ctx, ui, "Counter filled", () => QuestLog.SetObjectiveProgress(task.Id, objective.Id, objective.Target)));
            r.Button("Empty", () => DevActions.Quest(ctx, ui, "Counter emptied", () => QuestLog.SetObjectiveProgress(task.Id, objective.Id, 0)));
        });
    }

    // Tasks the game has loaded (through the current scene's objects) but the player hasn't started.
    private static void BuildStart(UiBuilder ui, CheatContext ctx)
    {
        var available = QuestLog.Available();
        if (available.Count == 0)
            return;
        if (_start >= available.Count)
            _start = 0;
        var names = new string[available.Count];
        for (int i = 0; i < available.Count; i++)
            names[i] = $"{available[i].Title} ({available[i].Id})";
        ui.Cycle("Start a task", names, _start, i => _start = i);
        ui.Button("Start it", () =>
        {
            var chosen = available[_start];
            _taskId = chosen.Id;
            _objectiveId = null;
            DevActions.Quest(ctx, ui, "Start " + chosen.Id, () => QuestLog.Start(chosen.Id));
        });
    }
}
