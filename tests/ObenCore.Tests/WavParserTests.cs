using ObenFramework.Assets;

namespace ObenCore.Tests;

public class WavParserTests
{
    [Fact]
    public void Reads_16_bit_stereo()
    {
        byte[] data = Samples16(-32768, 0, 16384, 32767);
        byte[] file = Wav(1, 2, 44100, 16, data);

        Assert.True(WavParser.TryParse(file, out var wav, out var error), error);
        Assert.Equal(2, wav.Channels);
        Assert.Equal(44100, wav.SampleRate);
        Assert.Equal(2, wav.Frames);
        Assert.Equal(-1f, wav.Samples[0]);
        Assert.Equal(0f, wav.Samples[1]);
        Assert.Equal(0.5f, wav.Samples[2]);
        Assert.Equal(32767f / 32768f, wav.Samples[3]);
    }

    [Fact]
    public void Reads_8_bit_as_unsigned()
    {
        byte[] file = Wav(1, 1, 8000, 8, new byte[] { 0, 128, 255 });

        Assert.True(WavParser.TryParse(file, out var wav, out _));
        Assert.Equal(-1f, wav.Samples[0]);
        Assert.Equal(0f, wav.Samples[1]);
        Assert.Equal(127f / 128f, wav.Samples[2]);
    }

    [Fact]
    public void Reads_24_bit_with_sign()
    {
        // -8388608 (min), 4194304 (half), -1.
        byte[] data = { 0x00, 0x00, 0x80, 0x00, 0x00, 0x40, 0xFF, 0xFF, 0xFF };
        byte[] file = Wav(1, 1, 48000, 24, data);

        Assert.True(WavParser.TryParse(file, out var wav, out _));
        Assert.Equal(3, wav.Samples.Length);
        Assert.Equal(-1f, wav.Samples[0]);
        Assert.Equal(0.5f, wav.Samples[1]);
        Assert.Equal(-1f / 8388608f, wav.Samples[2]);
    }

    [Fact]
    public void Reads_32_bit_integer()
    {
        byte[] data = BitConverter.GetBytes(int.MinValue).Concat(BitConverter.GetBytes(1 << 30)).ToArray();
        byte[] file = Wav(1, 1, 22050, 32, data);

        Assert.True(WavParser.TryParse(file, out var wav, out _));
        Assert.Equal(-1f, wav.Samples[0]);
        Assert.Equal(0.5f, wav.Samples[1]);
    }

    [Fact]
    public void Reads_float_and_clamps()
    {
        byte[] data = new[] { 0.25f, 2f, float.NaN, -3f }.SelectMany(BitConverter.GetBytes).ToArray();
        byte[] file = Wav(3, 1, 44100, 32, data);

        Assert.True(WavParser.TryParse(file, out var wav, out _));
        Assert.Equal(new[] { 0.25f, 1f, 0f, -1f }, wav.Samples);
    }

    [Fact]
    public void Reads_64_bit_float()
    {
        byte[] data = new[] { 0.5, -0.5 }.SelectMany(BitConverter.GetBytes).ToArray();
        byte[] file = Wav(3, 1, 44100, 64, data);

        Assert.True(WavParser.TryParse(file, out var wav, out _));
        Assert.Equal(new[] { 0.5f, -0.5f }, wav.Samples);
    }

    [Fact]
    public void Reads_the_extensible_header()
    {
        byte[] fmt = new byte[40];
        Put16(fmt, 0, 0xFFFE);
        Put16(fmt, 2, 1);
        Put32(fmt, 4, 44100);
        Put16(fmt, 14, 16);
        Put16(fmt, 16, 22);
        Put16(fmt, 24, 1); // sub format: PCM
        byte[] file = Riff(Chunk("fmt ", fmt), Chunk("data", Samples16(1000, -1000)));

        Assert.True(WavParser.TryParse(file, out var wav, out var error), error);
        Assert.Equal(2, wav.Samples.Length);
        Assert.Equal(1000 / 32768f, wav.Samples[0]);
    }

    [Fact]
    public void Skips_chunks_before_data_including_odd_sized_ones()
    {
        byte[] file = Riff(Fmt(1, 1, 44100, 16), Chunk("LIST", new byte[] { 1, 2, 3 }), Chunk("data", Samples16(100)));

        Assert.True(WavParser.TryParse(file, out var wav, out var error), error);
        Assert.Equal(100 / 32768f, wav.Samples[0]);
    }

    [Fact]
    public void Truncated_data_keeps_whole_frames()
    {
        // Claims 100 bytes but only 6 follow: 1.5 stereo frames of 16 bit, so one frame is kept.
        byte[] data = Samples16(1, 2, 3);
        byte[] fmt = Fmt(1, 2, 44100, 16);
        byte[] header = new byte[8];
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("data"), header, 4);
        Put32(header, 4, 100);
        byte[] file = Riff(fmt, header.Concat(data).ToArray());

