using ObenFramework.Assets;

namespace ObenCore.Tests;

public class IconPixelsTests
{
    // One linear RGBA pixel of a model with this colour and coverage, drawn over a background.
    private static float[] Over(float background, float r, float g, float b, float coverage) => new[]
    {
        r * coverage + background * (1f - coverage),
        g * coverage + background * (1f - coverage),
        b * coverage + background * (1f - coverage),
        1f,
    };

    private static byte[] One(float r, float g, float b, float coverage) =>
        IconPixels.Combine(Over(0f, r, g, b, coverage), Over(1f, r, g, b, coverage), 1, 1);

    [Fact]
    public void EmptyBackgroundIsClear() => Assert.Equal(new byte[] { 0, 0, 0, 0 }, One(0.5f, 0.5f, 0.5f, 0f));

    [Fact]
    public void SolidPixelKeepsItsColourInSrgb()
    {
        var pixel = One(1f, 0.2140f, 0f, 1f);
        Assert.Equal(255, pixel[0]);
        Assert.InRange(pixel[1], 126, 128);
        Assert.Equal(0, pixel[2]);
        Assert.Equal(255, pixel[3]);
    }

    [Fact]
    public void EdgePixelGetsPartAlphaAndTheModelsColour()
    {
        var pixel = One(1f, 1f, 1f, 0.5f);
        Assert.Equal(new byte[] { 255, 255, 255, 128 }, pixel);
    }

    [Fact]
    public void ShaderAlphaDoesNotMatter()
    {
        var black = Over(0f, 0.5f, 0.5f, 0.5f, 1f);
        var white = Over(1f, 0.5f, 0.5f, 0.5f, 1f);
        black[3] = 0f;
        white[3] = 0f;
        Assert.Equal(255, IconPixels.Combine(black, white, 1, 1)[3]);
    }

    [Fact]
    public void AveragesEachSquareOfSamples()
    {
        // Two by two samples, half of them covered by white: one pixel at half coverage.
        var black = new float[16];
        var white = new float[16];
        for (int i = 0; i < 4; i++)
        {
            bool covered = i % 2 == 0;
            Array.Copy(Over(0f, 1f, 1f, 1f, covered ? 1f : 0f), 0, black, i * 4, 4);
            Array.Copy(Over(1f, 1f, 1f, 1f, covered ? 1f : 0f), 0, white, i * 4, 4);
        }
        Assert.Equal(new byte[] { 255, 255, 255, 128 }, IconPixels.Combine(black, white, 1, 2));
    }

    [Fact]
    public void RefusesPicturesOfTheWrongSize() =>
        Assert.Throws<ArgumentException>(() => IconPixels.Combine(new float[4], new float[4], 2, 1));

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(0.2140f, 0.5f)]
    public void ConvertsToSrgb(float linear, float srgb) => Assert.Equal(srgb, IconPixels.ToSrgb(linear), 3);
}
