using ObenTool.Bridge;
using ObenTool.Game;
using ObenTool.Scripting;

namespace ObenTool.Mcp;

/// <summary>Running C# in the game: the quickest way to try something without a rebuild or restart.</summary>
public static class ScriptTools
{
    public static IEnumerable<McpTool> All(BridgeClient bridge, Func<GameInstall> game)
    {
        yield return new McpTool("script_run",
            "Compile C# against the game and run it inside it. Either a whole file with a static Run() or "
            + "Run(ScriptScope Scope) method, or just statements/an expression (the body of Run). Common usings "
            + "(UnityEngine, HarmonyLib, ObenFramework...) are added for snippets. Scope.Harmony patches and Scope.Track'ed "
            + "things are undone when a script with the same name runs again or script_reset is called. Returns "
            + "the result as JSON plus any log lines written while it ran.",
            Schema.Object()
                .Str("code", "The C# code", required: true)
                .Str("name", "Script name; runs with the same name replace each other (default eval)")
                .Int("depth", "How deep to expand the result (default 2)"),
            async (args, ct) =>
            {
                string code = args["code"]!.GetValue<string>();
                string name = args["name"]?.GetValue<string>() ?? "eval";
                int depth = args["depth"]?.GetValue<int>() ?? 2;
                var result = await ScriptClient.RunAsync(bridge, game(), name, code, name + ".cs", depth, ct);
                return ToolResult.Text(BridgeClient.Pretty(result));
            });

        yield return new McpTool("script_reset",
            "Undo what scripts set up (Harmony patches, objects, coroutines). Omit name to reset all.",
            Schema.Object().Str("name", "Script name"),
            async (args, ct) =>
                ToolResult.Text(BridgeClient.Pretty(await ScriptClient.ResetAsync(bridge, args["name"]?.GetValue<string>(), ct))));
    }
}
