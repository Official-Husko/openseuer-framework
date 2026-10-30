using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Assets;

/// <summary>How <see cref="ModelIcons"/> draws a model.</summary>
public sealed class IconOptions
{
    /// <summary>Width and height of the icon in pixels, 16 to 1024. The game's own icons are 256.</summary>
    public int Size = 256;

    /// <summary>Degrees the camera goes round the model; 0 looks at the side facing the model's forward (+Z).</summary>
    public float Yaw = 35f;

    /// <summary>Degrees the camera looks down on the model; negative looks up at it.</summary>
    public float Pitch = 25f;

    /// <summary>Space left empty on each side, as a fraction of the icon, 0 to 0.4.</summary>
    public float Margin = 0.06f;
}

/// <summary>
/// Draws a 3D model into a square sprite with a clear background, for an item's or a piece of furniture's
/// icon. It takes any model: a <see cref="ModelTemplate"/>, a game prefab or an object in the world. Only
/// its meshes are copied for the picture, so none of its scripts run, and the picture looks the same in
/// every level and at any time of day. Drawing takes a few milliseconds; do it once and keep the sprite.
/// </summary>
public static class ModelIcons
{
    public const int MinSize = 16;
    public const int MaxSize = 1024;

    private static int _layer = -1;

    /// <summary>
    /// Draws <paramref name="model"/>'s visible meshes (the most detailed level only). The sprite belongs to
    /// the caller: <see cref="Release"/> it when it is no longer shown. Null when the model has no meshes or
    /// drawing failed (logged).
    /// </summary>
    public static Sprite Render(GameObject model, IconOptions options = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        options ??= new IconOptions();
        int size = Mathf.Clamp(options.Size, MinSize, MaxSize);
        // Four samples a pixel smooth the edges; at the biggest sizes one is plenty.
        int scale = size <= 512 ? 2 : 1;
        try
        {
            using var stage = new IconStage(Layer());
            if (!stage.Copy(model))
            {
                AssetsLog.Warn($"Model '{model.name}' has no meshes to draw an icon from.");
                return null;
            }
            stage.Frame(options.Yaw, options.Pitch, Mathf.Clamp(options.Margin, 0f, 0.4f));
            var (overBlack, overWhite) = stage.Shoot(size * scale);
            return MakeSprite(IconPixels.Combine(overBlack, overWhite, size, scale), size, model.name);
        }
        catch (Exception e)
        {
            AssetsLog.Error($"Couldn't draw an icon of '{model.name}'", e);
            return null;
        }
    }

    /// <summary>Draws a model a mod loaded. Throws when it has been unloaded.</summary>
    public static Sprite Render(ModelTemplate model, IconOptions options = null)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        if (!model.IsAlive || model.Prefab == null)
            throw new ArgumentException($"Model {model.Name} has been unloaded.", nameof(model));
        return Render(model.Prefab, options);
    }

    /// <summary>Destroys a sprite <see cref="Render(GameObject, IconOptions)"/> made, and its texture.</summary>
    public static void Release(Sprite sprite)
    {
        if (sprite == null)
            return;
        var texture = sprite.texture;
        Object.Destroy(sprite);
        if (texture != null)
            Object.Destroy(texture);
    }

    private static Sprite MakeSprite(byte[] rgba, int size, string name)
    {
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Icon of " + name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = texture.name;
        return sprite;
    }

    // The game's own "ItemPreview" layer, which next to nothing in a level uses (and the stage is far above the
    // level anyway). Layer 31 if a game update drops it.
    private static int Layer()
    {
        if (_layer < 0)
        {
            int layer = LayerMask.NameToLayer("ItemPreview");
            _layer = layer >= 0 ? layer : 31;
        }
        return _layer;
    }
}
