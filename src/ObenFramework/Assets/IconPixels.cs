using System;

namespace ObenFramework.Assets;

/// <summary>
/// Turns two pictures of a model, one drawn over black and one over white, into an icon with a clear
/// background. Whatever a shader writes to alpha, the difference between the two backgrounds says how much
/// of each pixel the model covers, and the picture over black divided by that is the model's own colour.
/// Plain math on linear RGBA floats, tested on its own.
/// </summary>
internal static class IconPixels
{
    /// <summary>
    /// Combines the two pictures (linear RGBA, 4 floats a pixel, bottom row first, <paramref name="scale"/>
    /// times the icon's side) into the icon's sRGB RGBA bytes, averaging each <paramref name="scale"/>
    /// square of samples into one pixel.
    /// </summary>
    public static byte[] Combine(float[] overBlack, float[] overWhite, int size, int scale)
    {
        int side = size * scale;
        if (overBlack == null || overWhite == null || overBlack.Length < side * side * 4 || overWhite.Length < side * side * 4)
            throw new ArgumentException("Both pictures must be " + side + " pixels a side.");

        var result = new byte[size * size * 4];
        float samples = scale * scale;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = 0f, g = 0f, b = 0f, wr = 0f, wg = 0f, wb = 0f;
                for (int sy = 0; sy < scale; sy++)
                {
                    int row = (y * scale + sy) * side;
                    for (int sx = 0; sx < scale; sx++)
                    {
                        int i = (row + x * scale + sx) * 4;
                        r += overBlack[i];
                        g += overBlack[i + 1];
                        b += overBlack[i + 2];
                        wr += overWhite[i];
                        wg += overWhite[i + 1];
                        wb += overWhite[i + 2];
                    }
                }
                r /= samples; g /= samples; b /= samples;
                wr /= samples; wg /= samples; wb /= samples;

                // Over white each pixel gains (1 - coverage) on every channel.
                float alpha = Clamp01(1f - ((wr - r) + (wg - g) + (wb - b)) / 3f);
                int o = (y * size + x) * 4;
                if (alpha < 0.5f / 255f)
                    continue;
                result[o] = ToByte(ToSrgb(Clamp01(r / alpha)));
                result[o + 1] = ToByte(ToSrgb(Clamp01(g / alpha)));
                result[o + 2] = ToByte(ToSrgb(Clamp01(b / alpha)));
                result[o + 3] = ToByte(alpha);
            }
        }
        return result;
    }

    /// <summary>A linear colour value as sRGB (what an 8-bit texture holds).</summary>
    public static float ToSrgb(float linear) =>
        linear <= 0.0031308f ? linear * 12.92f : 1.055f * (float)Math.Pow(linear, 1.0 / 2.4) - 0.055f;

    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

    private static byte ToByte(float value) => (byte)Math.Round(value * 255f);
}
