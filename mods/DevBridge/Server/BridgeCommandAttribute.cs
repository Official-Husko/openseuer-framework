using System;

namespace DevBridge.Server;

/// <summary>
/// Marks a static method as a bridge command. The method takes a <see cref="Newtonsoft.Json.Linq.JObject"/>
/// of parameters and returns anything JSON-friendly (or a <see cref="Deferred"/> for async work).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BridgeCommandAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }

    /// <summary>Parameter docs, e.g. "name: item prefab name", "count?: how many".</summary>
    public string[] Params { get; set; } = new string[0];

    public BridgeCommandAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
