using System.Text.Json.Nodes;
using ObenTool.Bridge;
using ObenTool.Cli;

namespace ObenTool.Commands;

public sealed class CallCommand : ICommand
{
    public string Name => "call";
    public string Usage => "call <method> [json-params | key=value ...]";
    public string Summary => "Run a dev bridge command in the game, e.g. call player.get";

    public async Task<int> RunAsync(Args args)
    {
        string method = args.At(0) ?? throw new ArgumentException("Usage: " + Usage);
        var parameters = ParseParams(args.Positional.Skip(1).ToList());

        var client = new BridgeClient(port: args.BridgePort);
        var result = await client.CallAsync(method, parameters, TimeSpan.FromSeconds(args.GetInt("timeout", 30)));
        Console.WriteLine(BridgeClient.Pretty(result));
        return 0;
    }

    /// <summary>Accepts one JSON object, or key=value pairs where values are parsed as JSON when possible.</summary>
    private static JsonObject ParseParams(List<string> parts)
    {
        if (parts.Count == 1 && parts[0].TrimStart().StartsWith('{'))
            return JsonNode.Parse(parts[0])!.AsObject();

        var obj = new JsonObject();
        foreach (string part in parts)
        {
            int eq = part.IndexOf('=');
            if (eq <= 0)
                throw new ArgumentException($"Expected key=value, got '{part}'");
            string key = part[..eq];
            string raw = part[(eq + 1)..];
            obj[key] = TryParseJson(raw) ?? JsonValue.Create(raw);
        }
        return obj;
    }

    private static JsonNode? TryParseJson(string raw)
    {
        try { return JsonNode.Parse(raw); }
        catch (System.Text.Json.JsonException) { return null; }
    }
}
