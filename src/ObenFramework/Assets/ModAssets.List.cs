using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace ObenFramework.Assets;

/// <summary>One asset a mod has loaded, as reported by <see cref="ModAssets.List"/>.</summary>
public sealed class AssetEntry
{
    public string ModId { get; internal set; }

    /// <summary>"texture", "sprite", "sound" or "model".</summary>
    public string Kind { get; internal set; }

    /// <summary>The file name, or the key for a model.</summary>
    public string Name { get; internal set; }

    /// <summary>The full path of the file it came from.</summary>
    public string Source { get; internal set; }

    /// <summary>Size, length and so on in one line.</summary>
    public string Detail { get; internal set; }

    /// <summary>Memory the engine reports for it, in bytes.</summary>
    public long Bytes { get; internal set; }
}

public static partial class ModAssets
{
    /// <summary>
    /// Lists what mods have loaded and still hold: textures, sprites, sounds and models. Pass a mod id
    /// to see only that mod's assets.
    /// </summary>
    public static List<AssetEntry> List(string modId = null)
    {
        var entries = new List<AssetEntry>();
        foreach (var set in AssetSets.All())
        {
            if (modId != null && !string.Equals(set.ModId, modId, System.StringComparison.OrdinalIgnoreCase))
                continue;
            AddTextures(set, entries);
            AddSprites(set, entries);
            AddSounds(set, entries);
            AddModels(set, entries);
        }
        return entries;
    }

    private static void AddTextures(ModAssetSet set, List<AssetEntry> into)
    {
        foreach (var pair in set.Textures)
        {
            var texture = pair.Value;
            if (texture == null)
                continue;
            into.Add(Entry(set, "texture", Source(pair.Key), $"{texture.width}x{texture.height}", texture));
        }
    }

    private static void AddSprites(ModAssetSet set, List<AssetEntry> into)
    {
        foreach (var pair in set.Sprites)
        {
            var sprite = pair.Value;
            if (sprite == null)
                continue;
            var border = sprite.border;
            string slices = border == Vector4.zero ? "" : $", border {border.x}/{border.y}/{border.z}/{border.w}";
            into.Add(Entry(set, "sprite", Source(pair.Key), $"{sprite.rect.width}x{sprite.rect.height}{slices}", null));
        }
    }

    private static void AddSounds(ModAssetSet set, List<AssetEntry> into)
    {
        foreach (var pair in set.Sounds)
        {
            var clip = pair.Value;
            if (clip == null)
                continue;
            into.Add(Entry(set, "sound", pair.Key, $"{clip.length:0.##}s, {clip.channels} ch, {clip.frequency} Hz", clip));
        }
    }

    private static void AddModels(ModAssetSet set, List<AssetEntry> into)
    {
        foreach (var model in set.Models.Values)
        {
            if (!model.IsAlive)
                continue;
            var entry = Entry(set, "model", model.SourcePath, $"{model.VertexCount} vertices, {model.TriangleCount} triangles, {model.Materials.Length} materials", model.Mesh);
            entry.Name = model.Key;
            into.Add(entry);
        }
    }

    private static AssetEntry Entry(ModAssetSet set, string kind, string source, string detail, Object asset) => new AssetEntry
    {
        ModId = set.ModId,
        Kind = kind,
        Name = Path.GetFileName(source),
        Source = source,
        Detail = detail,
        Bytes = asset != null ? Profiler.GetRuntimeMemorySizeLong(asset) : 0,
    };

    // Cache keys are "path|options".
    private static string Source(string key)
    {
        int bar = key.LastIndexOf('|');
        return bar < 0 ? key : key.Substring(0, bar);
    }
}
