using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenLoader.Config;

/// <summary>
/// The parts of <see cref="ConfigFile{T}"/> that don't need the game: snapshots for the change log and putting
/// the defaults back. Unit tested.
/// </summary>
internal static class ConfigValues
{
    private const int MaxShown = 60;
    private static readonly JsonSerializer Serializer = JsonSerializer.Create(JsonFile.Settings);

    /// <summary>The settings as JSON, to compare with later.</summary>
    public static JObject Snapshot(object value) => JObject.FromObject(value, Serializer);

    /// <summary>"Speed 1 -> 2, Enabled false -> true" for every top-level value that differs, or null.</summary>
    public static string Changes(JObject before, JObject after)
    {
        var parts = new List<string>();
        foreach (var property in after.Properties())
        {
            var old = before[property.Name];
            if (!JToken.DeepEquals(old, property.Value))
                parts.Add($"{property.Name} {Show(old)} -> {Show(property.Value)}");
        }
        foreach (var property in before.Properties())
        {
            if (after[property.Name] == null)
                parts.Add($"{property.Name} {Show(property.Value)} -> (none)");
        }
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>Copies every public field and settable public property of <paramref name="from"/> onto <paramref name="to"/>.</summary>
    public static void CopyInto(object from, object to)
    {
        var type = to.GetType();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!field.IsInitOnly && !field.IsLiteral)
                field.SetValue(to, field.GetValue(from));
        }
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.CanRead && property.GetSetMethod() != null && property.GetIndexParameters().Length == 0)
                property.SetValue(to, property.GetValue(from));
        }
    }

    private static string Show(JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return "(none)";
        string text = token.Type == JTokenType.String ? "\"" + token + "\"" : token.ToString(Formatting.None);
        return text.Length > MaxShown ? text.Substring(0, MaxShown - 3) + "..." : text;
    }
}
