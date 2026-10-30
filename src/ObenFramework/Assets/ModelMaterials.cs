// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using UnityEngine;
using UnityEngine.Rendering;

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
    /// values, which is what Blender writes; they are converted for Unity's colour properties. An entry
    /// that is partly see-through (<c>d</c> or <c>Tr</c>) gets the Standard shader's Fade mode.
    /// </summary>
    public static Material Create(Shader shader, MtlMaterial mtl, Texture2D texture, float smoothness, string name)
    {
        var material = new Material(shader) { name = name };

        Color color = mtl == null ? Color.white : new Color(Gamma(mtl.Kd[0]), Gamma(mtl.Kd[1]), Gamma(mtl.Kd[2]), mtl.Alpha);
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
        if (mtl != null && mtl.Alpha < 1f)
            SeeThrough(material);
        return material;
    }

    // What the Standard shader's own editor sets for Fade. Other shaders keep their mode and only get the alpha.
    private static void SeeThrough(Material material)
    {
        if (!material.HasProperty("_Mode"))
            return;
        material.SetFloat("_Mode", 2f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private static float Gamma(float linear) => Mathf.LinearToGammaSpace(linear);
}
