#nullable disable
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ObenSave.Format;

/// <summary>
/// The ObenSave container: a 20-byte header followed by a save file's text, normally Deflate
/// compressed. The text is exactly what the game wrote (minified FullSerializer JSON for .tnmt files,
/// a Lua chunk for Globals.dialogue), so decoding gives back the original file byte for byte. A file
/// without the magic is a vanilla file and is read the way the game reads it. See docs/modding/save-format.md.
///
/// Header, little-endian:
///   0  8 bytes  ASCII "OBENSAVE"
///   8  1 byte   format version (1)
///   9  1 byte   codec: 0 = stored, 1 = raw Deflate
///  10  1 byte   flags: bit 0 = the original file started with a UTF-8 BOM
///  11  1 byte   reserved, 0
///  12  4 bytes  length of the text in bytes (uncompressed, BOM not counted)
///  16  4 bytes  CRC-32 of the text bytes
///  20  ...      payload
///
/// No Unity types are used here, so the mod, the oben tool and the unit tests compile the same file.
/// </summary>
public static class SaveContainer
{
    public const byte FormatVersion = 1;
    public const int HeaderSize = 20;
    public const int MagicSize = 8;

    /// <summary>Largest text a container may claim to hold. Real saves are a few megabytes.</summary>
    public const int MaxContentLength = 1 << 30;

    private const byte BomFlag = 1;

    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OBENSAVE");
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>True when the bytes start with the container magic.</summary>
    public static bool IsContainer(byte[] data) => data != null && StartsWithMagic(data, data.Length);

