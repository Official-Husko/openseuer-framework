using System.Text;
using System.Text.Json.Nodes;

namespace ObenTool.Mcp;

/// <summary>
/// Model Context Protocol server over stdio (newline-delimited JSON-RPC 2.0).
/// Implements the parts clients actually use: initialize, ping, tools/list and tools/call.
/// </summary>
public sealed class McpServer
{
    private const string ProtocolVersion = "2025-06-18";

    private readonly Dictionary<string, McpTool> _tools;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly TextWriter _out;

    public McpServer(IEnumerable<McpTool> tools, TextWriter output)
    {
        _tools = tools.ToDictionary(t => t.Name);
        _out = output;
    }

    public async Task RunAsync(TextReader input, CancellationToken ct)
    {
        var running = new List<Task>();
        while (!ct.IsCancellationRequested)
        {
            string? line = await input.ReadLineAsync(ct);
            if (line is null)
                break;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Requests run concurrently so a slow tool (e.g. launching the game) doesn't block pings.
            running.Add(HandleLineAsync(line, ct));
            running.RemoveAll(t => t.IsCompleted);
        }
        await Task.WhenAll(running);
    }

    private async Task HandleLineAsync(string line, CancellationToken ct)
    {
        JsonObject message;
        try
        {
            message = JsonNode.Parse(line)!.AsObject();
        }
        catch (Exception)
        {
            await SendAsync(Error(null, -32700, "Parse error"));
            return;
        }

        var id = message["id"]?.DeepClone();
        string method = message["method"]?.GetValue<string>() ?? "";
        var parameters = message["params"] as JsonObject ?? new JsonObject();

        if (id is null)
            return; // Notification (e.g. notifications/initialized): nothing to answer.

        try
        {
            JsonNode result = method switch
            {
                "initialize" => Initialize(parameters),
                "ping" => new JsonObject(),
                "tools/list" => ListTools(),
                "tools/call" => await CallToolAsync(parameters, ct),
                _ => throw new McpMethodNotFound(method),
            };
            await SendAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result });
        }
        catch (McpMethodNotFound e)
        {
            await SendAsync(Error(id, -32601, e.Message));
        }
        catch (Exception e)
        {
            await SendAsync(Error(id, -32603, e.Message));
        }
    }

    private static JsonObject Initialize(JsonObject p) => new()
    {
        ["protocolVersion"] = p["protocolVersion"]?.GetValue<string>() ?? ProtocolVersion,
        ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
        ["serverInfo"] = new JsonObject { ["name"] = "oben", ["version"] = "0.1.0" },
        ["instructions"] = "Controls a running copy of Obenseuer through the DevBridge mod. "
                           + "Start with game_status; use game_launch if the game isn't running.",
    };

    private JsonObject ListTools()
    {
        var list = new JsonArray();
        foreach (var tool in _tools.Values)
            list.Add(tool.Describe());
        return new JsonObject { ["tools"] = list };
    }

    private async Task<JsonNode> CallToolAsync(JsonObject p, CancellationToken ct)
    {
        string name = p["name"]?.GetValue<string>() ?? "";
        if (!_tools.TryGetValue(name, out var tool))
            return ToolResult.Error($"Unknown tool '{name}'").ToJson();

        var args = p["arguments"] as JsonObject ?? new JsonObject();
        try
        {
            return (await tool.Run(args, ct)).ToJson();
        }
        catch (Exception e)
        {
            // Tool failures are results, not protocol errors, so the model can see and react to them.
            return ToolResult.Error(e.Message).ToJson();
        }
    }

    private static JsonObject Error(JsonNode? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
    };

    private async Task SendAsync(JsonObject message)
    {
        await _writeLock.WaitAsync();
        try
        {
            await _out.WriteLineAsync(message.ToJsonString());
            await _out.FlushAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private sealed class McpMethodNotFound(string method) : Exception($"Method not found: {method}");
}
