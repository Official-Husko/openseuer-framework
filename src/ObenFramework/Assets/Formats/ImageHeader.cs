// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Assets;

/// <summary>
/// Reads the pixel size of a PNG or JPEG from its header, so a huge image can be refused before the
/// decoder allocates it.
/// </summary>
internal static class ImageHeader
{
    /// <summary>
    /// Gets the width and height of PNG or JPEG bytes. Returns false for anything else, or when a JPEG
    /// has no frame header.
    /// </summary>
    public static bool TryGetSize(byte[] bytes, out int width, out int height)
    {
        width = height = 0;
        if (bytes == null)
            return false;
        if (IsPng(bytes))
        {
            if (bytes.Length < 24)
                return false;
            width = Be32(bytes, 16);
            height = Be32(bytes, 20);
            return width > 0 && height > 0;
        }
        return IsJpeg(bytes) && JpegSize(bytes, out width, out height);
    }

    private static bool IsPng(byte[] b) =>
        b.Length >= 4 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47;

    private static bool IsJpeg(byte[] b) => b.Length >= 2 && b[0] == 0xFF && b[1] == 0xD8;

    private static int Be32(byte[] b, int at)
    {
        long v = ((long)b[at] << 24) | ((long)b[at + 1] << 16) | ((long)b[at + 2] << 8) | b[at + 3];
        return v > int.MaxValue ? int.MaxValue : (int)v;
    }

    private static bool JpegSize(byte[] bytes, out int width, out int height)
    {
        width = height = 0;
        int at = 2;
        while (at + 1 < bytes.Length)
        {
            if (bytes[at] != 0xFF || bytes[at + 1] == 0x00 || bytes[at + 1] == 0xFF)
            {
                at++;
                continue;
            }
            int marker = bytes[at + 1];
            if (marker == 0xD9)
                return false;
            // Start-of-frame markers, except DHT (C4), JPG (C8) and DAC (CC).
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                if (at + 9 > bytes.Length)
                    return false;
                height = bytes[at + 5] << 8 | bytes[at + 6];
                width = bytes[at + 7] << 8 | bytes[at + 8];
                return width > 0 && height > 0;
            }
            if (marker == 0x01 || marker == 0xD8 || (marker >= 0xD0 && marker <= 0xD7))
                at += 2;
            else if (at + 4 > bytes.Length)
                return false;
            else
                at += 2 + (bytes[at + 2] << 8 | bytes[at + 3]);
        }
        return false;
    }
}
