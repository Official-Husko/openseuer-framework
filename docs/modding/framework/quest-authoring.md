# Making quests

`QuestAuthoring` (namespace `ObenFramework.Quests`) lets a mod make its own tasks, change them, send them back to an earlier state and chain follow-ups. Reading and the game's own tasks are in [quests.md](quests.md); when to create things is in [story.md](story.md).

A mod's task is a real game task: it shows in the task panel, raises the game's "New Task" and "Objective Complete" notifications, and answers the game's Lua checks (`TaskActive("MyMod.intro")`, `ObjectiveCompleted("MyMod.intro", "talk")`). `QuestLog.Get`, the events and the dev bridge treat it like any other.

## Ids and ownership

- The task id is the mod id, a dot and a key you pick: `MyMod.intro`. `QuestAuthoring.TaskId(this, "intro")` builds it.
- Keys (task and objective) are 1 to 48 letters, digits, `_` or `-`, no dot. The mod id has to be 1 to 64 letters, digits, `_`, `-` or `.` for authoring to work.
- Only the mod whose id starts the task id can change it through `QuestAuthoring`. The game's own tasks never have a dot in their id and always come from an asset path, so they can't be mistaken for a mod's (checked against the 46 task assets loaded in Open Sewer Bazaar on 0.5.13).

## Making a task

```csharp
using ObenFramework.Quests;
using ObenFramework.Story;

public override void OnLoad()
{
    StoryPump.WhenReady(this, _ =>
    {
        if (QuestLog.Get(QuestAuthoring.TaskId(this, "kebab")) != null)
            return;                                   // already in this save
        var result = QuestAuthoring.Create(this, new QuestDefinition
        {
            Key = "kebab",
            Title = "A kebab for Aziz",
            Description = "Aziz wants to taste the competition.",
            FinishWhenDone = true,
            Objectives =
            {
                new QuestObjectiveDefinition { Key = "buy", Title = "Bring a kebab with fries", ItemId = 19500, Count = 1 },
                new QuestObjectiveDefinition { Key = "secret", Hidden = true },
            },
        });
        if (!result.Created)
            Log.Warn($"No quest: {result.Refusal} ({result.Reason})");
    });
}
```

`Create` returns a `QuestCreateResult` with `TaskId`, `Refusal` and `Reason`:

| Refusal | Meaning |
|---|---|
| `NotReady` | no world with a task panel; create from `StoryPump.Ready` |
| `Busy` | a save or load is running, or the call came from inside one of the game's own task edits |
| `BadInput` | a key, text, counter or item breaks the rules; `Reason` says which |
| `AlreadyExists` | the save already has this task id (tasks live in the save; check `QuestLog.Get` first) |
| `DuplicateTitle` | another started or loaded task has this title |
| `Rejected` | the game refused the first objective, nothing was made |
| `Failed` | a game call threw before the task existed (logged) |
| `PartiallyCreated` | the task exists but a later objective was refused; add it with `AddObjective` |

