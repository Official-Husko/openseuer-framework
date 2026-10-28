// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>Reads materials and names the shaders they use.</summary>
internal static class SceneLookMaterialReader
{
    // Shader names looked for inside a shader object; the first found names it.
    private static readonly string[] ShaderNames =
    {
        "Standard (Specular setup)", "Standard", "Unlit/Texture", "Unlit/Color", "Legacy Shaders/Diffuse",
        "Legacy Shaders/Bumped Diffuse", "Mobile/Diffuse", "Sprites/Default", "UI/Default", "Skybox/Cubemap",
        "Particles/Standard Unlit",
    };

    public static SceneLookMaterial ParseMaterial(SceneLookFile f, long pathId)
    {
        var c = new SceneLookCursor(f.ReadObject(pathId), 0);
        var material = new SceneLookMaterial { PathId = pathId, FileName = f.Name, Name = c.Text(), Shader = c.Pptr() };
        int valid = c.Count();
        for (int i = 0; i < valid; i++)
            c.Text(); // valid keywords
        int invalid = c.Count();
        for (int i = 0; i < invalid; i++)
            c.Text(); // invalid keywords
        c.U32();      // lightmap flags
        c.U8();       // enable instancing
        c.Align(4);
        material.RenderQueue = c.I32();
        int tags = c.Count();
        for (int i = 0; i < tags; i++)
        {
            c.Text();
            c.Text();
        }
        int passes = c.Count();
        for (int i = 0; i < passes; i++)
            c.Text(); // disabled passes

        int texEnvs = c.Count();
        material.TexEnvs = new List<SceneLookTexEnv>(Math.Min(texEnvs, 64));
        for (int i = 0; i < texEnvs; i++)
            material.TexEnvs.Add(new SceneLookTexEnv { Name = c.Text(), Texture = c.Pptr(), Scale = c.Floats(2), Offset = c.Floats(2) });

        int ints = c.Count();
        material.Ints = new List<SceneLookNamed<int>>(Math.Min(ints, 64));
        for (int i = 0; i < ints; i++)
            material.Ints.Add(new SceneLookNamed<int> { Name = c.Text(), Value = c.I32() });

        int floats = c.Count();
        material.Floats = new List<SceneLookNamed<float>>(Math.Min(floats, 64));
        for (int i = 0; i < floats; i++)
            material.Floats.Add(new SceneLookNamed<float> { Name = c.Text(), Value = c.F32() });

        int colors = c.Count();
        material.Colors = new List<SceneLookNamed<float[]>>(Math.Min(colors, 64));
        for (int i = 0; i < colors; i++)
            material.Colors.Add(new SceneLookNamed<float[]> { Name = c.Text(), Value = c.Floats(4) });
        return material;
    }

    /// <summary>A known shader name found in the shader object, else "unnamed" with its first property names.</summary>
    public static string ShaderLabel(SceneLookFile f, long pathId)
    {
        if (f.Memo.ShaderLabels.TryGetValue(pathId, out var cached))
            return cached;
        string label = ShaderLabelOf(f.ReadObject(pathId));
        f.Memo.ShaderLabels[pathId] = label;
        return label;
    }

    private static string ShaderLabelOf(byte[] bytes)
    {
        foreach (string name in ShaderNames)
        {
            if (SceneLookReader.Contains(bytes, SceneLookReader.LengthPrefixed(name)))
                return name;
        }
        try
        {
            var props = ShaderProperties(bytes);
            return "unnamed [" + string.Join(",", props.GetRange(0, Math.Min(3, props.Count))) + "]";
        }
        catch (Exception)
        {
            return "unnamed";
        }
    }

    private static List<string> ShaderProperties(byte[] bytes)
    {
        var c = new SceneLookCursor(bytes, 0);
        c.Text();
        int count = c.Count();
        var names = new List<string>();
        for (int i = 0; i < count; i++)
        {
            names.Add(c.Text()); // name
            c.Text();            // description
            int attributes = c.Count();
            for (int j = 0; j < attributes; j++)
                c.Text();
            c.I32();     // type
            c.U32();     // flags
            c.Skip(16);  // default values
            c.Text();    // default texture name
            c.I32();     // texture dimension
        }
        return names;
    }
}
