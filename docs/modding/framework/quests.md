# Quests

`ObenFramework.Quests` wraps the game's own task system (`TaskController`, tasks and objectives as ScriptableObject assets). It is not the PixelCrushers quest system. The data model and the game's Lua functions for it are in [../../game/npcs-dialogue-quests.md](../../game/npcs-dialogue-quests.md) section 4.

The entry point is the static class `QuestLog`. Tasks and objectives are addressed by the string ids the game's Lua (`TaskActive("id")`) and F1 console (`list_tracked_tasks`) use. The state only exists in gameplay scenes: `QuestLog.IsAvailable` says whether it does, and reads return nothing otherwise.

## Reading

```csharp
using ObenFramework.Quests;

foreach (var task in QuestLog.All())
{
    Log.Info($"{task.Id}: {task.Title} [{task.Status}]");
    foreach (var o in task.Objectives)
        Log.Info($"  {o.Id}: {o.Title} [{o.Status}]" + (o.HasCounter ? $" {o.Current}/{o.Target}" : ""));
}

CurrentTaskStatus? status = QuestLog.StatusOf("SomeTask");               // null: not started
ObjectiveStatus? step = QuestLog.StatusOf("SomeTask", "SomeObjective");
```

| Member | Meaning |
|---|---|
| `All()` | every started task as a `QuestTask` (`Id`, `Title`, `Description`, `Status`, `Objectives[]`) |
| `Get(taskId)` | one task or null |
| `StatusOf(taskId)`, `StatusOf(taskId, objectiveId)` | status or null when not started |
| `Available()` | tasks the game has in memory but the player has not started |

`QuestTask` and `QuestObjective` are copies taken when you ask; they do not follow later changes. Statuses are the game's own enums: `CurrentTaskStatus` (`Active, Failed, Finished, Abandoned`; `AnyStatus` is only a filter value) and `ObjectiveStatus` (`Active, Failed, Finished, Abandoned, Hidden`; `AnyStatus` and `NotActive` are filter values). Reading allocates, so call it in reaction to an event, not every frame.

Tasks are assets that the game loads through Addressables when a scene object refers to them. A task the player has not started only exists in memory if some loaded object points at it, which is why `Available()` is a partial list, and why `Start` can only begin tasks in it.

## Changing

```csharp
var result = QuestLog.SetObjectiveStatus("SomeTask", "SomeObjective", ObjectiveStatus.Finished);
if (!result.Changed)
    Log.Warn("Not done: " + result.Reason);
```

| Method | What it does |
|---|---|
| `Start(taskId)` | hands out the first objectives of a loaded, unstarted task, like a scene trigger |
| `SetTaskStatus(taskId, status)` | `EditTaskStatus`; leaving Active abandons the active objectives |
| `SetObjectiveStatus(taskId, objectiveId, status)` | finish, fail or abandon an active objective (`EditTask`, with the game's requirement checks); or activate: an objective never given is handed out if the game has it loaded, a closed one is reopened |
| `SetObjectiveProgress(taskId, objectiveId, amount, add = false)` | `EditTaskCounter`; reaching the target finishes the objective |

All of them go through the game's methods, so notifications, the task panel, the Lua lists (`TaskActive`, `ObjectiveCompleted`...) and follow-up objectives behave as in play. Finishing the last active objective of a task that finishes itself finishes the task too.

The game's methods fail without a word when a required task or objective is missing, the task isn't Active, or the objective is already in that state, and some return nothing at all. Each `QuestLog` method therefore reads the state again afterwards and returns a `QuestResult`: `Changed`, and a `Reason` in plain words when nothing happened. It also refuses, with a reason, while a save is loading (the load would overwrite the change) and outside a loaded world.

Known quirks of the game that you inherit: reopening a finished task with `SetTaskStatus(..., Active)` does not take it out of the game's finished list, so `TaskFinished(id)` in Lua stays true; the game's requirement check for task prerequisites is only applied when handing out objectives, not when changing a task's status. Tasks created at runtime (not from game assets) have no Addressables path, and whether they survive a save is untested, so this module does not create them.

## Events

```csharp
QuestLog.TaskChanged.Subscribe(this, c =>
{
    if (c.IsNew) Log.Info($"Started {c.Title}");
    else if (c.Current == CurrentTaskStatus.Finished) Log.Info($"Finished {c.Title}");
});

QuestLog.ObjectiveChanged.Subscribe(this, c =>
{
    if (c.StatusChanged && c.Current == ObjectiveStatus.Finished) ...
    else Log.Info($"{c.Title}: {c.Amount}/{c.Target}");
});
```

The game's own `TaskController.TaskUpdated` and `ObjectiveUpdated` carry no arguments and fire in the middle of the game's edits. The framework uses them only as a hint that something changed, compares the state against its last copy on the next frame (`LateUpdate`), and raises one event per difference:

- `TaskChange`: `TaskId`, `Title`, `IsNew`, `Previous`, `Current`.
- `ObjectiveChange`: `TaskId`, `ObjectiveId`, `Title`, `IsNew`, `Previous`, `Current`, `PreviousAmount`, `Amount`, `Target`, and `StatusChanged` (false when only the counter moved).

Things to know:

- Events arrive one frame after the change, in the same frame as each other, and never inside the game's call, so a handler may change quests itself without breaking the game's loops. Each handler runs in its own try/catch.
- Loading a save, changing level and any other time the game rebuilds the task list do not report every task as new: the framework records the state again silently. Changes made in the same frame a load finishes are recorded, not reported.
- Nothing runs per frame while quests are quiet: the watcher component is disabled until the game raises one of its events or a `QuestLog` method changes something. A comparison allocates only for a task or objective it has not seen before.
- A few game paths change state without raising an event (reactivating a task through `EditTaskStatus` is one). `QuestLog`'s own methods always trigger a comparison; for a game script that does this, the change is picked up at the next event.

## Dev bridge

```sh
oben call quests.list
oben call quests.list task=SomeTask
oben call quests.list status=Active available=true
oben call quests.set task=SomeTask objective=SomeObjective status=Finished
oben call quests.set task=SomeTask status=Abandoned
oben call quests.set task=SomeTask start=true
oben call quests.set task=SomeTask objective=Collect progress=2 add=true
```

`quests.list` prints the started tasks with their objectives (and, with `available=true`, the loaded tasks that were not started). `quests.set` returns `changed`, the `reason` when nothing changed, and the task as it is now. See [../dev-tools.md](../dev-tools.md).
