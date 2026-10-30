using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Assets;
using ObenFramework.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DevBridge.Commands;

/// <summary>Drawing icons from models, to look at them or try them on an item (owned by DevBridge).</summary>
internal static class IconCommands
{
    private static readonly Dictionary<int, IDisposable> Applied = new Dictionary<int, IDisposable>();

    [BridgeCommand("items.drawicon", "Draw an icon from an item's pickup model or a loaded model, save it as a PNG and/or use it as an item's icon.",
        Params = new[] { "item?: draw this item's model, and the item whose icon apply/clear change", "model?: a loaded model's full name (modId/key) instead",
            "size?: pixels, default 256", "yaw?: default 35", "pitch?: default 25", "margin?: default 0.06",
            "file?: write the icon to this PNG (absolute path)", "apply?: use it as the item's icon until clear or DevBridge reloads",
            "clear?: put the item's icon back" })]
    private static object DrawIcon(JObject p)
    {
        int itemId = p.Int("item", -1);
        if (p.Bool("clear", false))
        {
            bool had = Applied.TryGetValue(itemId, out var old);
            old?.Dispose();
            Applied.Remove(itemId);
            return new JObject { ["item"] = itemId, ["cleared"] = had };
        }

        var options = new IconOptions
        {
            Size = p.Int("size", 256),
            Yaw = p.Float("yaw", 35f),
            Pitch = p.Float("pitch", 25f),
            Margin = p.Float("margin", 0.06f),
        };
        string modelName = p.Str("model", null);
        GameObject source;
        if (modelName != null)
        {
            var model = ModelRegistry.Find(modelName) ?? throw new ArgumentException($"No loaded model '{modelName}'.");
            source = model.Prefab;
        }
        else
        {
            var item = ItemLookup.ById(itemId) ?? throw new ArgumentException("Give item=<id> or model=<modId/key>.");
            source = item.Appearance != null ? item.Prefab : null;
            if (source == null)
                throw new ArgumentException($"Item {itemId} ({item.Title}) has no pickup model.");
        }

        var timer = Stopwatch.StartNew();
        var sprite = ModelIcons.Render(source, options) ?? throw new InvalidOperationException($"Nothing was drawn of '{source.name}' (see the log).");
        double ms = timer.Elapsed.TotalMilliseconds;
        var result = new JObject { ["source"] = source.name, ["size"] = sprite.texture.width, ["ms"] = Math.Round(ms, 1) };

        string file = p.Str("file", null);
        if (file != null)
        {
            var (png, covered) = Png(sprite.texture);
            File.WriteAllBytes(file, png);
            result["file"] = file;
            result["coveredPercent"] = Math.Round(covered * 100.0, 1);
        }

        if (p.Bool("apply", false) && itemId >= 0)
        {
            if (Applied.TryGetValue(itemId, out var old))
                old.Dispose();
            var icon = ItemIcons.Set(DevBridgeMod.Instance, itemId, sprite);
            Applied[itemId] = ObenLoader.Runtime.Disposable.Create(() =>
            {
                icon.Dispose();
                ModelIcons.Release(sprite);
            });
            result["applied"] = itemId;
        }
        else
        {
            ModelIcons.Release(sprite);
        }
        return result;
    }

    // The icon's texture isn't readable; copy it through a render texture. Also counts how much of it isn't clear.
    private static (byte[] png, double covered) Png(Texture2D texture)
    {
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            copy.Apply();
            int covered = 0;
            var pixels = copy.GetPixels32();
            foreach (var pixel in pixels)
            {
                if (pixel.a > 0)
                    covered++;
            }
            return (copy.EncodeToPNG(), covered / (double)pixels.Length);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(copy);
        }
    }
}
