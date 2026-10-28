// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenLoader;
using UnityEngine;
using UnityEngine.UI;

namespace ObenFramework.Env;

/// <summary>
/// A coloured darkening towards the screen edges, drawn on the framework's own overlay canvas below the
/// game's HUD. The game's own vignette (Beautify) is left alone. The newest mod's vignette is shown; the
/// overlay fades between values and disappears when no mod holds one.
/// </summary>
internal static class ScreenVignette
{
    private const int SortOrder = -1000;
    private const int TextureSize = 256;
    private const float MinReach = 0.5f;
    private const float MaxReach = 1f;

    // r, g, b, a, strength
    private static readonly FadeTrack Track = new FadeTrack(5);
    private static readonly float[] Target = new float[5];
    private static readonly OwnedValues<VignetteValue> Values = new OwnedValues<VignetteValue> { Released = _ => Retarget(0f) };
    private static RawImage _image;
    private static Texture2D _texture;

    internal readonly struct VignetteValue
    {
        public VignetteValue(Color color, float strength)
        {
            Color = color;
            Strength = strength;
        }

        public Color Color { get; }

        public float Strength { get; }
    }

    public static bool Any => !Values.IsEmpty;

    public static void Set(ObenMod owner, Color color, float strength, float fadeSeconds)
    {
        Values.Set(owner, owner.Info.Id, "", new VignetteValue(color, strength));
        Retarget(fadeSeconds);
    }

    public static bool Clear(ObenMod owner, float fadeSeconds)
    {
        if (!Values.Remove(owner, ""))
            return false;
        Retarget(fadeSeconds);
        return true;
    }

    public static bool Step(float seconds)
    {
        bool running = Track.Step(seconds);
        Show();
        return running;
    }

    /// <summary>The framework unloads: the overlay object goes with it, the texture is freed here.</summary>
    public static void Unload()
    {
        Values.Clear();
        if (_texture != null)
            Object.Destroy(_texture);
        _texture = null;
        _image = null;
    }

    private static void Retarget(float fadeSeconds)
    {
        if (Values.TryGet("", out var top))
        {
            Target[0] = top.Color.r;
            Target[1] = top.Color.g;
            Target[2] = top.Color.b;
            Target[3] = top.Color.a;
            Target[4] = top.Strength;
            // Fading in from nothing keeps the colour and only raises the strength.
            if (Track.Current[4] <= 0f)
            {
                var start = new float[] { top.Color.r, top.Color.g, top.Color.b, top.Color.a, 0f };
                Track.FadeTo(start, 0f);
            }
        }
        else
        {
            for (int i = 0; i < 4; i++)
                Target[i] = Track.Current[i];
            Target[4] = 0f;
        }
        Track.FadeTo(Target, fadeSeconds);
        Show();
        if (Track.Running)
            FadeTicker.Wake();
    }

    private static void Show()
    {
        var c = Track.Current;
        float strength = Mathf.Clamp01(c[4]);
        if (strength <= 0f)
        {
            if (_image != null)
                _image.enabled = false;
            return;
        }
        var image = Image();
        if (image == null)
            return;
        float reach = Mathf.Lerp(MinReach, MaxReach, strength);
        float low = 0.5f - reach * 0.5f;
        image.uvRect = new Rect(low, low, reach, reach);
        image.color = new Color(c[0], c[1], c[2], c[3] * strength);
        image.enabled = true;
    }

    private static RawImage Image()
    {
        if (_image != null)
            return _image;
        var framework = FrameworkMod.Instance;
        if (framework == null)
            return null;

        var root = framework.CreateObject("Vignette");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;

        var go = new GameObject("Overlay", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(root.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _image = go.GetComponent<RawImage>();
        _image.raycastTarget = false;
        _image.texture = Texture();
        _image.enabled = false;
        return _image;
    }

    // White with alpha rising towards the corners; the image colour tints it.
    private static Texture2D Texture()
    {
        if (_texture != null)
            return _texture;
        var pixels = new Color32[TextureSize * TextureSize];
        float corner = Mathf.Sqrt(2f);
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float px = (x + 0.5f) / TextureSize * 2f - 1f;
                float py = (y + 0.5f) / TextureSize * 2f - 1f;
                float distance = Mathf.Sqrt(px * px + py * py) / corner;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, distance)) * 255f);
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
            }
        }
        _texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "ObenFramework vignette",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        _texture.SetPixels32(pixels);
        _texture.Apply(false, true);
        return _texture;
    }
}
