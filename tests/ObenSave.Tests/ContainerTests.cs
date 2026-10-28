using System.IO;
using System.Text;
using ObenSave.Format;
using Xunit;

namespace ObenSave.Tests;

public class ContainerTests
{
    [Fact]
    public void Crc32_matches_the_standard_check_value()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_file_comes_back_byte_for_byte(bool bom)
    {
        byte[] file = SampleSaves.FileBytes(SampleSaves.LevelJson(), bom);

        byte[] container = SaveContainer.Encode(file);

        Assert.True(SaveContainer.IsContainer(container));
        Assert.Equal(file, SaveContainer.ToVanillaBytes(container));
    }

    [Fact]
    public void Compressing_json_makes_it_much_smaller()
    {
        byte[] file = SampleSaves.FileBytes(SampleSaves.LevelJson(2000));

        byte[] container = SaveContainer.Encode(file);

        Assert.True(container.Length < file.Length / 5, $"{container.Length} of {file.Length}");
        Assert.Equal(SaveCodec.Deflate, SaveContainer.Inspect(container).Codec);
    }

    [Fact]
    public void Header_fields_are_laid_out_as_documented()
    {
        string text = SampleSaves.LevelJson(50);
        byte[] container = SaveContainer.Encode(SampleSaves.FileBytes(text));

        Assert.Equal("OBENSAVE", Encoding.ASCII.GetString(container, 0, 8));
        Assert.Equal(1, container[8]);
        Assert.Equal(1, container[9]);
        Assert.Equal(1, container[10]);
        Assert.Equal(0, container[11]);
        Assert.Equal(Encoding.UTF8.GetByteCount(text), System.BitConverter.ToInt32(container, 12));
        Assert.Equal(Crc32.Compute(Encoding.UTF8.GetBytes(text)), System.BitConverter.ToUInt32(container, 16));
    }

    [Fact]
    public void Tiny_files_are_stored_when_deflate_would_not_help()
    {
        byte[] file = SampleSaves.FileBytes("{}");

        byte[] container = SaveContainer.Encode(file);

        var info = SaveContainer.Inspect(container);
        Assert.Equal(SaveCodec.Stored, info.Codec);
        Assert.Equal(2, info.ContentLength);
        Assert.Equal(file, SaveContainer.ToVanillaBytes(container));
    }

    [Fact]
    public void An_empty_file_round_trips()
    {
        byte[] container = SaveContainer.Encode(new byte[0]);

        Assert.Empty(SaveContainer.ToVanillaBytes(container));
        Assert.Equal("", SaveContainer.ReadText(container));
    }

    [Fact]
    public void Text_is_read_without_the_byte_order_mark()
    {
        string text = SampleSaves.LevelJson(10);
        byte[] plain = SampleSaves.FileBytes(text);
        byte[] container = SaveContainer.Encode(plain);

        Assert.Equal(text, SaveContainer.ReadText(plain));
        Assert.Equal(text, SaveContainer.ReadText(container));
        Assert.DoesNotContain('\uFEFF', SaveContainer.ReadText(container));
    }

    [Fact]
    public void Plain_text_without_a_mark_is_read_as_utf8()
    {
        Assert.Equal("{\"name\":\"Sauna\"}", SaveContainer.ReadText(Encoding.UTF8.GetBytes("{\"name\":\"Sauna\"}")));
    }

    [Fact]
    public void Non_ascii_text_survives()
    {
        string text = "{\"name\":\"Päivi Äikäs\",\"note\":\"öå € 日本語 😀\"}";
        byte[] file = SampleSaves.FileBytes(text);

        byte[] container = SaveContainer.Encode(file);

        Assert.Equal(text, SaveContainer.ReadText(container));
        Assert.Equal(file, SaveContainer.ToVanillaBytes(container));
    }

    [Fact]
    public void Text_encoding_keeps_the_mark_the_game_writes()
    {
        byte[] container = SaveContainer.EncodeText("{\"a\":1}");

        Assert.Equal(SampleSaves.FileBytes("{\"a\":1}"), SaveContainer.ToVanillaBytes(container));
        Assert.True(SaveContainer.Inspect(container).HasBom);
        Assert.False(SaveContainer.Inspect(SaveContainer.EncodeText("{}", bom: false)).HasBom);
    }

    [Fact]
    public void Encoding_a_container_again_changes_nothing()
    {
        byte[] container = SaveContainer.Encode(SampleSaves.FileBytes(SampleSaves.LevelJson(20)));

        Assert.Same(container, SaveContainer.Encode(container));
    }

    [Fact]
    public void Utf16_files_are_refused()
    {
        byte[] utf16 = Encoding.Unicode.GetPreamble().Then(Encoding.Unicode.GetBytes("{}"));

        Assert.Throws<InvalidDataException>(() => SaveContainer.Encode(utf16));
    }

    [Fact]
    public void Inspect_describes_vanilla_files()
    {
        var with = SaveContainer.Inspect(SampleSaves.FileBytes("{}", bom: true));
        var without = SaveContainer.Inspect(SampleSaves.FileBytes("{}", bom: false));

        Assert.False(with.IsContainer);
        Assert.True(with.HasBom);
        Assert.Equal(2, with.ContentLength);
        Assert.Equal(5, with.FileLength);
        Assert.False(without.HasBom);
        Assert.Equal(2, without.FileLength);
    }

    [Fact]
    public void Vanilla_bytes_are_never_mistaken_for_a_container()
    {
        Assert.False(SaveContainer.IsContainer(SampleSaves.FileBytes(SampleSaves.LevelJson(5))));
        Assert.False(SaveContainer.IsContainer(Encoding.ASCII.GetBytes("OBENSAV")));
        Assert.False(SaveContainer.IsContainer(null));
        Assert.False(SaveContainer.IsContainer(new byte[0]));
    }

    [Fact]
    public void Vanilla_bytes_pass_through_untouched()
    {
        byte[] file = SampleSaves.FileBytes(SampleSaves.LevelJson(5));

        Assert.Same(file, SaveContainer.ToVanillaBytes(file));
    }
}

internal static class ByteExtensions
{
    public static byte[] Then(this byte[] first, byte[] second)
    {
        var all = new byte[first.Length + second.Length];
        first.CopyTo(all, 0);
        second.CopyTo(all, first.Length);
        return all;
    }
}
