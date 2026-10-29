using ObenLoader.Config;

namespace ObenCore.Tests;

public class JsonFileEnumTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "oben-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    public enum Size { Small, Medium, Large }

    [Flags]
    public enum Extras { None = 0, Lamp = 1, Radio = 2, Fridge = 4 }

    public class Flat
    {
        public Size Size = Size.Medium;
        public Extras Furniture = Extras.Lamp | Extras.Fridge;
        public int Rooms = 2;
        public string Name = "flat";
        public List<Size> Others = new();
    }

    private string Write(string json)
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "flat.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void EnumsAreWrittenByName()
    {
        string path = Path.Combine(_dir, "flat.json");
        JsonFile.Write(path, new Flat { Size = Size.Large });
        string text = File.ReadAllText(path);

        Assert.Contains("\"Size\": \"Large\"", text);
        Assert.Contains("\"Furniture\": \"Lamp, Fridge\"", text);
        Assert.Equal(Extras.Lamp | Extras.Fridge, JsonFile.Read<Flat>(path).Furniture);
    }

    [Fact]
    public void NumbersFromOlderFilesStillRead()
    {
        var flat = JsonFile.Read<Flat>(Write("{\"Size\": 2, \"Furniture\": 3}"));

        Assert.Equal(Size.Large, flat.Size);
        Assert.Equal(Extras.Lamp | Extras.Radio, flat.Furniture);
    }

    [Fact]
    public void NamesReadInAnyCase() =>
        Assert.Equal(Size.Small, JsonFile.Read<Flat>(Write("{\"Size\": \"small\"}")).Size);

    [Fact]
    public void AMisspeltValueKeepsOnlyItsOwnDefault()
    {
        var skipped = new List<string>();
        var flat = JsonFile.Read<Flat>(Write("{\"Size\": \"Hueg\", \"Rooms\": \"three\", \"Name\": \"mine\", \"Others\": [\"Small\", \"Tiny\", \"Large\"]}"), skipped);

        Assert.Equal(Size.Medium, flat.Size);
        Assert.Equal(2, flat.Rooms);
        Assert.Equal("mine", flat.Name);
        Assert.Equal(new[] { Size.Small, Size.Large }, flat.Others);
        Assert.Equal(3, skipped.Count);
        Assert.StartsWith("Size: ", skipped[0]);
        Assert.StartsWith("Rooms: ", skipped[1]);
    }

    [Fact]
    public void TheStrictReadStillFailsOnAMisspeltValue() =>
        Assert.ThrowsAny<Exception>(() => JsonFile.Read<Flat>(Write("{\"Size\": \"Hueg\"}")));

    [Fact]
    public void AFileThatIsNotJsonStillFails() =>
        Assert.ThrowsAny<Exception>(() => JsonFile.Read<Flat>(Write("{\"Size\": \"Large\", \"Name\": "), new List<string>()));

    [Fact]
    public void TooManyBadValuesFailTheFile()
    {
        string list = string.Join(", ", Enumerable.Repeat("\"Tiny\"", 25));
        var skipped = new List<string>();

        Assert.ThrowsAny<Exception>(() => JsonFile.Read<Flat>(Write("{\"Others\": [" + list + "]}"), skipped));
        Assert.Equal(20, skipped.Count);
    }
}
