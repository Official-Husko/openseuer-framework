using System.Text.Json.Nodes;
using ObenTool.Bridge;
using ObenTool.Game;

namespace ObenTool.Scripting;

/// <summary>Compiles a script and runs it in the game through the ScriptRunner mod.</summary>
public static class ScriptClient
{
    public static async Task<JsonNode?> RunAsync(BridgeClient bridge, GameInstall game, string name, string code,
        string fileLabel, int depth = 2, CancellationToken ct = default)
    {
        var output = ScriptCompiler.Compile(game, code, fileLabel);
        var parameters = new JsonObject
        {
            ["name"] = name,
            ["image"] = Convert.ToBase64String(output.Image),
            ["symbols"] = Convert.ToBase64String(output.Symbols),
            ["depth"] = depth,
        };
        return await bridge.CallAsync("script.run", parameters, ct: ct);
    }

    public static Task<JsonNode?> ResetAsync(BridgeClient bridge, string? name, CancellationToken ct = default)
    {
        var parameters = new JsonObject();
        if (name != null)
            parameters["name"] = name;
        return bridge.CallAsync("script.reset", parameters, ct: ct);
    }
}
