using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ObenTool.Cli;

/// <summary>One place for how the tool prints and writes JSON (indented, apostrophes left alone).</summary>
public static class JsonOut
{
    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Format(JsonNode? node) => node?.ToJsonString(Pretty) ?? "null";
}
