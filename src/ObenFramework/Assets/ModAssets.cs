using System;
using System.IO;
using ObenLoader;
using UnityEngine;
using USprite = UnityEngine.Sprite;

namespace ObenFramework.Assets;

/// <summary>
/// Loads images, sounds and models from a mod's folder. Everything is cached per mod and destroyed when
/// the mod unloads, so a mod never has to clean up after itself. Relative paths are inside the mod
/// folder (and may not leave it); absolute paths are used as given.
/// </summary>
/// <remarks>
/// Sounds are in <c>ModAssets.Audio.cs</c> and models in <see cref="ModelRegistry"/>. Playing a clip
/// through the game's mixer is <see cref="ModSounds"/>.
/// </remarks>
public static partial class ModAssets
{
    private const long MaxImageBytes = 32L * 1024 * 1024;

    /// <summary>
    /// Turns a path into a full one. Relative paths are taken from the mod folder and throw when they
    /// climb out of it. The file doesn't have to exist.
    /// </summary>
    public static string Resolve(ObenMod owner, string path)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path must not be empty", nameof(path));
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        string root = Path.GetFullPath(owner.Directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"'{path}' leaves the mod folder.", nameof(path));
        return full;
    }

    internal static string ResolveExisting(ObenMod owner, string path)
    {
        string full = Resolve(owner, path);
        if (!File.Exists(full))
            throw new FileNotFoundException($"No file at {full}", full);
        return full;
    }

    /// <summary>
    /// Loads a PNG or JPG as a texture. Asking for the same file with the same options again returns
    /// the cached texture. Throws when the file is missing, isn't an image or is bigger than
    /// <see cref="TextureOptions.MaxSide"/>.
    /// </summary>
    public static Texture2D Texture(ObenMod owner, string path, TextureOptions options = null)
    {
        string full = ResolveExisting(owner, path);
        return LoadTexture(AssetSets.For(owner), full, options ?? TextureOptions.Default);
    }

    /// <summary>
    /// Loads a PNG or JPG as a sprite, optionally with a nine-slice border. Sprites don't use mip maps and
    /// clamp at the edges so UI art stays sharp. Cached like <see cref="Texture"/>.
    /// </summary>
    public static USprite Sprite(ObenMod owner, string path, SpriteOptions options = null)
    {
        options = options ?? new SpriteOptions();
        string full = ResolveExisting(owner, path);
        var set = AssetSets.For(owner);

        string key = full + "|" + options.Key;
        if (set.Sprites.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var texture = LoadTexture(set, full, options.ForTexture());
        var sprite = USprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            options.Pivot,
            options.PixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            options.Border);
        sprite.name = texture.name;
        set.Sprites[key] = sprite;
        return sprite;
    }

    /// <summary>Shorthand for a sprite with a nine-slice border of the same size on every side.</summary>
    public static USprite Sprite(ObenMod owner, string path, float border) =>
        Sprite(owner, path, new SpriteOptions { Border = new Vector4(border, border, border, border) });

    internal static Texture2D LoadTexture(ModAssetSet set, string full, TextureOptions options)
    {
        string key = full + "|" + options.Key;
        if (set.Textures.TryGetValue(key, out var cached) && cached != null)
            return cached;

        byte[] bytes = ReadImage(full, options.MaxSide);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, options.MipMaps, options.Linear)
        {
            name = Path.GetFileNameWithoutExtension(full),
        };
        if (!EngineCalls.LoadImage(texture, bytes, !options.Readable))
        {
            UnityEngine.Object.Destroy(texture);
            throw new InvalidDataException($"{full} could not be decoded as an image.");
        }
        texture.filterMode = options.Filter;
        texture.wrapMode = options.Wrap;
        set.Textures[key] = texture;
        return texture;
    }

    // Looks at the header first so an enormous image is refused before the decoder allocates for it.
    private static byte[] ReadImage(string full, int maxSide)
    {
        if (new FileInfo(full).Length > MaxImageBytes)
            throw new InvalidDataException($"{full} is larger than {MaxImageBytes >> 20} MB.");
        byte[] bytes = File.ReadAllBytes(full);
        if (!ImageHeader.TryGetSize(bytes, out int width, out int height))
            throw new InvalidDataException($"{full} isn't a PNG or JPG.");
        if (width > maxSide || height > maxSide)
            throw new InvalidDataException($"{full} is {width}x{height}; the limit is {maxSide} pixels a side.");
        return bytes;
    }
}
