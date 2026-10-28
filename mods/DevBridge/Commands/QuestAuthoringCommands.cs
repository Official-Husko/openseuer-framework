using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.NewGames;
using ObenFramework.Quests;
using ObenFramework.Story;

namespace DevBridge.Commands;

/// <summary>Quests made through the bridge (owned by DevBridge, ids "DevBridge.key") and the story queue.</summary>
internal static class QuestAuthoringCommands
{
    [BridgeCommand("quests.create", "Make a DevBridge task (id DevBridge.<key>) from a definition.",
        Params = new[] { "key: task key", "title: task title", "description: optional", "finishWhenDone: optional bool",
            "objectives: array of {key, title, description, target, optional, hidden, itemId, count}", "next / nextDefinition: optional follow-up" })]
    private static object Create(JObject p)
    {
        var definition = p.ToObject<QuestDefinition>() ?? throw new ArgumentException("Pass the definition as parameters.");
        var result = QuestAuthoring.Create(DevBridgeMod.Instance, definition);
        return new JObject
        {
            ["created"] = result.Created,
            ["taskId"] = result.TaskId,
            ["refusal"] = result.Refusal.ToString(),
            ["reason"] = result.Reason,
        };
    }

    [BridgeCommand("quests.edit", "Change a DevBridge task: add, finish, fail, counter, handin, reveal or objective.",
        Params = new[] { "task: task id (DevBridge.<key>)", "action: add | finish | fail | counter | handin | reveal | objective",
            "objective: objective key (counter, handin, reveal, objective)", "amount: counter value", "status: Finished, Failed or Abandoned (objective)",
            "title / description: text for reveal", "definition: objective definition object (add)" })]
    private static object Edit(JObject p)
    {
        var owner = DevBridgeMod.Instance;
        string task = p.Str("task");
        string objective = p.Str("objective", null);
        string action = p.Str("action").ToLowerInvariant();
        QuestResult result;
        switch (action)
        {
            case "add":
                var definition = (p["definition"] as JObject)?.ToObject<QuestObjectiveDefinition>() ?? throw new ArgumentException("Pass 'definition' as an object.");
                result = QuestAuthoring.AddObjective(owner, task, definition);
                break;
            case "finish": result = QuestAuthoring.Finish(owner, task); break;
            case "fail": result = QuestAuthoring.Fail(owner, task); break;
            case "counter": result = QuestAuthoring.SetCounter(owner, task, Need(objective), p.Int("amount", 0)); break;
            case "handin": result = QuestAuthoring.HandIn(owner, task, Need(objective)); break;
            case "reveal": result = QuestAuthoring.Reveal(owner, task, Need(objective), p.Str("title"), p.Str("description", null)); break;
            case "objective": result = QuestAuthoring.SetObjective(owner, task, Need(objective), ParseEnum<ObjectiveStatus>(p.Str("status"))); break;
            default: throw new ArgumentException("Unknown action; use add, finish, fail, counter, handin, reveal or objective.");
        }
        return Describe(result, task);
    }

    [BridgeCommand("quests.rewind", "Rewind a DevBridge task: reset, restart, remove, jump, title, description, save or restore a checkpoint.",
        Params = new[] { "task: task id (DevBridge.<key>)", "action: reset | restart | remove | jump | title | description | save | restore",
            "objective: objective key (jump, or title of a step)", "text: the new title or description", "name: checkpoint name (save, restore)" })]
    private static object Rewind(JObject p)
    {
        var owner = DevBridgeMod.Instance;
        string task = p.Str("task");
        string objective = p.Str("objective", null);
        QuestResult result;
        switch (p.Str("action").ToLowerInvariant())
        {
            case "reset": result = QuestAuthoring.Reset(owner, task); break;
            case "restart": result = QuestAuthoring.Restart(owner, task); break;
            case "remove": result = QuestAuthoring.Remove(owner, task); break;
            case "jump": result = QuestAuthoring.JumpTo(owner, task, Need(objective)); break;
            case "title":
                result = objective == null ? QuestAuthoring.SetTitle(owner, task, p.Str("text")) : QuestAuthoring.SetTitle(owner, task, objective, p.Str("text"));
                break;
            case "description": result = QuestAuthoring.SetDescription(owner, task, p.Str("text", null)); break;
            case "save": result = QuestAuthoring.SaveCheckpoint(owner, task, p.Str("name")); break;
            case "restore": result = QuestAuthoring.RestoreCheckpoint(owner, task, p.Str("name")); break;
            default: throw new ArgumentException("Unknown action; use reset, restart, remove, jump, title, description, save or restore.");
        }
        var response = Describe(result, task);
        response["checkpoints"] = new JArray(QuestAuthoring.Checkpoints(task).ToArray());
        return response;
    }

    [BridgeCommand("story.queue", "The story timing: session kind, whether StoryPump.Ready was raised, and the actions waiting in the queue.")]
    private static object Queue(JObject p) => new JObject
    {
        ["session"] = Sessions.Current.ToString(),
        ["ready"] = StoryPump.IsReady,
        ["waitingForReady"] = StoryPump.WaitingForReady,
        ["pending"] = new JArray(StoryPump.PendingNames().ToArray()),
    };

    private static JObject Describe(QuestResult result, string taskId)
    {
        var task = QuestLog.Get(taskId);
        JObject json = null;
        if (task != null)
        {
            var objectives = new JArray();
            foreach (var o in task.Objectives)
                objectives.Add(new JObject { ["id"] = o.Id, ["title"] = o.Title, ["status"] = o.Status.ToString(), ["current"] = o.Current, ["target"] = o.Target });
            json = new JObject { ["id"] = task.Id, ["title"] = task.Title, ["status"] = task.Status.ToString(), ["objectives"] = objectives };
        }
        return new JObject { ["changed"] = result.Changed, ["reason"] = result.Reason, ["task"] = json };
    }

    private static string Need(string objective) => objective ?? throw new ArgumentException("Missing parameter 'objective'.");

    private static T ParseEnum<T>(string text) where T : struct
    {
        if (Enum.TryParse(text, ignoreCase: true, out T value) && Enum.IsDefined(typeof(T), value))
            return value;
        throw new ArgumentException($"'{text}' is not a valid {typeof(T).Name}; use {string.Join(", ", Enum.GetNames(typeof(T)))}.");
    }
}
