using System;
using System.IO;
using System.Linq;
using DevBridge.Server;
using ObenFramework.Console;
using ObenLoader;
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

    [BridgeCommand("log.stats", "What the logs held back this session, whether verbose logging is on, and the log files with their sizes.")]
    private static object Stats(JObject p)
    {
        var files = new JArray();
        foreach (string sub in new[] { "", "mods" })
        {
            string dir = Path.Combine(LoaderPaths.LogsDir, sub);
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in new DirectoryInfo(dir).GetFiles("*.log").OrderBy(f => f.Name))
                files.Add(new JObject { ["file"] = sub.Length == 0 ? file.Name : sub + "/" + file.Name, ["kb"] = (file.Length + 1023) / 1024 });
        }
        return new JObject
        {
            ["droppedLines"] = LogHub.DroppedLines,
            ["heldBackErrors"] = LogHub.HeldBackErrors,
            ["verbose"] = LogHub.Verbose,
            ["frame"] = LogHub.Frame,
            ["dir"] = LoaderPaths.LogsDir,
            ["files"] = files,
        };
    }

    [BridgeCommand("console.exec", "Run a line in the game's F1 console and return what it printed.", Params = new[] { "command: e.g. 'add_money 100'" })]
    private static object Exec(JObject p) => new JArray(GameConsole.Execute(p.Str("command")).ToArray());

    [BridgeCommand("console.mods", "Console commands added by mods.")]
    private static object ModCommands(JObject p) =>
        new JArray(GameConsole.ModCommands.Select(c => $"{c.Usage} ({c.Owner})").ToArray());
}
