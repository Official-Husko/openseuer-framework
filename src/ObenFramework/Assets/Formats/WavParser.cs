// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Assets;

/// <summary>Decoded WAV audio: interleaved samples in the range -1 to 1.</summary>
internal sealed class WavData
{
    public int Channels;
    public int SampleRate;
    public float[] Samples;

    /// <summary>Samples per channel.</summary>
    public int Frames => Channels == 0 ? 0 : Samples.Length / Channels;

    public float Seconds => SampleRate == 0 ? 0f : (float)Frames / SampleRate;
}

/// <summary>
/// Decodes RIFF/WAVE bytes: integer PCM at 8, 16, 24 and 32 bits and IEEE float at 32 and 64 bits,
/// mono up to 8 channels, including the extensible header. No Unity types, so it can run on any thread.
/// </summary>
internal static class WavParser
{
    /// <summary>Largest file read by the built-in decoder; longer audio goes through Unity's loader.</summary>
    public const long MaxBytes = 32L * 1024 * 1024;

    private const int FormatPcm = 1;
    private const int FormatFloat = 3;
    private const int FormatExtensible = 0xFFFE;
    private const int MaxChannels = 8;

    public static bool TryParse(byte[] d, out WavData wav, out string error)
    {
        wav = null;
        if (d == null || d.Length < 12 || !Tag(d, 0, "RIFF") || !Tag(d, 8, "WAVE"))
        {
            error = "not a RIFF/WAVE file";
            return false;
        }

        int format = 0, channels = 0, rate = 0, bits = 0, dataStart = -1, dataLength = 0;
        int pos = 12;
        while (pos + 8 <= d.Length)
        {
            int size = Int32(d, pos + 4);
            int body = pos + 8;
            if (Tag(d, pos, "data"))
            {
                dataStart = body;
                // Streamed files leave the size as 0xFFFFFFFF; truncated ones are cut short.
                dataLength = size < 0 || size > d.Length - body ? d.Length - body : size;
                break;
            }
            if (size < 0 || size > d.Length - body)
            {
                error = "a chunk runs past the end of the file";
                return false;
            }
            if (Tag(d, pos, "fmt ") && size >= 16)
            {
                format = UInt16(d, body);
                channels = UInt16(d, body + 2);
                rate = Int32(d, body + 4);
                bits = UInt16(d, body + 14);
                if (format == FormatExtensible && size >= 26)
                    format = UInt16(d, body + 24);
            }
            pos = body + size + (size & 1);
        }

        if (dataStart < 0)
        {
            error = "no data chunk";
            return false;
        }
        bool pcm = format == FormatPcm && (bits == 8 || bits == 16 || bits == 24 || bits == 32);
        bool floating = format == FormatFloat && (bits == 32 || bits == 64);
        if (!pcm && !floating)
        {
            error = "unsupported format (tag " + format + ", " + bits + " bits); use PCM 8/16/24/32 or float 32/64";
            return false;
        }
        if (channels < 1 || channels > MaxChannels || rate < 1000 || rate > 384000)
        {
            error = "unsupported channel count or sample rate (" + channels + " channels, " + rate + " Hz)";
            return false;
        }

        int width = bits / 8;
        int count = dataLength / (width * channels) * channels;
        if (count < 1)
        {
            error = "no samples";
            return false;
        }

        var samples = new float[count];
        for (int i = 0; i < count; i++)
            samples[i] = Sample(d, dataStart + i * width, width, floating);

        wav = new WavData { Channels = channels, SampleRate = rate, Samples = samples };
        error = null;
        return true;
    }

    private static bool Tag(byte[] d, int p, string tag)
    {
        for (int i = 0; i < 4; i++)
        {
            if (d[p + i] != tag[i])
                return false;
        }
        return true;
    }

    private static int UInt16(byte[] d, int p) => d[p] | d[p + 1] << 8;

    private static int Int32(byte[] d, int p) => d[p] | d[p + 1] << 8 | d[p + 2] << 16 | d[p + 3] << 24;

    private static float Sample(byte[] d, int p, int width, bool floating)
    {
        if (floating)
        {
            double value = width == 4 ? BitConverter.ToSingle(d, p) : BitConverter.ToDouble(d, p);
            if (double.IsNaN(value))
                return 0f;
            return (float)(value < -1.0 ? -1.0 : value > 1.0 ? 1.0 : value);
        }
        switch (width)
        {
            case 1: return (d[p] - 128) / 128f;
            case 2: return (short)(d[p] | d[p + 1] << 8) / 32768f;
            case 3: return ((d[p] << 8 | d[p + 1] << 16 | d[p + 2] << 24) >> 8) / 8388608f;
            default: return Int32(d, p) / 2147483648f;
        }
    }
}
