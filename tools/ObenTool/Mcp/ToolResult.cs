using System.Text.Json.Nodes;

namespace ObenTool.Mcp;

/// <summary>What a tool hands back to the MCP client: text and/or images.</summary>
public sealed class ToolResult
{
    private readonly JsonArray _content = new();

    public bool IsError { get; private init; }

    public static ToolResult Text(string text) => new ToolResult().AddText(text);

    public static ToolResult Error(string text) => new ToolResult { IsError = true }.AddText(text);

    public ToolResult AddText(string text)
    {
        _content.Add(new JsonObject { ["type"] = "text", ["text"] = text });
        return this;
    }

    public ToolResult AddImage(string base64, string mimeType)
    {
        _content.Add(new JsonObject { ["type"] = "image", ["data"] = base64, ["mimeType"] = mimeType });
        return this;
    }

    public JsonObject ToJson() => new()
    {
        ["content"] = _content.DeepClone(),
        ["isError"] = IsError,
    };
}
