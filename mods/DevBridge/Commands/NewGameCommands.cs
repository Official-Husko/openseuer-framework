using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.NewGames;

namespace DevBridge.Commands;

/// <summary>Play sessions, the last new game, new game options, start choices and the tutorial.</summary>
internal static class NewGameCommands
{
    [BridgeCommand("session.info", "How the running session began (SaveLoad, NewGame, Direct, Unknown or None) and whether its world is ready.")]
    private static object SessionInfo(JObject p) => new JObject
    {
        ["kind"] = Sessions.Current.ToString(),
        ["running"] = Sessions.IsRunning,
        ["newGame"] = Sessions.IsNewGame,
    };

    [BridgeCommand("newgame.last", "The choices of the last new game started in this run of the game, or null.")]
    private static object Last(JObject p)
    {
        var info = NewGameEvents.Last;
        if (info == null)
            return null;
        var options = new JArray();
        foreach (var value in info.Options)
            options.Add(new JObject { ["mod"] = value.ModId, ["key"] = value.Key, ["title"] = value.Title, ["value"] = value.Value });
        return new JObject
        {
            ["name"] = info.FullName,
            ["age"] = info.Age,
            ["gender"] = info.Gender,
            ["difficulty"] = info.Difficulty == null ? null : DifficultyCommands.Describe(info.Difficulty),
            ["preset"] = info.DifficultyPreset,
            ["options"] = options,
        };
    }

    [BridgeCommand("newgame.options", "Registered new game options with the value the screen shows and the value stored with the running game.",
        Params = new[] { "mod: optional mod id", "key: with mod, set the screen value of that option", "value: the value to set (1/0, a choice id or a number)" })]
    private static object Options(JObject p)
    {
        string mod = p.Str("mod", null);
        string key = p.Str("key", null);
        if (mod != null && key != null && p["value"] != null)
            NewGameOptions.Choose(mod, key, p.Str("value"));

        var options = new JArray();
        foreach (var option in NewGameOptions.Options)
        {
            if (mod != null && option.ModId != mod)
                continue;
            var json = new JObject
            {
                ["mod"] = option.ModId,
                ["key"] = option.Key,
                ["title"] = option.Title,
                ["kind"] = option.Kind.ToString(),
                ["placement"] = option.Placement.ToString(),
                ["default"] = option.Default,
                ["screen"] = NewGameOptions.ScreenValue(option.ModId, option.Key),
                ["value"] = NewGameOptions.GetValue(option.ModId, option.Key),
                ["chosen"] = NewGameOptions.WasChosen(option.ModId, option.Key),
            };
            if (option.Kind == NewGameOptionKind.Choice)
                json["ids"] = new JArray(option.Ids);
            options.Add(json);
        }

        var stored = new JArray();
        foreach (var value in NewGameOptions.Values())
            stored.Add(new JObject { ["mod"] = value.ModId, ["key"] = value.Key, ["value"] = value.Value });
        return new JObject { ["options"] = options, ["stored"] = stored };
    }

    [BridgeCommand("newgame.testoptions", "Registers four DevBridge-owned sample options (one of each kind) to try the New Game screen. They go when DevBridge unloads.")]
    private static object TestOptions(JObject p)
    {
        var bridge = DevBridgeMod.Instance;
        if (NewGameOptions.Find(bridge.Info.Id, "toggle") == null)
        {
            NewGameOptions.RegisterToggle(bridge, "toggle", "Test toggle", "A sample toggle from the dev bridge.", true);
            NewGameOptions.RegisterChoice(bridge, "choice", "Test choice", null, new[] { "low", "mid", "high" }, new[] { "Low", "Mid", "High" }, 1);
            NewGameOptions.RegisterInteger(bridge, "count", "Test count", null, 0, 50, 5, NewGamePlacement.CustomPanel);
            NewGameOptions.RegisterFloat(bridge, "rate", "Test rate", "Half to double, in quarters.", 0.5f, 2f, 0.25f, 1f, NewGamePlacement.CustomPanel);
        }
        return Options(new JObject { ["mod"] = bridge.Info.Id });
    }

    [BridgeCommand("start.choices", "The start choices picked in this game and the relationship the game derives from them.")]
    private static object StartChoicesList(JObject p)
    {
        var picks = new JArray();
        foreach (var choice in StartChoices.Selected())
            picks.Add(new JObject { ["id"] = choice.Id, ["title"] = choice.Title, ["oc"] = choice.Oc, ["rm"] = choice.Rm });
        return new JObject { ["available"] = StartChoices.IsAvailable, ["selected"] = picks, ["relationship"] = StartChoices.Relationship };
    }

    [BridgeCommand("tutorial.state", "Whether a tutorial prompt is up and which one.")]
    private static object TutorialState(JObject p) => new JObject
    {
        ["available"] = TutorialGuide.IsAvailable,
        ["active"] = TutorialGuide.Active,
        ["step"] = TutorialGuide.Step.ToString(),
    };
}
