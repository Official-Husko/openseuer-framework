using System;
using System.Collections;
using System.Reflection;
using ObenFramework.Reflection;
using ObenFramework.Scenes;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Values;

/// <summary>
/// Converts between game values and JSON for tools. Vectors and colours are arrays
/// ([x,y,z], [r,g,b,a]), enums are names, Unity objects are short references, and other
/// objects are expanded field by field up to a depth limit.
/// </summary>
public static class JsonValues
{
    private const int MaxItems = 100;

    public static JToken ToJson(object value, int depth = 1)
    {
        switch (value)
        {
            case null: return JValue.CreateNull();
            case JToken token: return token;
            case string s: return new JValue(s);
            case bool or int or long or short or byte or uint or ulong or double or decimal: return new JValue(value);
            case float f: return new JValue(Math.Round(f, 4));
            case Enum e: return new JValue(e.ToString());
            case Vector2 v: return new JArray(R(v.x), R(v.y));
            case Vector3 v: return new JArray(R(v.x), R(v.y), R(v.z));
            case Vector4 v: return new JArray(R(v.x), R(v.y), R(v.z), R(v.w));
            case Quaternion q: return ToJson(q.eulerAngles);
            case Color c: return new JArray(R(c.r), R(c.g), R(c.b), R(c.a));
            case Type t: return new JValue(t.FullName);
            case Object o: return Reference(o);
            case Delegate d: return new JValue($"delegate ({d.GetInvocationList().Length} handlers)");
            case IDictionary dict: return depth > 0 ? Dictionary(dict, depth) : Summary(value);
            case IEnumerable list: return depth > 0 ? Array(list, depth) : Summary(value);
            default: return depth > 0 ? Fields(value, depth) : Summary(value);
        }
    }

    public static JToken Reference(Object o)
    {
        if (o == null)
            return JValue.CreateNull();
        var r = new JObject { ["name"] = o.name, ["type"] = o.GetType().Name, ["id"] = "#" + o.GetInstanceID() };
        if (o is Component c)
            r["path"] = SceneQuery.PathOf(c.gameObject);
        else if (o is GameObject go)
            r["path"] = SceneQuery.PathOf(go);
        return r;
    }

    public static object FromJson(JToken token, Type type)
    {
        if (token == null || token.Type == JTokenType.Null)
            return type.IsValueType ? Activator.CreateInstance(type) : null;

        if (type == typeof(Vector3)) return new Vector3(F(token, 0, "x"), F(token, 1, "y"), F(token, 2, "z"));
        if (type == typeof(Vector2)) return new Vector2(F(token, 0, "x"), F(token, 1, "y"));
        if (type == typeof(Quaternion)) return Quaternion.Euler(F(token, 0, "x"), F(token, 1, "y"), F(token, 2, "z"));
        if (type == typeof(Color)) return new Color(F(token, 0, "r"), F(token, 1, "g"), F(token, 2, "b"), token.Type == JTokenType.Array && ((JArray)token).Count > 3 ? F(token, 3, "a") : 1f);
        if (type.IsEnum) return token.Type == JTokenType.String ? Enum.Parse(type, token.Value<string>(), true) : Enum.ToObject(type, token.Value<int>());
        if (type == typeof(Type)) return TypeFinder.Require(token.Value<string>());

        if (typeof(Object).IsAssignableFrom(type) && token.Type == JTokenType.String)
        {
            var go = SceneQuery.Resolve(token.Value<string>());
            if (type == typeof(GameObject)) return go;
            if (type == typeof(Transform)) return go.transform;
            return go.GetComponent(type) ?? throw new ArgumentException($"{go.name} has no {type.Name}.");
        }

        return token.ToObject(type);
    }

    private static JArray Array(IEnumerable list, int depth)
    {
        var array = new JArray();
        foreach (var item in list)
        {
            if (array.Count >= MaxItems)
            {
                array.Add("...");
                break;
            }
            array.Add(ToJson(item, depth - 1));
        }
        return array;
    }

    private static JObject Dictionary(IDictionary dict, int depth)
    {
        var obj = new JObject();
        foreach (DictionaryEntry entry in dict)
        {
            if (obj.Count >= MaxItems)
                break;
            obj[entry.Key?.ToString() ?? "null"] = ToJson(entry.Value, depth - 1);
        }
        return obj;
    }

    private static JObject Fields(object value, int depth)
    {
        var obj = new JObject { ["$type"] = value.GetType().Name };
        // Walk the base classes too: private fields of a base type aren't returned for the derived type.
        for (var type = value.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.Name.Contains("<") || obj.ContainsKey(field.Name))
                    continue;
                try { obj[field.Name] = ToJson(field.GetValue(value), depth - 1); }
                catch (Exception e) { obj[field.Name] = "<" + e.GetType().Name + ">"; }
            }
        }
        return obj;
    }

    private static JValue Summary(object value) => new JValue($"<{value.GetType().Name}>");

    private static double R(float f) => Math.Round(f, 4);

    private static float F(JToken token, int index, string name) =>
        token.Type == JTokenType.Array ? token[index].Value<float>() : token[name]?.Value<float>() ?? 0f;
}
