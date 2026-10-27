using System.Text.Json.Nodes;

namespace ObenTool.Mcp;

/// <summary>Tiny builder for JSON Schema objects used as tool inputs.</summary>
public sealed class Schema
{
    private readonly JsonObject _properties = new();
    private readonly JsonArray _required = new();

    public static Schema Object() => new();

    public Schema Str(string name, string description, bool required = false) => Add(name, "string", description, required);
    public Schema Num(string name, string description, bool required = false) => Add(name, "number", description, required);
    public Schema Int(string name, string description, bool required = false) => Add(name, "integer", description, required);
    public Schema Bool(string name, string description, bool required = false) => Add(name, "boolean", description, required);
    public Schema Obj(string name, string description, bool required = false) => Add(name, "object", description, required);
    public Schema Any(string name, string description, bool required = false)
    {
        _properties[name] = new JsonObject { ["description"] = description };
        if (required)
            _required.Add(name);
        return this;
    }

    public Schema Arr(string name, string description, bool required = false)
    {
        _properties[name] = new JsonObject { ["type"] = "array", ["description"] = description };
        if (required)
            _required.Add(name);
        return this;
    }

    private Schema Add(string name, string type, string description, bool required)
    {
        _properties[name] = new JsonObject { ["type"] = type, ["description"] = description };
        if (required)
            _required.Add(name);
        return this;
    }

    public static implicit operator JsonObject(Schema s)
    {
        var obj = new JsonObject { ["type"] = "object", ["properties"] = s._properties.DeepClone() };
        if (s._required.Count > 0)
            obj["required"] = s._required.DeepClone();
        return obj;
    }
}
