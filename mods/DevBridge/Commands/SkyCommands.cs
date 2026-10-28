using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Env;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Sky overrides through ObenFramework.Env.WorldSky, held by the bridge (or the mod named in 'mod').</summary>
internal static class SkyCommands
{
    [BridgeCommand("sky.get", "What the sky shows now (fog, ambient, dome, sun) and the overrides in place.")]
    private static object SkyGet(JObject p)
    {
        var s = WorldSky.Current;
        var overrides = new JArray();
        foreach (var entry in WorldSky.All())
            overrides.Add(new JObject { ["mod"] = entry.OwnerId, ["parts"] = Parts(entry.Value) });
        return new JObject
        {
            ["fog"] = s.FogEnabled, ["fogMode"] = s.FogMode.ToString(), ["fogColor"] = ColorJson(s.FogColor), ["fogDensity"] = s.FogDensity,
            ["fogStart"] = s.FogStart, ["fogEnd"] = s.FogEnd, ["ambient"] = ColorJson(s.AmbientColor), ["ambientIntensity"] = s.AmbientIntensity,
            ["exposure"] = s.SkyboxExposure, ["sunColor"] = s.SunColor.HasValue ? ColorJson(s.SunColor.Value) : null, ["sunIntensity"] = s.SunIntensity,
            ["stars"] = s.StarsIntensity, ["clouds"] = s.CloudDensity, ["overrides"] = overrides,
        };
    }

    [BridgeCommand("sky.override", "Override parts of the sky until sky.clear; parts left out keep the game's value.",
        Params = new[]
        {
            "fog?: true/false", "fogMode?: Linear, Exponential or ExponentialSquared", "fogColor?: [r,g,b]", "fogDensity?: number",
            "fogStart?/fogEnd?: metres", "ambient?: [r,g,b]", "ambientIntensity?: number", "exposure?: number", "sunColor?: [r,g,b]",
            "sunIntensity?: number", "stars?: number", "clouds?: 0-1", "mod?: owner, default the bridge",
        })]
    private static object SkyOverride(JObject p)
    {
        var values = new SkyOverride
        {
            FogEnabled = p["fog"]?.Value<bool>(),
            FogMode = p["fogMode"] != null ? (FogMode)Enum.Parse(typeof(FogMode), p.Str("fogMode"), true) : (FogMode?)null,
            FogColor = ColorOf(p["fogColor"]),
            FogDensity = p["fogDensity"]?.Value<float>(),
            FogStart = p["fogStart"]?.Value<float>(),
            FogEnd = p["fogEnd"]?.Value<float>(),
            AmbientColor = ColorOf(p["ambient"]),
            AmbientIntensity = p["ambientIntensity"]?.Value<float>(),
            SkyboxExposure = p["exposure"]?.Value<float>(),
            SunColor = ColorOf(p["sunColor"]),
            SunIntensity = p["sunIntensity"]?.Value<float>(),
            StarsIntensity = p["stars"]?.Value<float>(),
            CloudDensity = p["clouds"]?.Value<float>(),
        };
        if (values.IsEmpty)
            throw new ArgumentException("Give at least one part of the sky to override.");
        WorldSky.Set(BridgeOwner.Of(p), values);
        return SkyGet(p);
    }

    [BridgeCommand("sky.clear", "End the bridge's (or a mod's) sky override.", Params = new[] { "mod?: owner, default the bridge" })]
    private static object SkyClear(JObject p)
    {
        bool cleared = WorldSky.Clear(BridgeOwner.Of(p));
        var result = (JObject)SkyGet(p);
        result["cleared"] = cleared;
        return result;
    }

    private static JArray Parts(SkyOverride o)
    {
        var parts = new JArray();
        if (o.FogEnabled.HasValue) parts.Add("fog");
        if (o.FogMode.HasValue) parts.Add("fogMode");
        if (o.FogColor.HasValue) parts.Add("fogColor");
        if (o.FogDensity.HasValue) parts.Add("fogDensity");
        if (o.FogStart.HasValue) parts.Add("fogStart");
        if (o.FogEnd.HasValue) parts.Add("fogEnd");
        if (o.AmbientColor.HasValue) parts.Add("ambient");
        if (o.AmbientIntensity.HasValue) parts.Add("ambientIntensity");
        if (o.SkyboxExposure.HasValue) parts.Add("exposure");
        if (o.SunColor.HasValue) parts.Add("sunColor");
        if (o.SunIntensity.HasValue) parts.Add("sunIntensity");
        if (o.StarsIntensity.HasValue) parts.Add("stars");
        if (o.CloudDensity.HasValue) parts.Add("clouds");
        return parts;
    }

    private static Color? ColorOf(JToken token) =>
        token is JArray c && c.Count >= 3 ? new Color(c[0].Value<float>(), c[1].Value<float>(), c[2].Value<float>(), c.Count > 3 ? c[3].Value<float>() : 1f) : (Color?)null;

    private static JArray ColorJson(Color c) => new JArray(Round(c.r), Round(c.g), Round(c.b), Round(c.a));

    private static float Round(float v) => (float)Math.Round(v, 3);
}
