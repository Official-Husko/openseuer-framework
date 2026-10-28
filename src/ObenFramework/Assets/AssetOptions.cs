using System.Globalization;
using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>How an image is decoded into a texture. The defaults suit UI art; models use their own set.</summary>
public sealed class TextureOptions
{
    /// <summary>Longest side accepted, in pixels. Bigger images are refused before they are decoded.</summary>
    public int MaxSide = 4096;

    /// <summary>Build mip maps. Turn off for pixel-exact UI art.</summary>
    public bool MipMaps = true;

    /// <summary>Treat the pixels as data instead of colour (normal maps, masks).</summary>
    public bool Linear;

    /// <summary>Keep a CPU copy so <c>GetPixels</c> works. Costs memory.</summary>
    public bool Readable;

    public FilterMode Filter = FilterMode.Bilinear;
    public TextureWrapMode Wrap = TextureWrapMode.Clamp;

    internal string Key =>
        string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}.{4}.{5}", MaxSide, MipMaps ? 1 : 0, Linear ? 1 : 0, Readable ? 1 : 0, (int)Filter, (int)Wrap);

    internal static TextureOptions Default => new TextureOptions();
}

/// <summary>How an image becomes a sprite.</summary>
public sealed class SpriteOptions
{
    /// <summary>
    /// Nine-slice border in pixels as (left, bottom, right, top). Use it with an Image set to Sliced so
    /// the corners keep their size while the middle stretches.
    /// </summary>
    public Vector4 Border;

    /// <summary>Pixels that make one world unit; also scales UI sprites at "Pixels Per Unit Multiplier" 1.</summary>
    public float PixelsPerUnit = 100f;

    /// <summary>Pivot as a fraction of the image, (0.5, 0.5) is the centre.</summary>
    public Vector2 Pivot = new Vector2(0.5f, 0.5f);

    /// <summary>Longest side accepted, in pixels.</summary>
    public int MaxSide = 4096;

    /// <summary>Point filtering instead of smoothing, for pixel art.</summary>
    public bool Pixelated;

    internal string Key =>
        string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}.{4},{5}.{6}.{7}", Border.x, Border.y, Border.z, Border.w, PixelsPerUnit, Pivot.x, Pivot.y, Pixelated ? 1 : 0);

    internal TextureOptions ForTexture() => new TextureOptions
    {
        MaxSide = MaxSide,
        MipMaps = false,
        Filter = Pixelated ? FilterMode.Point : FilterMode.Bilinear,
        Wrap = TextureWrapMode.Clamp,
    };
}
