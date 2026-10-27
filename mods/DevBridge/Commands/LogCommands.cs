using System;
using System.Linq;
using DevBridge.Server;
using ObenFramework.Console;
using ObenLoader.Logging;
using Newtonsoft.Json.Linq;

namespace DevBridge.Commands;

internal static class LogCommands
{
    [BridgeCommand("log.tail", "Recent log lines from the game, mods and Unity.",
        Params = new[] { "after?: only lines after this sequence number", "max?: default 100", "level?: Debug, Info, Warning, Error" })]
    private static object Tail(JObject p)
    {
        var level = Enum.TryParse(p.Str("level", "Debug"), true, out LogLevel l) ? l : LogLevel.Debug;
        var lines = LogHub.Buffer.Read(p["after"]?.Value<long>() ?? -1, p.Int("max", 100), level);
        return new JObject
        {
            ["last"] = LogHub.Buffer.LastSequence,
            ["lines"] = new JArray(lines.Select(e => $"{e.Sequence} [{e.Level}] [{e.Source}] {e.Message}").ToArray()),
        };
    }

    [BridgeCommand("console.exec", "Run a line in the game's F1 console and return what it printed.", Params = new[] { "command: e.g. 'add_money 100'" })]
    private static object Exec(JObject p) => new JArray(GameConsole.Execute(p.Str("command")).ToArray());

    [BridgeCommand("console.mods", "Console commands added by mods.")]
    private static object ModCommands(JObject p) =>
        new JArray(GameConsole.ModCommands.Select(c => $"{c.Usage} ({c.Owner})").ToArray());
}