        Assert.True(WavParser.TryParse(file, out var wav, out _));
        Assert.Equal(1, wav.Frames);
    }

    [Fact]
    public void Streamed_data_size_reads_to_the_end()
    {
        byte[] header = new byte[8];
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("data"), header, 4);
        Put32(header, 4, unchecked((int)0xFFFFFFFF));
        byte[] file = Riff(Fmt(1, 1, 44100, 16), header.Concat(Samples16(5, 6, 7)).ToArray());

        Assert.True(WavParser.TryParse(file, out var wav, out _));
        Assert.Equal(3, wav.Samples.Length);
    }

    [Fact]
    public void Computes_length_in_seconds()
    {
        byte[] file = Wav(1, 1, 1000, 16, Samples16(new short[500]));

        Assert.True(WavParser.TryParse(file, out var wav, out _));
        Assert.Equal(0.5f, wav.Seconds, 3);
    }

    [Theory]
    [InlineData(2, 16)] // ADPCM-style tag
    [InlineData(1, 12)]
    [InlineData(3, 16)] // float needs 32 or 64
    public void Refuses_unsupported_formats(int tag, int bits)
    {
        byte[] file = Wav(tag, 1, 44100, bits, new byte[16]);

        Assert.False(WavParser.TryParse(file, out var wav, out var error));
        Assert.Null(wav);
        Assert.Contains("unsupported", error);
    }

    [Fact]
    public void Refuses_bad_channel_counts_and_rates()
    {
        Assert.False(WavParser.TryParse(Wav(1, 0, 44100, 16, new byte[8]), out _, out _));
        Assert.False(WavParser.TryParse(Wav(1, 9, 44100, 16, new byte[18]), out _, out _));
        Assert.False(WavParser.TryParse(Wav(1, 1, 0, 16, new byte[8]), out _, out _));
    }

    [Fact]
    public void Refuses_garbage_and_missing_parts()
    {
        Assert.False(WavParser.TryParse(null, out _, out _));
        Assert.False(WavParser.TryParse(new byte[4], out _, out _));
        Assert.False(WavParser.TryParse(System.Text.Encoding.ASCII.GetBytes("RIFF....WAVEfmt "), out _, out _));

        // No data chunk at all.
        Assert.False(WavParser.TryParse(Riff(Fmt(1, 1, 44100, 16)), out _, out var error));
        Assert.Contains("data", error);

        // Data chunk present but empty.
        Assert.False(WavParser.TryParse(Wav(1, 1, 44100, 16, Array.Empty<byte>()), out _, out error));
        Assert.Contains("no samples", error);
    }

    [Fact]
    public void A_chunk_past_the_end_is_refused()
    {
        byte[] bad = new byte[8];
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("junk"), bad, 4);
        Put32(bad, 4, 1000);

        Assert.False(WavParser.TryParse(Riff(bad), out _, out var error));
        Assert.Contains("past the end", error);
    }

    private static byte[] Wav(int tag, int channels, int rate, int bits, byte[] data) =>
        Riff(Fmt(tag, channels, rate, bits), Chunk("data", data));

    private static byte[] Fmt(int tag, int channels, int rate, int bits)
    {
        byte[] body = new byte[16];
        Put16(body, 0, tag);
        Put16(body, 2, channels);
        Put32(body, 4, rate);
        Put32(body, 8, rate * channels * bits / 8);
        Put16(body, 12, channels * bits / 8);
        Put16(body, 14, bits);
        return Chunk("fmt ", body);
    }

    private static byte[] Chunk(string id, byte[] body)
    {
        byte[] chunk = new byte[8 + body.Length + (body.Length & 1)];
        Array.Copy(System.Text.Encoding.ASCII.GetBytes(id), chunk, 4);
        Put32(chunk, 4, body.Length);
        Array.Copy(body, 0, chunk, 8, body.Length);
        return chunk;
    }

    private static byte[] Riff(params byte[][] chunks)
    {
        byte[] body = chunks.SelectMany(c => c).ToArray();
        byte[] file = new byte[12 + body.Length];
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("RIFF"), file, 4);
        Put32(file, 4, 4 + body.Length);
        Array.Copy(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, file, 8, 4);
        Array.Copy(body, 0, file, 12, body.Length);
        return file;
    }

    private static byte[] Samples16(params short[] values) => values.SelectMany(BitConverter.GetBytes).ToArray();

    private static byte[] Samples16(params int[] values) => values.SelectMany(v => BitConverter.GetBytes((short)v)).ToArray();

    private static void Put16(byte[] b, int at, int v)
    {
        b[at] = (byte)v;
        b[at + 1] = (byte)(v >> 8);
    }

    private static void Put32(byte[] b, int at, int v)
    {
        Put16(b, at, v);
        Put16(b, at + 2, v >> 16);
    }
}
