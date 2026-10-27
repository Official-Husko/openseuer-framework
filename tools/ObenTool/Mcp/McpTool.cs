using System.Text.Json.Nodes;

namespace ObenTool.Mcp;

/// <summary>One tool exposed over MCP.</summary>
public sealed record McpTool(
    string Name,
    string Description,
    JsonObject InputSchema,
    Func<JsonObject, CancellationToken, Task<ToolResult>> Run)
{
    public JsonObject Describe() => new()
    {
        ["name"] = Name,
        ["description"] = Description,
        ["inputSchema"] = InputSchema.DeepClone(),
    };
}
