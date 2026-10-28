// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AzureSky;

namespace ObenFramework.Env;

/// <summary>
/// The sky's renderer and its sun lights (Azure's directional light, the interiors' <c>SunDirection</c>
/// lights), found by a scan every couple of seconds while an override holds, and the sun's two channels
/// per light.
/// </summary>
internal static class SkySun
{
    private const int ScanFrames = 120;
    private static readonly Light[] NoLights = new Light[0];
    private static readonly AccessTools.FieldRef<AzureTimeController, Transform> DirectionalLight =
        AccessTools.FieldRefAccess<AzureTimeController, Transform>("m_directionalLight");

    private static readonly Dictionary<Light, SkyChannel<float>> Intensity = new Dictionary<Light, SkyChannel<float>>();
    private static readonly Dictionary<Light, SkyChannel<Color>> Tint = new Dictionary<Light, SkyChannel<Color>>();
    private static readonly List<Light> Released = new List<Light>();
    private static Light[] _lights = NoLights;
    private static int _nextScan;

    /// <summary>The scene's sky dome renderer, or null (interiors).</summary>
    public static AzureSkyRenderController Render { get; private set; }

    public static bool Holding => Intensity.Count > 0;

    /// <summary>The first sun light, for reading.</summary>
    public static Light First
    {
        get
        {
            foreach (var light in _lights)
            {
                if (light != null)
                    return light;
            }
            return null;
        }
    }

    public static void Scan()
    {
        if (Time.frameCount < _nextScan)
            return;
        _nextScan = Time.frameCount + ScanFrames;
        Render = UnityEngine.Object.FindFirstObjectByType<AzureSkyRenderController>();
        var found = new List<Light>();
        foreach (var time in UnityEngine.Object.FindObjectsByType<AzureTimeController>(FindObjectsSortMode.None))
        {
            var sun = DirectionalLight(time);
            Add(found, sun != null ? sun.GetComponent<Light>() : null);
        }
        foreach (var sun in UnityEngine.Object.FindObjectsByType<SunDirection>(FindObjectsSortMode.None))
            Add(found, sun.GetComponent<Light>());
        _lights = found.ToArray();
    }

    /// <summary>The scene changed: everything held belongs to destroyed objects.</summary>
    public static void ForgetScene()
    {
        _nextScan = 0;
        Render = null;
        _lights = NoLights;
        Apply(null, null);
    }

    public static void Apply(float? intensity, Color? color)
    {
        if (intensity.HasValue || color.HasValue)
        {
            foreach (var light in _lights)
            {
                if (light == null || Intensity.ContainsKey(light))
                    continue;
                Intensity[light] = new SkyChannel<float>(t => ((Light)t).intensity, (t, v) => ((Light)t).intensity = v);
                Tint[light] = new SkyChannel<Color>(t => ((Light)t).color, (t, v) => ((Light)t).color = v);
            }
        }

        Released.Clear();
        foreach (var pair in Intensity)
        {
            var light = pair.Key;
            if (light == null)
            {
                Released.Add(light);
                continue;
            }
            bool scanned = Array.IndexOf(_lights, light) >= 0;
            var tint = Tint[light];
            pair.Value.Apply(light, scanned ? intensity : null);
            tint.Apply(light, scanned ? color : null);
            if (!pair.Value.Held && !tint.Held)
                Released.Add(light);
        }
        foreach (var light in Released)
        {
            Intensity.Remove(light);
            Tint.Remove(light);
        }
        Released.Clear();
    }

    /// <summary>Suspends or captures every light channel around a game writer.</summary>
    public static void Sweep(bool capture)
    {
        foreach (var pair in Intensity)
        {
            if (pair.Key == null)
                continue;
            SkyApply.Step(pair.Value, capture);
            SkyApply.Step(Tint[pair.Key], capture);
        }
    }

    private static void Add(List<Light> found, Light light)
    {
        if (light != null && !found.Contains(light))
            found.Add(light);
    }
}
