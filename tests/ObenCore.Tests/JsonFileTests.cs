using ObenLoader.Config;

namespace ObenCore.Tests;

public class JsonFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "oben-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private string PathTo(string name) => Path.Combine(_dir, name);

    public class Sample
    {
        public string Name = "default";
        public int Count = 3;
        public List<string> Tags = new() { "x" };
        public string Optional;
        public Dictionary<string, float> Values = new();
    }

    [Fact]
    public void RoundTripKeepsEverything()
    {
        var original = new Sample
        {
            Name = "hello",
            Count = 42,
            Tags = new List<string> { "a", "b" },
            Values = { ["pi"] = 3.5f },
        };

        JsonFile.Write(PathTo("sample.json"), original);
        var loaded = JsonFile.Read<Sample>(PathTo("sample.json"));

        Assert.Equal("hello", loaded.Name);
        Assert.Equal(42, loaded.Count);
        Assert.Equal(new[] { "a", "b" }, loaded.Tags);
        Assert.Equal(3.5f, loaded.Values["pi"]);
    }

    [Fact]
    public void WriteCreatesMissingFolders()
    {
        string path = Path.Combine(_dir, "deep", "er", "file.json");
        JsonFile.Write(path, new Sample());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void WriteIsIndentedAndSkipsNulls()
    {
        JsonFile.Write(PathTo("s.json"), new Sample());
        string text = File.ReadAllText(PathTo("s.json"));
        Assert.Contains("\n", text);
        Assert.DoesNotContain("Optional", text);
    }

    [Fact]
    public void WriteUsesNoByteOrderMark()
    {
        JsonFile.Write(PathTo("s.json"), new Sample());
        byte[] bytes = File.ReadAllBytes(PathTo("s.json"));
        Assert.Equal((byte)'{', bytes[0]);
    }

    [Fact]
    public void OverwritingReplacesTheOldContentAndLeavesNoTempFile()
    {
        JsonFile.Write(PathTo("s.json"), new Sample { Name = "first", Tags = new List<string> { "1", "2", "3" } });
        JsonFile.Write(PathTo("s.json"), new Sample { Name = "second", Tags = new List<string> { "only" } });

        var loaded = JsonFile.Read<Sample>(PathTo("s.json"));
        Assert.Equal("second", loaded.Name);
        Assert.Equal(new[] { "only" }, loaded.Tags);
        Assert.Equal(new[] { PathTo("s.json") }, Directory.GetFiles(_dir));
    }

    [Fact]
    public void ReadOfAMissingFileGivesDefaults()
    {
        var loaded = JsonFile.Read<Sample>(PathTo("nope.json"));
        Assert.Equal("default", loaded.Name);
        Assert.Equal(3, loaded.Count);
    }

    [Fact]
    public void ReadOfPartialJsonKeepsFieldDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathTo("p.json"), "{ \"Count\": 9 }");
        var loaded = JsonFile.Read<Sample>(PathTo("p.json"));
        Assert.Equal(9, loaded.Count);
        Assert.Equal("default", loaded.Name);
    }

    [Fact]
    public void ReadOfLiteralNullGivesDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathTo("n.json"), "null");
        Assert.Equal("default", JsonFile.Read<Sample>(PathTo("n.json")).Name);
    }

    [Fact]
    public void ReadOfBrokenJsonThrows()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathTo("bad.json"), "{ not json");
        Assert.ThrowsAny<Exception>(() => JsonFile.Read<Sample>(PathTo("bad.json")));
    }

    [Fact]
    public void TryReadSucceedsOnAGoodFile()
    {
        JsonFile.Write(PathTo("s.json"), new Sample { Name = "ok" });
        Assert.True(JsonFile.TryRead<Sample>(PathTo("s.json"), out var value, out var error));
        Assert.Equal("ok", value.Name);
        Assert.Null(error);
    }

    [Fact]
    public void TryReadReportsAMissingFile()
    {
        Assert.False(JsonFile.TryRead<Sample>(PathTo("nope.json"), out var value, out var error));
        Assert.Null(value);
        Assert.IsType<FileNotFoundException>(error);
    }

    [Fact]
    public void TryReadReportsBrokenJson()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(PathTo("bad.json"), "{ not json");
        Assert.False(JsonFile.TryRead<Sample>(PathTo("bad.json"), out _, out var error));
        Assert.NotNull(error);
    }
}
