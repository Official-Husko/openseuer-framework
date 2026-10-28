// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.SceneLook;

/// <summary>Builds a look's textures and materials once and shares them between the bodies wearing it.</summary>
internal static class SceneLookApplyTexture
{
    private const string MainTex = "_MainTex";
    private const string BumpMap = "_BumpMap";
    private const string Mode = "_Mode";
    private const string Standard = "Standard";
    private const string NormalMap = "_NORMALMAP";

    public static Texture2D Texture(SceneLookAssets assets, SceneLookTexture source, bool linear, out string reason)
    {
        reason = null;
        string key = source.FileName + "|" + source.PathId + (linear ? "|linear" : "|srgb");
        if (assets.Textures.TryGetValue(key, out var cached))
            return cached;
        if (!SceneLookTextureReader.IsSupported(source.Format))
        {
            reason = "format " + SceneLookTextureReader.FormatLabel(source.Format) + " not supported";
            return null;
        }
        if (SceneLookTextureReader.ExpectedBytes(source.Format, source.Width, source.Height, source.Mips) != source.CompleteSize)
        {
            reason = "texture " + source.Name + " size rule failed";
            return null;
        }

        byte[] pixels;
        try
        {
            if (!source.Inline && !assets.Files.Installed(source.StreamPath, source.StreamOffset + source.StreamSize))
            {
                reason = "external missing " + source.StreamPath;
                return null;
            }
            pixels = SceneLookTextureReader.TexturePixels(assets.Files, source);
        }
        catch (Exception e)
        {
            reason = e.Message;
            return null;
        }
        if (pixels.Length != source.CompleteSize)
        {
            reason = "texture " + source.Name + " has " + pixels.Length + " pixel bytes";
            return null;
        }

        var texture = new Texture2D(source.Width, source.Height, (TextureFormat)source.Format, source.Mips > 0 ? source.Mips : 1, linear) { name = source.Name };
        try
        {
            texture.LoadRawTextureData(pixels);
            texture.Apply(false, true);
        }
        catch (Exception e)
        {
            Object.Destroy(texture);
            reason = e.Message;
            return null;
        }
        assets.Textures[key] = texture;
        return texture;
    }

    /// <summary>
    /// A material for one of the look's materials. It uses the body's own shader when that shader has every
    /// property the source sets (so the look matches the body's lighting), otherwise Standard.
    /// </summary>
    public static Material Material(SceneLookAssets assets, SceneLookMaterial source, Material target, out string reason)
    {
        reason = null;
        if (source == null)
            return null;
        var shader = ShaderFor(source, target);
        if (shader == null)
        {
            reason = "no shader for material " + source.Name;
            return null;
        }
        string key = source.FileName + "|" + source.PathId + "|" + shader.name;
        if (assets.Materials.TryGetValue(key, out var cached))
            return cached;

        var material = new Material(shader) { name = source.Name };
        foreach (var env in source.TexEnvs)
        {
            if (env.Resolved == null || (env.Name != MainTex && env.Name != BumpMap))
                continue;
            bool linear = env.Name == BumpMap;
            var texture = Texture(assets, env.Resolved, linear, out reason);
            if (reason != null)
            {
                Object.Destroy(material);
                return null;
            }
            material.SetTexture(env.Name, texture);
            material.SetTextureScale(env.Name, new Vector2(env.Scale[0], env.Scale[1]));
            material.SetTextureOffset(env.Name, new Vector2(env.Offset[0], env.Offset[1]));
            if (linear && shader.name == Standard)
                material.EnableKeyword(NormalMap);
        }
        Copy(material, source);
        assets.Materials[key] = material;
        return material;
    }

    /// <summary>A plain material for a renderer whose source has none.</summary>
    public static Material Plain(SceneLookAssets assets, Material target, out string reason)
    {
        reason = null;
        var shader = Shader.Find(Standard);
        if (shader == null && target != null)
            shader = target.shader;
        if (shader == null)
        {
            reason = "no shader for the look";
            return null;
        }
        string key = "plain|" + shader.name;
        if (assets.Materials.TryGetValue(key, out var cached))
            return cached;
        var material = new Material(shader) { name = assets.Look.FileName + " default" };
        assets.Materials[key] = material;
        return material;
    }

    private static Shader ShaderFor(SceneLookMaterial source, Material target)
    {
        if (target != null && target.shader != null && HasProperties(source, target))
            return target.shader;
        var standard = Shader.Find(Standard);
        if (standard != null)
            return standard;
        return target != null ? target.shader : null;
    }

    private static bool HasProperties(SceneLookMaterial source, Material target)
    {
        foreach (var env in source.TexEnvs)
        {
            if (env.Resolved != null && !target.HasProperty(env.Name))
                return false;
        }
        foreach (var value in source.Floats)
        {
            if (!target.HasProperty(value.Name))
                return false;
        }
        foreach (var value in source.Ints)
        {
            if (!target.HasProperty(value.Name))
                return false;
        }
        foreach (var value in source.Colors)
        {
            if (!target.HasProperty(value.Name))
                return false;
        }
        return true;
    }

    // The Standard shader's _Mode needs matching keywords and render states; copying the number alone breaks it.
    private static void Copy(Material material, SceneLookMaterial source)
    {
        foreach (var value in source.Floats)
        {
            if (value.Name != Mode && material.HasProperty(value.Name))
                material.SetFloat(value.Name, value.Value);
        }
        foreach (var value in source.Ints)
        {
            if (value.Name != Mode && material.HasProperty(value.Name))
                material.SetInt(value.Name, value.Value);
        }
        foreach (var value in source.Colors)
        {
            if (value.Value != null && value.Value.Length >= 4 && material.HasProperty(value.Name))
                material.SetColor(value.Name, new Color(value.Value[0], value.Value[1], value.Value[2], value.Value[3]));
        }
    }
}
