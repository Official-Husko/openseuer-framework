using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DevBridge.Server;

/// <summary>Typed access to request parameters with clear errors for missing ones.</summary>
public static class Params
{
    public static string Str(this JObject p, string name) =>
        p.Value<string>(name) ?? throw new ArgumentException($"Missing parameter '{name}'.");

    public static string Str(this JObject p, string name, string fallback) => p.Value<string>(name) ?? fallback;

    public static float Float(this JObject p, string name) =>
        p[name]?.Value<float>() ?? throw new ArgumentException($"Missing parameter '{name}'.");

    public static float Float(this JObject p, string name, float fallback) => p[name]?.Value<float>() ?? fallback;

    public static int Int(this JObject p, string name, int fallback) => p[name]?.Value<int>() ?? fallback;

    public static bool Bool(this JObject p, string name) =>
        p[name]?.Value<bool>() ?? throw new ArgumentException($"Missing parameter '{name}'.");

    public static bool Bool(this JObject p, string name, bool fallback) => p[name]?.Value<bool>() ?? fallback;

    public static JArray Array(this JObject p, string name) => p[name] as JArray ?? new JArray();

    /// <summary>A position given as x/y/z fields or as a "pos": [x,y,z] array.</summary>
    public static Vector3? Position(this JObject p)
    {
        if (p["pos"] is JArray a && a.Count >= 3)
            return new Vector3(a[0].Value<float>(), a[1].Value<float>(), a[2].Value<float>());
        if (p["x"] != null && p["y"] != null && p["z"] != null)
            return new Vector3(p.Float("x"), p.Float("y"), p.Float("z"));
        return null;
    }
}
