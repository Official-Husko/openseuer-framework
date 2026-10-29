// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenUIKit.Theme;

/// <summary>
/// Sprites and textures made in code for uGUI of a mod's own (the kit's windows use the game's square look):
/// rounded rectangles that slice to any size, and vertical gradients for a backdrop. Each is made once and
/// shared; the kit destroys them when it unloads.
/// </summary>
public static class UiSprites
{
    private const int MaxRadius = 64;
    private const int GradientSteps = 64;

    private static readonly Dictionary<int, Sprite> Rounded = new Dictionary<int, Sprite>();
    private static readonly Dictionary<long, Texture2D> Gradients = new Dictionary<long, Texture2D>();

    /// <summary>
    /// A white rounded rectangle with corners of <paramref name="radius"/> pixels (0 to 64) and smooth edges, set up
    /// to slice: put it on an <c>Image</c> with <c>type = Sliced</c> and tint it with the Image's colour.
    /// </summary>
    public static Sprite RoundedRect(int radius)
    {
        radius = Mathf.Clamp(radius, 0, MaxRadius);
        if (Rounded.TryGetValue(radius, out var sprite) && sprite != null)
            return sprite;
        sprite = BuildRounded(radius);
        Rounded[radius] = sprite;
        return sprite;
    }

    /// <summary>A 1 by 64 texture fading from <paramref name="bottom"/> to <paramref name="top"/>, for a <c>RawImage</c>.</summary>
    public static Texture2D Gradient(Color bottom, Color top)
    {
        long key = (long)Color32Key(bottom) << 32 | Color32Key(top);
        if (Gradients.TryGetValue(key, out var texture) && texture != null)
            return texture;
        texture = new Texture2D(1, GradientSteps, TextureFormat.RGBA32, false)
        {
            name = "ObenUIKit gradient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };
        for (int y = 0; y < GradientSteps; y++)
            texture.SetPixel(0, y, Color.Lerp(bottom, top, y / (GradientSteps - 1f)));
        texture.Apply(false, true);
        Gradients[key] = texture;
        return texture;
    }

    /// <summary>Destroys everything made so far (the kit does this when it unloads).</summary>
    internal static void Clear()
    {
        foreach (var sprite in Rounded.Values)
        {
            if (sprite == null)
                continue;
            Object.Destroy(sprite.texture);
            Object.Destroy(sprite);
        }
        foreach (var texture in Gradients.Values)
        {
            if (texture != null)
                Object.Destroy(texture);
        }
        Rounded.Clear();
        Gradients.Clear();
    }

    // The alpha falls off over one pixel at the rounded edge, measured from a box shrunk by the radius.
    private static Sprite BuildRounded(int radius)
    {
        int size = radius * 2 + 2;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ObenUIKit rounded " + radius,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset,
        };
        float half = size * 0.5f;
        float inner = half - radius;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - outside) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    private static uint Color32Key(Color color)
    {
        Color32 c = color;
        return (uint)(c.r << 24 | c.g << 16 | c.b << 8 | c.a);
    }
}
