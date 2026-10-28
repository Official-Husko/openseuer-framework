#nullable disable
namespace ObenSave.Format;

/// <summary>How a container stores its text.</summary>
public enum SaveCodec : byte
{
    /// <summary>The text as it is.</summary>
    Stored = 0,

    /// <summary>Raw Deflate (RFC 1951), no zlib or gzip wrapper.</summary>
    Deflate = 1,
}

/// <summary>How a save file is stored, as read from its first bytes. Describes container and vanilla files.</summary>
public sealed class SaveFileInfo
{
    /// <summary>True for an ObenSave container, false for a file as the game writes it.</summary>
    public bool IsContainer;

    /// <summary>Container format version; 0 for a vanilla file.</summary>
    public byte Version;

    public SaveCodec Codec;

    /// <summary>True when the file as the game wrote it starts with a UTF-8 byte order mark.</summary>
    public bool HasBom;

    /// <summary>Length of the text in bytes, without the byte order mark.</summary>
    public int ContentLength;

    /// <summary>Size of the whole file in bytes.</summary>
    public long FileLength;

    /// <summary>CRC-32 of the text bytes (containers only).</summary>
    public uint Crc;
}
