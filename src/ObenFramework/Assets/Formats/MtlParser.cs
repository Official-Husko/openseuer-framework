// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ObenFramework.Assets;

/// <summary>One <c>newmtl</c> block of an MTL file.</summary>
internal sealed class MtlMaterial
{
    public string Name;

    /// <summary>Diffuse colour (linear RGB as written in the file).</summary>
    public float[] Kd = { 1f, 1f, 1f };

    /// <summary>Absolute path of the diffuse texture, only set when it is an image inside the model folder.</summary>
    public string MapKd;

    /// <summary>Emission colour.</summary>
    public float[] Ke = new float[3];

    /// <summary>Opacity from <c>d</c> or <c>Tr</c>; 1 is solid.</summary>
    public float Alpha = 1f;
}

/// <summary>The materials of an MTL file and anything that was left out and why.</summary>
internal sealed class MtlData
{
    public readonly List<MtlMaterial> Materials = new List<MtlMaterial>();
    public readonly List<string> Warnings = new List<string>();

    public MtlMaterial Find(string name)
    {
        if (name == null)
            return null;
        foreach (var material in Materials)
        {
            if (material.Name == name)
                return material;
        }
        return null;
    }
}

/// <summary>
/// Reads the parts of an MTL file a mesh needs: <c>Kd</c>, <c>Ke</c>, <c>map_Kd</c>, <c>d</c> and
/// <c>Tr</c>. Texture paths are only kept when they point at a PNG or JPG inside the model folder.
/// </summary>
internal static class MtlParser
{
    public const long MaxFileBytes = 1024L * 1024;
    private static readonly char[] Blanks = { ' ', '\t' };
    private static readonly string[] Reserved =
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Reads an MTL file. Problems become warnings and the result is empty, never an exception.</summary>
    public static MtlData Parse(string mtlPath, string modelFolder)
    {
        var data = new MtlData();
        try
        {
            if (!File.Exists(mtlPath))
            {
                data.Warnings.Add("mtl file not found: " + mtlPath);
                return data;
            }
            if (new FileInfo(mtlPath).Length > MaxFileBytes)
            {
                data.Warnings.Add("mtl file is larger than 1 MB; no materials used");
                return data;
            }
            return ParseText(File.ReadAllText(mtlPath), Path.GetDirectoryName(mtlPath), modelFolder);
        }
        catch (Exception e)
        {
            data.Warnings.Add("mtl not read: " + e.Message);
            return data;
        }
    }

    /// <summary>Parses MTL text. Texture paths are resolved against <paramref name="mtlFolder"/> and must stay inside <paramref name="modelFolder"/>.</summary>
    public static MtlData ParseText(string text, string mtlFolder, string modelFolder)
    {
        var data = new MtlData();
        MtlMaterial current = null;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            string[] t = line.Split(Blanks, StringSplitOptions.RemoveEmptyEntries);
            if (t[0] == "newmtl")
            {
                current = new MtlMaterial { Name = line.Substring(t[0].Length).Trim() };
                data.Materials.Add(current);
            }
            else if (current != null)
            {
                Statement(data, current, line, t, mtlFolder, modelFolder);
            }
        }
        return data;
    }

    private static void Statement(MtlData data, MtlMaterial m, string line, string[] t, string mtlFolder, string modelFolder)
    {
        switch (t[0])
        {
            case "Kd":
                float[] kd = Color(t);
                if (kd != null) m.Kd = kd;
                else data.Warnings.Add("material " + m.Name + ": bad Kd line ignored");
                break;
            case "Ke":
                float[] ke = Color(t);
                if (ke != null) m.Ke = ke;
                else data.Warnings.Add("material " + m.Name + ": bad Ke line ignored");
                break;
            case "map_Kd":
                m.MapKd = MapPath(data, m, MtlMapOptions.StripOptions(line.Substring(t[0].Length)), mtlFolder, modelFolder);
                break;
            case "d":
                if (TryFloat(t[t.Length - 1], out float d))
                    m.Alpha = Clamp01(d);
                break;
            case "Tr":
                if (TryFloat(t[t.Length - 1], out float tr))
                    m.Alpha = Clamp01(1f - tr);
                break;
        }
    }

    private static float[] Color(string[] t)
    {
        if (t.Length < 4)
            return null;
        var c = new float[3];
        for (int i = 0; i < 3; i++)
        {
            if (!TryFloat(t[i + 1], out c[i]))
                return null;
        }
        return c;
    }

    private static bool TryFloat(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

    private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    private static bool ImageFile(string rel)
    {
        if (rel.IndexOf(':') >= 0)
            return false;
        string ext = Path.GetExtension(rel).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
            return false;
        foreach (string segment in rel.Split('/'))
        {
            string stem = segment.Split('.')[0].ToUpperInvariant();
            if (Array.IndexOf(Reserved, stem) >= 0)
                return false;
        }
        return true;
    }

    private static string MapPath(MtlData data, MtlMaterial m, string token, string mtlFolder, string modelFolder)
    {
        string rel = token.Replace('\\', '/');
        try
        {
            string name = Path.GetFileName(rel);
            if (name.Length > 0 && name != "." && name != ".." && !Path.IsPathRooted(rel) && ImageFile(rel))
            {
                string full = Path.GetFullPath(Path.Combine(mtlFolder, rel));
                string root = Path.GetFullPath(modelFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return full;
            }
        }
        catch (Exception)
        {
            // Falls through to the warning.
        }
        data.Warnings.Add("material " + m.Name + ": map_Kd not used (no file, not a .png/.jpg/.jpeg, or outside the model folder): " + token);
        return null;
    }
}
