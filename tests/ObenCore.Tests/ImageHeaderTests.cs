using ObenFramework.Assets;

namespace ObenCore.Tests;

public class ImageHeaderTests
{
    [Fact]
    public void Reads_png_size()
    {
        byte[] png = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(png, 0);
        new byte[] { 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(png, 8);
        new byte[] { 0, 0, 0x04, 0x00, 0, 0, 0x02, 0x00 }.CopyTo(png, 16);

        Assert.True(ImageHeader.TryGetSize(png, out int w, out int h));
        Assert.Equal(1024, w);
        Assert.Equal(512, h);
    }

    [Fact]
    public void A_huge_png_size_does_not_overflow()
    {
        byte[] png = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47 }.CopyTo(png, 0);
        new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }.CopyTo(png, 16);

        Assert.True(ImageHeader.TryGetSize(png, out int w, out int h));
        Assert.Equal(int.MaxValue, w);
        Assert.Equal(int.MaxValue, h);
    }

    [Fact]
    public void Reads_jpeg_size_after_other_segments()
    {
        byte[] jpeg =
        {
            0xFF, 0xD8,
            0xFF, 0xE0, 0x00, 0x04, 0x01, 0x02,              // APP0 with two bytes of payload
            0xFF, 0xC4, 0x00, 0x03, 0x00,                    // DHT: not a frame header
            0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x01, 0x90, 0x03, 0x20, 0x01, 0x01, 0x11, 0x00, // SOF0: 800 x 400
        };

        Assert.True(ImageHeader.TryGetSize(jpeg, out int w, out int h));
        Assert.Equal(800, w);
        Assert.Equal(400, h);
    }

    [Fact]
    public void Refuses_everything_else()
    {
        Assert.False(ImageHeader.TryGetSize(null!, out _, out _));
        Assert.False(ImageHeader.TryGetSize(new byte[0], out _, out _));
        Assert.False(ImageHeader.TryGetSize(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, out _, out _)); // too short
        Assert.False(ImageHeader.TryGetSize(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }, out _, out _)); // ends before a frame
        Assert.False(ImageHeader.TryGetSize(System.Text.Encoding.ASCII.GetBytes("GIF89a......"), out _, out _));
        Assert.False(ImageHeader.TryGetSize(new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0x00 }, out _, out _)); // cut short
    }
}
