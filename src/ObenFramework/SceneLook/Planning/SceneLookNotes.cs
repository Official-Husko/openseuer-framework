// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ObenFramework.SceneLook;

/// <summary>
/// The rest skeleton of a look, and short notes about things in its file that often explain a look that
/// comes out the wrong size or mirrored. Rest skeletons are cached per look.
/// </summary>
internal static class SceneLookNotes
{
    private const int RestCacheMax = 64;
    private static readonly Dictionary<SceneLookLook, Dictionary<long, SceneLookMatrix>> Rests = new Dictionary<SceneLookLook, Dictionary<long, SceneLookMatrix>>();

    /// <summary>Each bone's rest transform from the look's own bind poses, keyed by transform path id.</summary>
    public static Dictionary<long, SceneLookMatrix> Rest(SceneLookLook look)
    {
        if (Rests.TryGetValue(look, out var cached))
            return cached;
        var rest = SceneLookBind.Rest(SceneLookFrame.RestMeshes(look), out _);
        if (Rests.Count >= RestCacheMax)
            Rests.Clear();
        Rests[look] = rest;
        return rest;
    }

    public static void Forget(SceneLookLook look) => Rests.Remove(look);

    public static void Clear() => Rests.Clear();

    /// <summary>Nodes outside the bind poses with a scale other than one, or "".</summary>
    public static string ScaleNote(SceneLookLook look)
    {
        if (look?.Nodes == null)
            return "";
        Dictionary<long, SceneLookMatrix> rest;
        try { rest = Rest(look); }
        catch (Exception) { return ""; }
        var parts = new List<string>();
        foreach (var node in look.Nodes)
        {
            if (rest.ContainsKey(node.Transform) || node.Scale == null || node.Scale.Length < 3)
                continue;
            if (Unit(node.Scale[0]) && Unit(node.Scale[1]) && Unit(node.Scale[2]))
                continue;
            if (parts.Count >= 3)
                break;
            parts.Add(node.Name + " (" + Number(node.Scale[0]) + ", " + Number(node.Scale[1]) + ", " + Number(node.Scale[2]) + ")");
        }
        return parts.Count == 0 ? "" : " | non unit scale on a node outside the file bind poses: " + string.Join(", ", parts.ToArray());
    }

    /// <summary>Nodes whose transform mirrors (negative determinant), or "".</summary>
    public static string AxisNote(SceneLookLook look)
    {
        if (look?.Nodes == null)
            return "";
        var parts = new List<string>();
        foreach (var node in look.Nodes)
        {
            if (node.Scale == null || node.Position == null || node.Rotation == null)
                continue;
            if (node.Scale.Length < 3 || node.Position.Length < 3 || node.Rotation.Length < 4)
                continue;
            float determinant = SceneLookFrame.Local(node).Determinant3();
            if (!(determinant < 0f))
                continue;
            if (parts.Count >= 3)
                break;
            parts.Add(node.Name + " (" + Number(determinant) + ")");
        }
        return parts.Count == 0 ? "" : " | mirrored nodes (negative determinant): " + string.Join(", ", parts.ToArray());
    }

    /// <summary>The mean axis length of a serialized bind pose.</summary>
    public static float BindScale(float[] pose)
    {
        if (pose == null || pose.Length < 11)
            return 0f;
        float x = (float)Math.Sqrt(pose[0] * pose[0] + pose[1] * pose[1] + pose[2] * pose[2]);
        float y = (float)Math.Sqrt(pose[4] * pose[4] + pose[5] * pose[5] + pose[6] * pose[6]);
        float z = (float)Math.Sqrt(pose[8] * pose[8] + pose[9] * pose[9] + pose[10] * pose[10]);
        return (x + y + z) / 3f;
    }

    public static bool Unit(float value) => value > 0.999f && value < 1.001f;

    public static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static string Number(float[] values)
    {
        var parts = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
            parts[i] = Number(values[i]);
        return string.Join(", ", parts);
    }
}
