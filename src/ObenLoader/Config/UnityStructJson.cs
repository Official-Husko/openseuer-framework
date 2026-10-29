using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenLoader.Config;

/// <summary>
/// Writes Unity's small value types (Vector2/3/4, Quaternion, Color, Color32, Vector2Int...) as their public
/// fields only: <c>{"x": 1, "y": 2, "z": 3}</c>. Newtonsoft on its own also follows their properties, and
/// <c>Vector3.normalized</c> or <c>Color.linear</c> give the same type again, which it stops on as a loop, so a
/// config or save holding one couldn't be written. Reading takes the fields that are there and leaves the
/// rest at zero. Matches by namespace, so it needs no Unity reference (and is unit tested with stand-ins).
/// </summary>
internal sealed class UnityStructJson : JsonConverter
{
    public override bool CanConvert(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (!type.IsValueType || type.IsPrimitive || type.IsEnum || type.Namespace != "UnityEngine")
            return false;
        // Structs that keep their data private (Rect, Bounds) are left to Newtonsoft's usual handling.
        return type.GetFields(BindingFlags.Instance | BindingFlags.Public).Length > 0;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            writer.WritePropertyName(field.Name);
            serializer.Serialize(writer, field.GetValue(value));
        }
        writer.WriteEndObject();
    }

    public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
    {
        var target = Nullable.GetUnderlyingType(type);
        if (reader.TokenType == JsonToken.Null)
            return target != null ? null : Activator.CreateInstance(type);
        target ??= type;

        var json = JObject.Load(reader);
        object value = Activator.CreateInstance(target);
        foreach (var field in target.GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            var token = json[field.Name];
            if (token != null && token.Type != JTokenType.Null)
                field.SetValue(value, token.ToObject(field.FieldType, serializer));
        }
        return value;
    }
}