    /// <summary>True when the first <paramref name="count"/> bytes of a buffer are the container magic.</summary>
    public static bool StartsWithMagic(byte[] head, int count)
    {
        if (head == null || count < MagicSize)
            return false;
        for (int i = 0; i < MagicSize; i++)
        {
            if (head[i] != Magic[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Wraps the bytes of a vanilla save file in a container. The bytes must be UTF-8, with or without a
    /// byte order mark; anything else (UTF-16, say) throws <see cref="InvalidDataException"/> so the caller
    /// leaves the file alone. A file that is already a container is returned unchanged.
    /// </summary>
    public static byte[] Encode(byte[] fileBytes)
    {
        if (fileBytes == null)
            throw new ArgumentNullException(nameof(fileBytes));
        if (IsContainer(fileBytes))
            return fileBytes;
        if (StartsWith(fileBytes, 0xFF, 0xFE) || StartsWith(fileBytes, 0xFE, 0xFF))
            throw new InvalidDataException("The file is UTF-16 text, not the UTF-8 the game writes.");

        bool bom = StartsWith(fileBytes, 0xEF, 0xBB, 0xBF);
        int offset = bom ? Utf8Bom.Length : 0;
        return Build(fileBytes, offset, fileBytes.Length - offset, bom);
    }

    /// <summary>Wraps text (for example hand-edited JSON) in a container, with the BOM the game's files have.</summary>
    public static byte[] EncodeText(string text, bool bom = true)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        byte[] raw = new UTF8Encoding(false).GetBytes(text);
        return Build(raw, 0, raw.Length, bom);
    }

    /// <summary>
    /// The original file bytes of a container (with the BOM restored), which is what the game would have
    /// written. Bytes that are not a container are returned as they are.
    /// </summary>
    public static byte[] ToVanillaBytes(byte[] data)
    {
        if (!IsContainer(data))
            return data;
        var info = Inspect(data);
        byte[] content = ReadContent(data, info);
        if (!info.HasBom)
            return content;
        var file = new byte[Utf8Bom.Length + content.Length];
        Buffer.BlockCopy(Utf8Bom, 0, file, 0, Utf8Bom.Length);
        Buffer.BlockCopy(content, 0, file, Utf8Bom.Length, content.Length);
        return file;
    }

    /// <summary>
    /// The text of a save file, the same string <c>File.ReadAllText(path, Encoding.UTF8)</c> gives for a
    /// vanilla file (no BOM in the result). Containers are decoded and checked.
    /// </summary>
    public static string ReadText(byte[] data)
    {
        if (!IsContainer(data))
        {
            using (var reader = new StreamReader(new MemoryStream(data), Encoding.UTF8, true))
                return reader.ReadToEnd();
        }
        byte[] content = ReadContent(data, Inspect(data));
        return new UTF8Encoding(false).GetString(content);
    }

    /// <summary>
    /// Reads the header of a container, or describes a vanilla file. Throws
    /// <see cref="InvalidDataException"/> for a container header that is cut short or from a newer version.
    /// </summary>
    public static SaveFileInfo Inspect(byte[] data)
    {
        if (!IsContainer(data))
        {
            bool bom = StartsWith(data, 0xEF, 0xBB, 0xBF);
            return new SaveFileInfo
            {
                HasBom = bom,
                ContentLength = data.Length - (bom ? Utf8Bom.Length : 0),
                FileLength = data.Length,
            };
        }

        if (data.Length < HeaderSize)
            throw new InvalidDataException("The save file is cut short (the header is incomplete).");
        if (data[8] > FormatVersion)
            throw new InvalidDataException($"Save format {data[8]} is newer than this version understands ({FormatVersion}).");
        if (data[9] > (byte)SaveCodec.Deflate)
            throw new InvalidDataException($"Unknown save codec {data[9]}.");

        int length = ReadInt(data, 12);
        if (length < 0 || length > MaxContentLength)
            throw new InvalidDataException("The save file is damaged (impossible length).");

        return new SaveFileInfo
        {
            IsContainer = true,
            Version = data[8],
            Codec = (SaveCodec)data[9],
            HasBom = (data[10] & BomFlag) != 0,
            ContentLength = length,
            FileLength = data.Length,
            Crc = (uint)ReadInt(data, 16),
        };
    }

    private static byte[] Build(byte[] source, int offset, int count, bool bom)
    {
        byte[] payload = Deflate(source, offset, count);
        var codec = SaveCodec.Deflate;
        if (payload.Length >= count)
        {
            // Tiny files (Info.tnmt can be a couple of hundred bytes) don't always shrink.
            codec = SaveCodec.Stored;
            payload = new byte[count];
            Buffer.BlockCopy(source, offset, payload, 0, count);
        }

        var output = new byte[HeaderSize + payload.Length];
        Buffer.BlockCopy(Magic, 0, output, 0, MagicSize);
        output[8] = FormatVersion;
        output[9] = (byte)codec;
        output[10] = bom ? BomFlag : (byte)0;
        WriteInt(output, 12, count);
        WriteInt(output, 16, (int)Crc32.Compute(source, offset, count));
        Buffer.BlockCopy(payload, 0, output, HeaderSize, payload.Length);
        return output;
    }

    private static byte[] ReadContent(byte[] data, SaveFileInfo info)
    {
        byte[] content;
        if (info.Codec == SaveCodec.Stored)
        {
            if (data.Length - HeaderSize != info.ContentLength)
                throw Damaged();
            content = new byte[info.ContentLength];
            Buffer.BlockCopy(data, HeaderSize, content, 0, content.Length);
        }
        else
        {
            content = Inflate(data, HeaderSize, info.ContentLength);
        }

        if (Crc32.Compute(content) != info.Crc)
            throw Damaged();
        return content;
    }

    private static byte[] Deflate(byte[] source, int offset, int count)
    {
        using (var output = new MemoryStream(count / 6 + 64))
        {
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                deflate.Write(source, offset, count);
            return output.ToArray();
        }
    }

    // The output must be exactly the announced length. It is collected in chunks and abandoned as soon as
    // it grows past that, so neither a damaged length field nor a decompression bomb can make a small
    // file take up gigabytes.
    private static byte[] Inflate(byte[] data, int offset, int length)
    {
        try
        {
            using (var input = new MemoryStream(data, offset, data.Length - offset))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var chunk = new byte[64 * 1024];
                long total = 0;
                int n;
                while ((n = deflate.Read(chunk, 0, chunk.Length)) > 0)
                {
                    total += n;
                    if (total > length)
                        throw Damaged();
                    output.Write(chunk, 0, n);
                }
                if (total != length)
                    throw Damaged();
                return output.ToArray();
            }
        }
        catch (InvalidDataException)
        {
            throw Damaged();
        }
    }

    private static InvalidDataException Damaged() =>
        new InvalidDataException("The save file is damaged (length or checksum mismatch).");

    private static bool StartsWith(byte[] data, params byte[] prefix)
    {
        if (data == null || data.Length < prefix.Length)
            return false;
        for (int i = 0; i < prefix.Length; i++)
        {
            if (data[i] != prefix[i])
                return false;
        }
        return true;
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    private static int ReadInt(byte[] buffer, int offset) =>
        buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24;
}
