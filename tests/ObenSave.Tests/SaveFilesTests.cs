using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ObenSave.Files;
using ObenSave.Format;
using Xunit;

namespace ObenSave.Tests;

public sealed class SaveFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "obensave-tests-" + Guid.NewGuid().ToString("N"));

    public SaveFilesTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, true);

    private string Write(string relative, byte[] bytes)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string WriteSlot(string slot, int objects = 100)
    {
        Write(slot + "/Info.tnmt", SampleSaves.FileBytes("{\"SaveName\":\"" + slot + "\",\"Data\":[]}"));
        Write(slot + "/Globals.tnmt", SampleSaves.FileBytes(SampleSaves.LevelJson(objects)));
        Write(slot + "/Interior Player Tenement.tnmt", SampleSaves.FileBytes(SampleSaves.LevelJson(objects)));
        Write(slot + "/Globals.dialogue", SampleSaves.FileBytes("Variable = {}"));
        Write(slot + "/map.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 });
        return Path.Combine(_root, slot);
    }

    [Fact]
    public void Only_the_games_save_files_count_as_save_files()
    {
        Assert.True(SaveFiles.IsSaveFileName("/x/Slot1/Info.tnmt"));
        Assert.True(SaveFiles.IsSaveFileName("Interior Player Tenement.TNMT"));
        Assert.True(SaveFiles.IsSaveFileName("Globals.dialogue"));
        Assert.False(SaveFiles.IsSaveFileName("map.png"));
        Assert.False(SaveFiles.IsSaveFileName("Globals.tnmt.obensave.tmp"));
    }

    [Fact]
    public void Compress_then_restore_gives_back_the_same_file_and_time()
    {
        byte[] original = SampleSaves.FileBytes(SampleSaves.LevelJson());
        string path = Write("Slot1/Globals.tnmt", original);
        var time = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, time);

        Assert.Equal(ConvertResult.Converted, SaveFiles.Compress(path));
        Assert.True(SaveFiles.IsContainerFile(path));
        Assert.True(new FileInfo(path).Length < original.Length / 4);
        Assert.Equal(time, File.GetLastWriteTimeUtc(path));

        Assert.Equal(ConvertResult.Converted, SaveFiles.Restore(path));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(time, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Converting_twice_does_nothing_the_second_time()
    {
        string path = Write("Globals.tnmt", SampleSaves.FileBytes(SampleSaves.LevelJson(20)));

        SaveFiles.Compress(path);
        byte[] once = File.ReadAllBytes(path);

        Assert.Equal(ConvertResult.Unchanged, SaveFiles.Compress(path));
        Assert.Equal(once, File.ReadAllBytes(path));
        Assert.Equal(ConvertResult.Converted, SaveFiles.Restore(path));
        Assert.Equal(ConvertResult.Unchanged, SaveFiles.Restore(path));
    }

    [Fact]
    public void No_temp_file_is_left_behind()
    {
        string path = Write("Globals.tnmt", SampleSaves.FileBytes(SampleSaves.LevelJson(20)));

        SaveFiles.Compress(path);
        SaveFiles.Restore(path);

        Assert.Equal(new[] { path }, Directory.GetFiles(_root));
    }

    [Fact]
    public void Utf16_files_are_skipped_and_left_alone()
    {
        byte[] utf16 = Encoding.Unicode.GetPreamble().Then(Encoding.Unicode.GetBytes("{}"));
        string path = Write("Globals.tnmt", utf16);

        Assert.Equal(ConvertResult.Skipped, SaveFiles.Compress(path));
        Assert.Equal(utf16, File.ReadAllBytes(path));
    }

    [Fact]
    public void A_damaged_container_is_never_restored_over()
    {
        string path = Write("Globals.tnmt", SampleSaves.FileBytes(SampleSaves.LevelJson()));
        SaveFiles.Compress(path);
        byte[] data = File.ReadAllBytes(path);
        data[data.Length - 5] ^= 0xFF;
        File.WriteAllBytes(path, data);

        Assert.ThrowsAny<InvalidDataException>(() => SaveFiles.Restore(path));
        Assert.Equal(data, File.ReadAllBytes(path));
    }

    [Fact]
    public void A_vanilla_file_is_left_to_the_game()
    {
        string path = Write("Globals.tnmt", SampleSaves.FileBytes(SampleSaves.LevelJson(5)));

        Assert.False(SaveFiles.TryReadContainer(path, out string text));
        Assert.Null(text);
    }

    [Fact]
    public void A_container_reads_as_the_text_the_game_would_have_read()
    {
        string json = SampleSaves.LevelJson(30);
        string path = Write("Globals.tnmt", SampleSaves.FileBytes(json));
        string fromGame = File.ReadAllText(path, Encoding.UTF8);

        SaveFiles.Compress(path);

        Assert.True(SaveFiles.TryReadContainer(path, out string text));
        Assert.Equal(fromGame, text);
        Assert.Equal(json, text);
        Assert.Equal(json, SaveFiles.ReadText(path));
    }

    [Fact]
    public void A_damaged_container_is_reported_to_the_caller()
    {
        string path = Write("Globals.tnmt", SampleSaves.FileBytes(SampleSaves.LevelJson()));
        SaveFiles.Compress(path);
        byte[] data = File.ReadAllBytes(path);
        data[30] ^= 0xFF;
        File.WriteAllBytes(path, data);

        Assert.ThrowsAny<InvalidDataException>(() => SaveFiles.TryReadContainer(path, out _));
    }

    [Fact]
    public void A_missing_file_is_a_file_not_found()
    {
        Assert.Throws<FileNotFoundException>(() => SaveFiles.TryReadContainer(Path.Combine(_root, "none.tnmt"), out _));
    }

    [Fact]
    public void A_file_shorter_than_the_magic_is_vanilla()
    {
        string path = Write("Info.tnmt", new byte[] { 0xEF, 0xBB, 0xBF, (byte)'{' });

        Assert.False(SaveFiles.TryReadContainer(path, out _));
    }

    [Fact]
    public void A_folder_converts_both_ways_and_leaves_other_files_alone()
    {
        string slot = WriteSlot("Char/Slot1");
        var before = SaveFolders.Files(slot).ToDictionary(p => p, File.ReadAllBytes);
        byte[] map = File.ReadAllBytes(Path.Combine(slot, "map.png"));

        var packed = SaveFolders.Convert(slot, compress: true);

        Assert.Equal(4, packed.Converted);
        Assert.Equal(0, packed.Failed);
        Assert.True(packed.BytesAfter < packed.BytesBefore);
        SaveFolders.Count(slot, out int plain, out int containers);
        Assert.Equal((0, 4), (plain, containers));

        var unpacked = SaveFolders.Convert(slot, compress: false);

        Assert.Equal(4, unpacked.Converted);
        foreach (var pair in before)
            Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        Assert.Equal(map, File.ReadAllBytes(Path.Combine(slot, "map.png")));
    }

    [Fact]
    public void A_mixed_folder_converts_only_what_is_left()
    {
        string slot = WriteSlot("Char/Slot2");
        SaveFiles.Compress(Path.Combine(slot, "Info.tnmt"));

        var summary = SaveFolders.Convert(slot, compress: true);

        Assert.Equal(3, summary.Converted);
        Assert.Equal(1, summary.Unchanged);
    }

    [Fact]
    public void One_bad_file_does_not_stop_the_rest()
    {
        string slot = WriteSlot("Char/Slot3");
        string bad = Path.Combine(slot, "Globals.tnmt");
        SaveFiles.Compress(bad);
        byte[] data = File.ReadAllBytes(bad);
        data[data.Length - 3] ^= 0xFF;
        File.WriteAllBytes(bad, data);
        var errors = new List<string>();

        var summary = SaveFolders.Convert(slot, compress: false, (path, e) => errors.Add(Path.GetFileName(path)));

        Assert.Equal(1, summary.Failed);
        Assert.Equal(new[] { "Globals.tnmt" }, errors);
        Assert.Equal(data, File.ReadAllBytes(bad));
        Assert.False(SaveFiles.IsContainerFile(Path.Combine(slot, "Info.tnmt")));
    }

    [Fact]
    public void A_missing_folder_converts_nothing()
    {
        var summary = SaveFolders.Convert(Path.Combine(_root, "nope"), compress: true);

        Assert.Equal(0, summary.Converted + summary.Failed + summary.Unchanged);
    }
}