Rules: 1 to 12 objectives with unique keys, titles up to 100 characters on one line (hidden objectives may leave it out), descriptions up to 1000 characters, counters 0 to 9999, an item hand-in needs both `ItemId` and `Count` (1 to 9999). Every objective starts at once; a `Hidden` one shows "???" until revealed. With `FinishWhenDone` the game finishes the task once no objective is active (hidden ones don't count); otherwise call `Finish`.

## Changing a task

All return a `QuestResult` (`Changed`, `Reason`), like `QuestLog`'s methods.

| Method | What it does |
|---|---|
| `AddObjective(this, taskId, objective)` | adds a step to a running task (up to 12) |
| `SetObjective(this, taskId, key, status)` | finishes, fails or abandons an active step |
| `SetCounter(this, taskId, key, amount)` | sets a counter from 0 to its target; the target finishes the step |
| `HandIn(this, taskId, key)` | takes `Count` of `ItemId` from the player and moves the step on (adds to a counter, else finishes); gives the items back if the step can't move |
| `Reveal(this, taskId, key, title, description)` | gives a hidden step its text and makes it active ("New Objective"); a counter already at its target finishes it |
| `Finish(this, taskId)`, `Fail(this, taskId)` | closes a running task; active steps are abandoned by the game |

## Rewinding

The game has no way back for a task, so these rewrite the task's status, each step's status and counter, and the game's Lua lists (`TaskActive`, `ObjectiveCompleted` and the rest) directly, restore the texts from the remembered definition, and redraw the task panel. Each raises `QuestLog.Rewound` (`TaskId`, `Kind`, and for moves `From`/`To`, the first active step before and after).

| Method | What it does |
|---|---|
| `SetTitle(this, taskId, title)`, `SetTitle(this, taskId, key, title)`, `SetDescription(this, taskId, text)` | new texts (a hidden step keeps "???" and gets the title when revealed) |
| `Restart(this, taskId)` | a finished, failed or abandoned task back to its start |
| `Reset(this, taskId)` | back to the start from any state |
| `JumpTo(this, taskId, key)` | that step active, the ones before it finished with full counters, the rest at their start |
| `Remove(this, taskId)` | out of the panel and the lists; gone from the save at the next save |
| `SaveCheckpoint(this, taskId, name, restored)`, `RestoreCheckpoint(this, taskId, name)`, `Checkpoints(taskId)` | named copies of the task's state for this session (16 per task, names of letters, digits and `-`); `restored` runs after each restore and is dropped when the mod unloads |

## Follow-ups

Set `Next` (and the first time `NextDefinition`) to chain tasks of the same mod. When the task finishes, the framework creates the follow-up from the remembered definition at the end of the frame, if the save doesn't have it yet and the mod is loaded. At most 8 follow-ups start per session, so a chain that loops stops.

## Saves, and removing a mod

- The task itself (status, texts, counters, steps) is saved by the game in its own task data. A mod's task is a plain `TaskInfo` with an empty asset path; the game's serializer writes all its fields and creates it again on load, and the game's "load from asset path" step keeps the loaded copy when there is no path.
- The definition (original texts, which steps were hidden, what a hand-in takes, the follow-up chain) is kept in `<save>/mods/ObenFramework.quests.json` through `ModSaves`. Rewinds, hand-ins and follow-ups need it; nothing else does.
- The framework patches `TaskInfo.Compare` so a mod's task is matched by id, never by title (the game falls back to the title). Without the framework the game would match by title, which is why `Create` refuses duplicate titles.
- If a mod is removed, its tasks stay in the log as they were and can't move any more, and its lines in `ObenFramework.quests.json` are unused. Neither breaks the save. Removing the task first (`Remove`) takes it out completely.

## Console

`modquest list` lists the mod tasks of the save, `modquest reset MyMod.intro` sends a mod's task back to its start (the mod has to be loaded), `modquest skip <taskId> [objectiveId]` finishes a task or one step, game tasks included.

## Dev bridge

DevBridge owns the tasks it makes (`DevBridge.<key>`):

```sh
oben call quests.create '{"key":"test","title":"Bridge test","objectives":[{"key":"a","title":"Step one","target":3},{"key":"b","hidden":true}]}'
oben call quests.edit task=DevBridge.test action=counter objective=a amount=2
oben call quests.edit task=DevBridge.test action=reveal objective=b title="Step two"
oben call quests.rewind task=DevBridge.test action=save name=before
oben call quests.edit task=DevBridge.test action=finish
oben call quests.rewind task=DevBridge.test action=restore name=before
oben call quests.rewind task=DevBridge.test action=jump objective=b
oben call quests.rewind task=DevBridge.test action=reset
oben call quests.rewind task=DevBridge.test action=remove
oben call story.queue
```

`quests.edit` actions: `add` (with `definition`), `finish`, `fail`, `counter`, `handin`, `reveal`, `objective` (with `status`). `quests.rewind` actions: `reset`, `restart`, `remove`, `jump`, `title`, `description` (with `text`), `save`, `restore` (with `name`).
