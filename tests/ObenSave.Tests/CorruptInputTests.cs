using System;
using System.IO;
using ObenSave.Format;
using Xunit;

namespace ObenSave.Tests;

public class CorruptInputTests
{
    private static byte[] Container(int objects = 300) =>
        SaveContainer.Encode(SampleSaves.FileBytes(SampleSaves.LevelJson(objects)));

    private static void AssertDamaged(byte[] data)
    {
        var e = Assert.Throws<InvalidDataException>(() => SaveContainer.ReadText(data));
        Assert.Contains("damaged", e.Message);
        Assert.Throws<InvalidDataException>(() => SaveContainer.ToVanillaBytes(data));
    }

    [Fact]
    public void A_flipped_payload_byte_is_caught()
    {
        byte[] data = Container();
        data[data.Length / 2] ^= 0x55;

        Assert.ThrowsAny<InvalidDataException>(() => SaveContainer.ReadText(data));
    }

    [Fact]
    public void A_truncated_payload_is_caught()
    {
        byte[] data = Container();
        Array.Resize(ref data, data.Length - 20);

        AssertDamaged(data);
    }

    [Fact]
    public void Trailing_bytes_after_a_deflate_stream_do_not_change_the_content()
    {
        // The Deflate stream ends by itself; what follows it is ignored, and the length and CRC-32 still hold.
        byte[] data = Container();
        string expected = SaveContainer.ReadText(data);
        Array.Resize(ref data, data.Length + 4);

        Assert.Equal(expected, SaveContainer.ReadText(data));
    }

    [Fact]
    public void A_wrong_checksum_is_caught()
    {
        byte[] data = Container();
        data[16] ^= 0xFF;

        AssertDamaged(data);
    }

    [Fact]
    public void A_stored_file_with_a_wrong_length_is_caught()
    {
        byte[] data = SaveContainer.Encode(SampleSaves.FileBytes("{}"));
        Array.Resize(ref data, data.Length + 1);

        AssertDamaged(data);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(1000000)]
    public void A_wrong_length_field_is_caught(int change)
    {
        byte[] data = Container();
        int length = BitConverter.ToInt32(data, 12) + change;
        BitConverter.GetBytes(length).CopyTo(data, 12);

        AssertDamaged(data);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void An_impossible_length_is_refused_without_allocating_it(int length)
    {
        byte[] data = Container();
        BitConverter.GetBytes(length).CopyTo(data, 12);

        Assert.Throws<InvalidDataException>(() => SaveContainer.ReadText(data));
    }

    [Fact]
    public void A_newer_version_is_refused_with_a_clear_message()
    {
        byte[] data = Container();
        data[8] = 2;

        var e = Assert.Throws<InvalidDataException>(() => SaveContainer.ReadText(data));
        Assert.Contains("newer", e.Message);
    }

    [Fact]
    public void An_unknown_codec_is_refused()
    {
        byte[] data = Container();
        data[9] = 7;

        var e = Assert.Throws<InvalidDataException>(() => SaveContainer.ReadText(data));
        Assert.Contains("codec", e.Message);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(19)]
    public void A_cut_off_header_is_refused(int size)
    {
        byte[] data = Container();
        Array.Resize(ref data, size);

        Assert.True(SaveContainer.IsContainer(data));
        Assert.Throws<InvalidDataException>(() => SaveContainer.ReadText(data));
    }

    [Fact]
    public void Garbage_after_the_magic_never_escapes_as_another_exception()
    {
        var random = new Random(7);
        for (int run = 0; run < 200; run++)
        {
            byte[] data = Container(5);
            for (int i = 8; i < data.Length; i++)
            {
                if (random.Next(6) == 0)
                    data[i] = (byte)random.Next(256);
            }

            try
            {
                SaveContainer.ReadText(data);
            }
            catch (InvalidDataException)
            {
            }
        }
    }

    [Fact]
    public void A_deflate_stream_that_expands_past_its_length_is_stopped()
    {
        byte[] data = Container(2000);
        BitConverter.GetBytes(10).CopyTo(data, 12);

        AssertDamaged(data);
    }
}
