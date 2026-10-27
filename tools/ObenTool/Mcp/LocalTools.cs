using System.Text.Json.Nodes;
using ObenTool.Bridge;
using ObenTool.Game;

namespace ObenTool.Mcp;

/// <summary>Tools that act on this machine (process control, files) instead of inside the game.</summary>
public static class LocalTools
{
    public static IEnumerable<McpTool> All(BridgeClient bridge, Func<GameInstall> game, int? instance = null)
    {
        yield return new McpTool("game_status",
            "Is the game running, is the bridge reachable, is the loader installed, and how the game is launched.",
            Schema.Object(),
            async (_, ct) =>
            {
                var g = game();
                var installer = new LoaderInstaller(g);
                var status = new JsonObject
                {
                    ["gameDir"] = g.Dir,
                    ["running"] = GameProcess.IsRunning(instance),
                    ["bridge"] = await bridge.IsUpAsync(ct),
                    ["loaderInstalled"] = installer.FilesPresent && installer.ManifestsPatched,
                    ["launch"] = LaunchProfile.Resolve(g).ToString(),
                };
                return ToolResult.Text(BridgeClient.Pretty(status));
            });

        yield return new McpTool("game_launch",
            "Start the game windowed and wait until the bridge answers.",
            Schema.Object().Int("waitSeconds", "How long to wait for the bridge (default 120)"),
            async (args, ct) =>
            {
                var g = game();
                if (!GameProcess.IsRunning(instance ?? 1))
                    GameProcess.Launch(g, instance ?? 1, GameProcess.WindowedArgs);

                int wait = args["waitSeconds"]?.GetValue<int>() ?? 120;
                var deadline = DateTime.UtcNow.AddSeconds(wait);
                while (DateTime.UtcNow < deadline)
                {
                    if (await bridge.IsUpAsync(ct))
                        return ToolResult.Text("Game is up and the bridge is answering.");
                    await Task.Delay(1000, ct);
                }
                return ToolResult.Error($"Bridge didn't answer within {wait}s. Check the log with game_logs.");
            });

        yield return new McpTool("game_stop",
            "Close the game. Asks it to quit first, kills it if that fails.",
            Schema.Object(),
            async (_, ct) =>
            {
                try { await bridge.CallAsync("game.quit", timeout: TimeSpan.FromSeconds(3), ct: ct); }
                catch (Exception e) when (e is BridgeException or OperationCanceledException or IOException) { }

                for (int i = 0; i < 20 && GameProcess.IsRunning(instance); i++)
                    await Task.Delay(500, ct);
                int killed = GameProcess.IsRunning(instance) ? GameProcess.Kill(instance) : 0;
                return ToolResult.Text(killed > 0 ? $"Killed {killed} process(es)." : "Game closed.");
            });

        yield return new McpTool("game_logs",
            "Tail of the loader log, Unity's Player.log or umu/Proton's output. Works even when the game is closed.",
            Schema.Object()
                .Str("which", "loader (default), player or runner")
                .Int("lines", "Number of lines (default 80)"),
            (args, _) =>
            {
                var g = game();
                int inst = instance ?? 1;
                string path = args["which"]?.GetValue<string>() switch
                {
                    "player" => g.PlayerLogFile(inst),
                    "runner" => Path.Combine(g.LogsDir, "runner" + GameInstall.Suffix(inst) + ".log"),
                    _ => g.LoaderLogFile(inst),
                };
                if (!File.Exists(path))
                    return Task.FromResult(ToolResult.Error($"No log at {path}"));

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var lines = reader.ReadToEnd().Split('\n');
                int count = args["lines"]?.GetValue<int>() ?? 80;
                return Task.FromResult(ToolResult.Text(string.Join('\n', lines.Skip(Math.Max(0, lines.Length - count)))));
            });

        yield return new McpTool("build",
            "Build the repo (or one project) in Release. Builds copy themselves into the game; hot-reload mods reload by themselves.",
            Schema.Object().Str("project", "Path to a .csproj, relative to the repo root (default: everything)"),
            async (args, ct) =>
            {
                string? project = args["project"]?.GetValue<string>();
                var (code, output) = await DotnetBuild.RunAsync(project, ct);
                return code == 0 ? ToolResult.Text(output) : ToolResult.Error(output);
            });
    }
}
