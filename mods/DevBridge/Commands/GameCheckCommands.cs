using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework;
using ObenFramework.Diagnostics;

namespace DevBridge.Commands;

internal static class GameCheckCommands
{
    [BridgeCommand("game.check", "The start-up game check: does every loaded mod's hook fit this game, and is it the verified version.",
        Params = new[] { "run?: true to check again now (the result comes with the next call)" })]
    private static object Check(JObject p)
    {
        bool started = p.Bool("run", false) && GameCheck.Start();
        var result = GameCheck.Last;
        var answer = new JObject
        {
            ["running"] = GameCheck.IsRunning,
            ["started"] = started,
            ["verifiedGame"] = FrameworkInfo.IsVerifiedGame,
            ["gameMismatch"] = FrameworkInfo.GameMismatch,
            ["verifiedOn"] = FrameworkInfo.VerifiedGameVersion,
        };
        if (result == null)
            return answer;
        answer["game"] = result.Game;
        answer["allFit"] = result.AllFit;
        answer["failure"] = result.Failure;
        answer["mods"] = result.Mods;
        answer["checked"] = result.Checked;
        answer["dynamic"] = result.Dynamic;
        answer["seconds"] = System.Math.Round(result.Seconds, 2);
        answer["problems"] = new JArray(result.Problems.Select(x => new JObject
        {
            ["mod"] = x.Mod,
            ["class"] = x.Class,
            ["hook"] = x.Hook,
            ["message"] = x.Message,
            ["ambiguous"] = x.Ambiguous,
        }).ToArray<object>());
        return answer;
    }
}
