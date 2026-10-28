// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>
/// Makes the materials of a model. The game renders almost everything with Unity's built-in Standard
/// shader (the build has no scriptable render pipeline), so that is what models get. If a build ever
/// strips it, simpler shaders that are still present are tried in turn.
/// </summary>
internal static class ModelMaterials
{
    private static readonly string[] Fallbacks =
    {
        "Standard",
        "Legacy Shaders/Diffuse",
        "Unlit/Texture",
        "Sprites/Default",
    };

    private static Shader _default;

    public static void Reset() => _default = null;

    /// <summary>The shader for a name, or the best available default when the name is null.</summary>
    public static Shader Pick(string name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            var named = Shader.Find(name);
            if (named == null)
                throw new System.ArgumentException($"No shader named '{name}' is in the game.");
            return named;
        }

        if (_default != null)
            return _default;
        foreach (string candidate in Fallbacks)
        {
            var shader = Shader.Find(candidate);
            if (shader != null && shader.isSupported)
            {
                if (candidate != Fallbacks[0])
                    AssetsLog.Warn($"Standard shader not found, models use '{candidate}'.");
                _default = shader;
                return shader;
            }
        }
        throw new System.InvalidOperationException("The game has none of the shaders models need (Standard, Diffuse, Unlit).");
    }

    /// <summary>
    /// Builds one material from an MTL entry (null for the default white). The MTL colours are linear
    /// values, which is what Blender writes; they are converted for Unity's colour properties.
    /// </summary>
    public static Material Create(Shader shader, MtlMaterial mtl, Texture2D texture, float smoothness, string name)
    {
        var material = new Material(shader) { name = name };

        Color color = mtl == null ? Color.white : new Color(Gamma(mtl.Kd[0]), Gamma(mtl.Kd[1]), Gamma(mtl.Kd[2]), 1f);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (texture != null)
        {
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
        }

        // Standard's defaults are shinier than most hand-made models expect.
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        if (mtl != null && (mtl.Ke[0] > 0f || mtl.Ke[1] > 0f || mtl.Ke[2] > 0f) && material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", new Color(mtl.Ke[0], mtl.Ke[1], mtl.Ke[2], 1f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return material;
    }

    private static float Gamma(float linear) => Mathf.LinearToGammaSpace(linear);
}
