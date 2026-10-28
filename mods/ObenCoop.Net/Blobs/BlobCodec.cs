using System;
using System.IO;
using System.IO.Compression;

namespace ObenCoop.Net.Blobs;

/// <summary>
/// Deflate and CRC-32 for blobs. Both are plain CPU work on arrays and are run on a worker thread
/// by <see cref="BlobTransfer"/>, so nothing here touches session state.
/// </summary>
internal static class BlobCodec
{
    private static readonly uint[] Table = BuildTable();

    /// <summary>CRC-32 (the zip/PNG polynomial) of a whole buffer region.</summary>
    public static uint Crc32(byte[] data, int offset, int count)
    {
        uint crc = 0xFFFFFFFFu;
        for (int i = offset; i < offset + count; i++)
            crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>
    /// Compresses the data. Returns false, with the data itself as <paramref name="wire"/>, when
    /// compressing does not make it smaller (already packed data, tiny payloads).
    /// </summary>
    public static bool TryCompress(byte[] data, CompressionLevel level, out byte[] wire, out int wireLength)
    {
        if (data.Length >= 64)
        {
            using (var output = new MemoryStream(data.Length / 2 + 64))
            {
                using (var deflate = new DeflateStream(output, level, true))
                    deflate.Write(data, 0, data.Length);
                if (output.Length < data.Length)
                {
                    wire = output.GetBuffer();
                    wireLength = (int)output.Length;
                    return true;
                }
            }
        }
        wire = data;
        wireLength = data.Length;
        return false;
    }

    /// <summary>
    /// Inflates into <paramref name="output"/>, which must be exactly the size the data should have.
    /// The size is fixed up front, so a damaged or hostile stream can't grow past it.
    /// </summary>
    /// <exception cref="InvalidDataException">The stream is damaged, or shorter or longer than declared.</exception>
    public static void Inflate(byte[] wire, int wireLength, byte[] output)
    {
        using (var input = new MemoryStream(wire, 0, wireLength, false))
        using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
        {
            int filled = 0;
            while (filled < output.Length)
            {
                int read = deflate.Read(output, filled, output.Length - filled);
                if (read <= 0)
                    throw new InvalidDataException($"The data ended after {filled} of {output.Length} bytes.");
                filled += read;
            }
            if (deflate.ReadByte() >= 0)
                throw new InvalidDataException($"The data is longer than the declared {output.Length} bytes.");
        }
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
