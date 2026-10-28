// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ObenFramework.SceneLook;

/// <summary>Reads texture headers, finds their pixel data and checks it can be uploaded as is.</summary>
internal static class SceneLookTextureReader
{
    private const int MaxMips = 16;

    // RGB24, RGBA32, DXT1 and DXT5: the formats the game's character textures use and Unity loads raw.
    private static readonly int[] SupportedFormats = { 3, 4, 10, 12 };

    private static readonly Dictionary<int, string> FormatNames = new Dictionary<int, string>
    {
        { 1, "Alpha8" }, { 2, "ARGB4444" }, { 3, "RGB24" }, { 4, "RGBA32" }, { 5, "ARGB32" }, { 7, "RGB565" },
        { 10, "DXT1" }, { 12, "DXT5" }, { 13, "RGBA4444" }, { 14, "BGRA32" }, { 24, "BC6H" }, { 25, "BC7" },
        { 26, "BC4" }, { 27, "BC5" }, { 28, "DXT1Crunched" }, { 29, "DXT5Crunched" },
    };

    public static string FormatLabel(int format) => FormatNames.TryGetValue(format, out var name) ? name : format.ToString();

    public static bool IsSupported(int format) => Array.IndexOf(SupportedFormats, format) >= 0;

    /// <summary>Bytes of all mip levels of a supported format, or -1 for impossible sizes.</summary>
    public static long ExpectedBytes(int format, int width, int height, int mips)
    {
        bool block = format == 10 || format == 12;
        long unit = format == 3 ? 3 : format == 4 ? 4 : format == 10 ? 8 : 16;
        if (width < 0 || height < 0 || mips < 0 || mips > MaxMips)
            return -1;
        long total = 0;
        for (int i = 0; i < mips; i++)
        {
            long mw = Math.Max(1L, (long)width >> i);
            long mh = Math.Max(1L, (long)height >> i);
            long level = LevelBytes(block, mw, mh, unit);
            if (level < 0 || total > long.MaxValue - level)
                return -1;
            total += level;
        }
        return total;
    }

    public static SceneLookTexture ParseTexture(SceneLookFile f, long pathId, byte[] bytes)
    {
        var c = new SceneLookCursor(bytes, 0);
        var texture = new SceneLookTexture { PathId = pathId, FileName = f.Name, Name = c.Text() };
        c.I32(); // forced fallback format
        texture.Width = c.I32();
        texture.Height = c.I32();
        texture.CompleteSize = c.U32();
        c.I32(); // mips stripped
        texture.Format = c.I32();
        texture.Mips = c.I32();
        if (FindStream(bytes, out string path, out long offset, out long size))
        {
            texture.StreamPath = path;
            texture.StreamOffset = offset;
            texture.StreamSize = size;
        }
        else
        {
            texture.Inline = true;
            texture.InlineValid = InlineOk(bytes, texture.CompleteSize);
        }
        return texture;
    }

    /// <summary>The format of a texture from the first bytes of its object.</summary>
    public static int HeadFormat(SceneLookFile f, long pathId)
    {
        var c = new SceneLookCursor(f.ReadObjectHead(pathId, 512), 0);
        c.Text();
        c.I32();
        c.I32();
        c.I32();
        c.U32();
        c.I32();
        return c.I32();
    }

    /// <summary>Why a texture can't be uploaded raw, empty when it can.</summary>
    public static List<string> TextureReasons(SceneLookFiles files, SceneLookTexture texture)
    {
        var reasons = new List<string>();
        if (!IsSupported(texture.Format))
            reasons.Add("format " + FormatLabel(texture.Format) + " not supported");
        else if (ExpectedBytes(texture.Format, texture.Width, texture.Height, texture.Mips) != texture.CompleteSize)
            reasons.Add("texture size rule failed");
        if (texture.Inline)
        {
            if (!texture.InlineValid)
                reasons.Add("inline texture data not validated");
            return reasons;
        }
        if (!files.Installed(texture.StreamPath, texture.StreamOffset + texture.StreamSize))
            reasons.Add("texture stream not installed");
        else if (texture.StreamSize != texture.CompleteSize)
            reasons.Add("texture stream size mismatch");
        return reasons;
    }

    /// <summary>The raw pixel bytes of a texture, from its stream file or from the end of its object.</summary>
    public static byte[] TexturePixels(SceneLookFiles files, SceneLookTexture texture)
    {
        if (!texture.Inline)
            return files.ReadStream(texture.StreamPath, texture.StreamOffset, texture.StreamSize);
        var owner = files.Get(texture.FileName, out string reason) ?? throw new InvalidDataException(reason);
        byte[] bytes = owner.ReadObject(texture.PathId);
        int size = (int)texture.CompleteSize;
        if (bytes.Length - 16 - size < 4)
            throw new InvalidDataException("inline texture " + texture.Name + " is too short");
        var pixels = new byte[size];
        Buffer.BlockCopy(bytes, bytes.Length - 16 - size, pixels, 0, size);
        return pixels;
    }

    private static long LevelBytes(bool block, long width, long height, long unit)
    {
        if (block)
        {
            long wide = (width + 3) / 4;
            long high = (height + 3) / 4;
            if (wide != 0 && high > long.MaxValue / wide / unit)
                return -1;
            return wide * high * unit;
        }
        if (width != 0 && height > long.MaxValue / width / unit)
            return -1;
        return width * height * unit;
    }

    // The stream info sits at the end of the object: offset (8), size (4), then the length-prefixed path.
    private static bool FindStream(byte[] bytes, out string path, out long offset, out long size)
    {
        path = null;
        offset = 0;
        size = 0;
        foreach (string extension in new[] { ".resS", ".resource" })
        {
            byte[] ext = Encoding.ASCII.GetBytes(extension);
            int e = LastIndexOf(bytes, ext);
            if (e < 0)
                continue;
            int end = e + ext.Length;
            for (int n = ext.Length + 1; n <= 200; n++)
            {
                int s = end - n;
                if (s < 16)
                    break;
                if (BitConverter.ToInt32(bytes, s - 4) != n || !IsPrintable(bytes, s, end))
                    continue;
                path = Encoding.ASCII.GetString(bytes, s, end - s);
                offset = BitConverter.ToInt64(bytes, s - 16);
                size = BitConverter.ToUInt32(bytes, s - 8);
                return true;
            }
        }
        return false;
    }

    // Inline pixels end 16 bytes before the object does and are preceded by their own length.
    private static bool InlineOk(byte[] bytes, long complete)
    {
        if (bytes.Length < complete + 20)
            return false;
        int at = (int)(bytes.Length - 16 - complete - 4);
        return BitConverter.ToUInt32(bytes, at) == complete;
    }

    private static bool IsPrintable(byte[] bytes, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (bytes[i] < 0x20 || bytes[i] > 0x7E)
                return false;
        }
        return true;
    }

    private static int LastIndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = haystack.Length - needle.Length; i >= 0; i--)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j])
                j++;
            if (j == needle.Length)
                return i;
        }
        return -1;
    }
}
