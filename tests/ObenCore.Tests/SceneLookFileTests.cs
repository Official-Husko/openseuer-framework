using ObenFramework.SceneLook;

namespace ObenCore.Tests;

public class SceneLookFileTests
{
    // Root "Npc" (go 1, transform 2) with a child "Bip001 Pelvis" (go 3, transform 4).
    private static SceneFileWriter Hierarchy() => new SceneFileWriter()
        .Add(1, SceneLookClass.GameObject, ObjectBytes.GameObject("Npc", 2))
        .Add(2, SceneLookClass.Transform, ObjectBytes.Transform(1, 0, 4))
        .Add(3, SceneLookClass.GameObject, ObjectBytes.GameObject("Bip001 Pelvis", 4))
        .Add(4, SceneLookClass.Transform, ObjectBytes.Transform(3, 2));

    [Fact]
    public void Opens_a_file_and_reads_its_objects()
    {
        using var folder = new TempFolder();
        Hierarchy().External("sharedassets0.assets").Write(folder.File("level0"));
        using var files = new SceneLookFiles(folder.Path);

        var file = files.Get("level0", out string reason);

        Assert.True(file != null, reason);
        Assert.Equal("6000.0.77f1", file.UnityString);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, file.Order);
        Assert.Equal(SceneLookClass.Transform, file.ClassId(4));
        Assert.Equal(new[] { "sharedassets0.assets" }, file.Externals);
        Assert.Same(file, files.Get("level0", out _));
    }

    [Fact]
    public void Walks_the_hierarchy()
    {
        using var folder = new TempFolder();
        Hierarchy().Write(folder.File("level0"));
        using var files = new SceneLookFiles(folder.Path);
        var file = files.Get("level0", out _);

        Assert.Equal("Bip001 Pelvis", SceneLookReader.GameObject(file, 3).Name);
        Assert.Equal(4, SceneLookReader.TransformOf(file, 3));
        Assert.Equal(new long[] { 3, 1 }, SceneLookReader.Chain(file, 3));
        Assert.Equal(new long[] { 1, 3 }, SceneLookReader.Subtree(file, 1));
        Assert.Equal("Npc/Bip001 Pelvis", SceneLookReader.PathName(file, 3));
        Assert.Equal(0, SceneLookReader.BodyRoot(file, 3));
    }

    [Fact]
    public void References_resolve_inside_and_across_files()
    {
        using var folder = new TempFolder();
        Hierarchy().External("other.assets").External("Library/unity default resources").Write(folder.File("level0"));
        new SceneFileWriter().Add(7, SceneLookClass.GameObject, ObjectBytes.GameObject("Elsewhere")).Write(folder.File("other.assets"));
        using var files = new SceneLookFiles(folder.Path);
        var file = files.Get("level0", out _);

        Assert.Equal("null", file.Target(new SceneLookRef(0, 0)).Error);
        Assert.Same(file, file.Target(new SceneLookRef(0, 3)).File);
        Assert.Equal("pptr target missing", file.Target(new SceneLookRef(0, 99)).Error);
        var external = file.Target(new SceneLookRef(1, 7));
        Assert.Null(external.Error);
        Assert.Equal("other.assets", external.File.Name);
        Assert.Equal("external not installed", file.Target(new SceneLookRef(2, 1)).Error);
        Assert.Equal("bad fileID", file.Target(new SceneLookRef(5, 1)).Error);
    }

    [Theory]
    [InlineData(21, "6000.0.77f1", 0, "serialized format 21")]
    [InlineData(22, "2022.3.1f1", 0, "Unity version 2022.3.1f1")]
    [InlineData(22, "6000.0.77f1", 1, "type trees")]
    public void Refuses_other_layouts(int format, string unity, byte typeTrees, string expected)
    {
        using var folder = new TempFolder();
        var writer = Hierarchy();
        writer.Format = format;
        writer.UnityVersion = unity;
        writer.TypeTrees = typeTrees;
        writer.Write(folder.File("level0"));
        using var files = new SceneLookFiles(folder.Path);

        Assert.Null(files.Get("level0", out string reason));
        Assert.Contains(expected, reason);
        Assert.Null(files.Get("level0", out string again));
        Assert.Equal(reason, again);
    }

    [Fact]
    public void Only_plain_names_in_the_folder_are_opened()
    {
        using var folder = new TempFolder();
        using var files = new SceneLookFiles(folder.Path);

        Assert.Null(files.Get("../level0", out string bad));
        Assert.StartsWith("bad file name", bad);
        Assert.Null(files.Get("level9", out string missing));
        Assert.StartsWith("not installed", missing);
        Assert.False(SceneLookFiles.IsPlainName("archive:/x"));
        Assert.True(SceneLookFiles.IsPlainName("resources.assets"));
    }

    [Fact]
    public void Streams_are_read_within_the_file()
    {
        using var folder = new TempFolder();
        File.WriteAllBytes(folder.File("level0.resS"), new byte[] { 1, 2, 3, 4, 5 });
        using var files = new SceneLookFiles(folder.Path);

        Assert.Equal(new byte[] { 3, 4 }, files.ReadStream("level0.resS", 2, 2));
        Assert.True(files.Installed("level0.resS", 5));
        Assert.False(files.Installed("level0.resS", 6));
        Assert.Throws<InvalidDataException>(() => files.ReadStream("level0.resS", 4, 2));
    }
}
