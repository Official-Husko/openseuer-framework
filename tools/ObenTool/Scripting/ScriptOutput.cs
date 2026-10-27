using System.Text.Json.Nodes;
using ObenTool.Bridge;
using ObenTool.Cli;

namespace ObenTool.Scripting;

/// <summary>Prints what a script logged, then its result.</summary>
public static class ScriptOutput
{
    public static void Print(JsonNode? response)
    {
        if (response?["log"] is JsonArray log)
        {
            foreach (var line in log)
                Out.Info(line?.GetValue<string>() ?? "");
        }
        Console.WriteLine(BridgeClient.Pretty(response?["result"]));
    }
}
