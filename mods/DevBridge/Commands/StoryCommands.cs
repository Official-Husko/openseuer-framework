using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Dialogue;
using ObenFramework.Quests;

namespace DevBridge.Commands;

/// <summary>Dialogue Lua state and quests.</summary>
internal static class StoryCommands
{
    [BridgeCommand("lua.get", "Read a dialogue variable, or an actor field when 'actor' is given.",
        Params = new[] { "name: variable name (or field name with actor)", "actor: optional actor name" })]
    private static object LuaGet(JObject p)
    {
        string actor = p.Str("actor", null);
        var value = actor == null ? LuaVariables.Get(p.Str("name")) : ActorFields.Get(actor, p.Str("name"));
        return Describe(value);
    }

    [BridgeCommand("lua.set", "Set a dialogue variable or actor field. Writes during a save load are held back until it finishes.",
        Params = new[] { "name: variable name (or field name with actor)", "value: string, number or bool", "actor: optional actor name", "default: only set it when missing, default false" })]
    private static object LuaSet(JObject p)
    {
        string name = p.Str("name");
        string actor = p.Str("actor", null);
        object value = ToValue(p["value"]);
        bool onlyIfMissing = p.Bool("default", false);

        LuaWriteResult result;
        if (actor == null)
            result = onlyIfMissing ? LuaVariables.SetDefault(name, value) : LuaVariables.Set(name, value);
        else
            result = onlyIfMissing ? ActorFields.SetDefault(actor, name, value) : ActorFields.Set(actor, name, value);

        var now = actor == null ? LuaVariables.Get(name) : ActorFields.Get(actor, name);
        var response = Describe(now);
        response["result"] = result.ToString().ToLowerInvariant();
        return response;
    }

    [BridgeCommand("lua.run", "Run Lua in the dialogue environment. Use 'return ...' to get a value back.",
        Params = new[] { "code: Lua source" })]
    private static object LuaRun(JObject p)
    {
        var result = LuaScript.Run(p.Str("code"));
        var response = Describe(result.Value);
        response["ok"] = result.Succeeded;
        if (result.Error != null)
            response["error"] = result.Error;
        return response;
    }

    [BridgeCommand("quests.list", "List started tasks with their objectives, or one task.",
        Params = new[] { "task: optional task id", "status: optional filter (Active, Finished, Failed, Abandoned)", "available: also list loaded tasks that were not started, default false" })]
    private static object QuestsList(JObject p)
    {
        RequireQuests();
        string id = p.Str("task", null);
        CurrentTaskStatus? filter = p["status"] == null ? (CurrentTaskStatus?)null : ParseEnum<CurrentTaskStatus>(p.Str("status"));

        var source = QuestLog.All();
        if (id != null)
        {
            var one = QuestLog.Get(id) ?? throw new ArgumentException($"Task '{id}' is not started.");
            source.Clear();
            source.Add(one);
        }

        var tasks = new JArray();
        foreach (var task in source)
        {
            if (filter == null || filter == CurrentTaskStatus.AnyStatus || task.Status == filter)
                tasks.Add(Describe(task));
        }

        var response = new JObject { ["tasks"] = tasks };
        if (p.Bool("available", false))
        {
            var available = new JArray();
            foreach (var task in QuestLog.Available())
                available.Add(new JObject { ["id"] = task.Id, ["title"] = task.Title });
            response["available"] = available;
        }
        return response;
    }

    [BridgeCommand("quests.set", "Change a task or objective. Reports whether anything changed, since the game ignores unmet requirements silently.",
        Params = new[] { "task: task id", "objective: optional objective id", "status: Active, Finished, Failed or Abandoned", "start: true to start an unstarted task instead", "progress: set the counter of an objective", "add: true to add 'progress' instead of setting it" })]
    private static object QuestsSet(JObject p)
    {
        RequireQuests();
        string taskId = p.Str("task");
        string objectiveId = p.Str("objective", null);

        QuestResult result;
        if (p.Bool("start", false))
            result = QuestLog.Start(taskId);
        else if (p["progress"] != null)
            result = QuestLog.SetObjectiveProgress(taskId, objectiveId ?? throw new ArgumentException("Missing parameter 'objective'."),
                p.Int("progress", 0), p.Bool("add", false));
        else if (objectiveId != null)
            result = QuestLog.SetObjectiveStatus(taskId, objectiveId, ParseEnum<ObjectiveStatus>(p.Str("status")));
        else
            result = QuestLog.SetTaskStatus(taskId, ParseEnum<CurrentTaskStatus>(p.Str("status")));

        var task = QuestLog.Get(taskId);
        return new JObject
        {
            ["changed"] = result.Changed,
            ["reason"] = result.Reason,
            ["task"] = task == null ? null : Describe(task),
        };
    }

    private static void RequireQuests()
    {
        if (!QuestLog.IsAvailable)
            throw new InvalidOperationException("There are no quests: not in a gameplay scene (main menu or still loading).");
    }

    private static JObject Describe(DialogueValue value)
    {
        var json = new JObject { ["exists"] = value.Exists, ["type"] = value.Type };
        if (value.Exists)
            json["value"] = value.IsNumber ? JToken.FromObject(value.AsNumber) : value.IsBool || value.IsString ? JToken.FromObject(value.ToObject()) : value.ToString();
        return json;
    }

    private static JObject Describe(QuestTask task)
    {
        var objectives = new JArray();
        foreach (var o in task.Objectives)
        {
            var json = new JObject
            {
                ["id"] = o.Id,
                ["title"] = o.Title,
                ["status"] = o.Status.ToString(),
                ["optional"] = o.Optional,
            };
            if (o.HasCounter)
            {
                json["current"] = o.Current;
                json["target"] = o.Target;
            }
            objectives.Add(json);
        }
        return new JObject { ["id"] = task.Id, ["title"] = task.Title, ["status"] = task.Status.ToString(), ["objectives"] = objectives };
    }

    private static object ToValue(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            throw new ArgumentException("Missing parameter 'value'.");
        switch (token.Type)
        {
            case JTokenType.Boolean: return token.Value<bool>();
            case JTokenType.Integer: return token.Value<double>();
            case JTokenType.Float: return token.Value<double>();
            case JTokenType.String: return token.Value<string>();
            default: throw new ArgumentException("'value' must be a string, number or bool.");
        }
    }

    private static T ParseEnum<T>(string text) where T : struct
    {
        if (Enum.TryParse(text, ignoreCase: true, out T value) && Enum.IsDefined(typeof(T), value))
            return value;
        throw new ArgumentException($"'{text}' is not a valid {typeof(T).Name}; use {string.Join(", ", Enum.GetNames(typeof(T)))}.");
    }
}
