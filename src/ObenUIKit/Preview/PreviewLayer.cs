using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenUIKit.Preview;

/// <summary>
/// The layer previews draw on: one the game leaves unnamed and none of its cameras render (layers 3, 6
/// and 7 in 0.5.13), so the main camera never sees a preview model. The scene's sun and moon light every
/// layer, so while a preview is open their light is kept off this one layer and given back afterwards;
/// nothing in the game uses the layer, so the game looks the same.
/// </summary>
internal static class PreviewLayer
{
    private static readonly int[] Preferred = { 7, 6, 3 };
    private static readonly List<Light> Masked = new List<Light>();

    private static int _layer = -1;
    private static int _users;
    private static int _scannedScene = -1;

    public static int Get()
    {
        if (_layer < 0)
            _layer = Pick();
        return _layer;
    }

    public static void Acquire() => _users++;

    public static void Release()
    {
        if (_users > 0 && --_users == 0)
            Restore();
    }

    /// <summary>Takes the scene's directional lights off the preview layer; scans once per scene.</summary>
    public static void Isolate()
    {
        int scene = SceneManager.GetActiveScene().handle;
        if (scene == _scannedScene)
            return;
        _scannedScene = scene;
        int bit = 1 << Get();
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional || (light.cullingMask & bit) == 0 || light.cullingMask == bit)
                continue;
            light.cullingMask &= ~bit;
            Masked.Add(light);
        }
    }

    private static void Restore()
    {
        int bit = 1 << Get();
        foreach (var light in Masked)
        {
            if (light != null)
                light.cullingMask |= bit;
        }
        Masked.Clear();
        _scannedScene = -1;
    }

    private static int Pick()
    {
        int seen = 0;
        foreach (var camera in Camera.allCameras)
            seen |= camera.cullingMask;
        foreach (int layer in Preferred)
        {
            if (Free(layer, seen))
                return layer;
        }
        for (int layer = 31; layer > 7; layer--)
        {
            if (Free(layer, seen))
                return layer;
        }
        return Preferred[0];
    }

    private static bool Free(int layer, int seen) => string.IsNullOrEmpty(LayerMask.LayerToName(layer)) && (seen & (1 << layer)) == 0;
}
