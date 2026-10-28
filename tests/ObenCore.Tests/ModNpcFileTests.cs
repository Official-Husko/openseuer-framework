using ObenFramework.Npcs;

namespace ObenCore.Tests;

public class ModNpcFileTests
{
    [Fact]
    public void IdsAreStableAndInTheModRange()
    {
        int first = ModNpcIds.Allocate("CoolMod", "barkeep", _ => false);
        int again = ModNpcIds.Allocate("CoolMod", "barkeep", _ => false);
        int other = ModNpcIds.Allocate("CoolMod", "guard", _ => false);

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
        Assert.True(ModNpcIds.IsModRange(first));
        Assert.False(ModNpcIds.IsModRange(42));
    }

    [Fact]
    public void TakenIdsAreSkipped()
    {
        int free = ModNpcIds.Allocate("CoolMod", "barkeep", _ => false);
        var taken = new HashSet<int> { free, free + 1 };
        Assert.Equal(free + 2, ModNpcIds.Allocate("CoolMod", "barkeep", taken.Contains));
    }

    [Fact]
    public void IdsWrapAtTheEndOfTheRange()
    {
        // Only the first id of the range is free: the search runs off the end and starts over.
        Assert.Equal(ModNpcIds.Base, ModNpcIds.Allocate("CoolMod", "barkeep", i => i != ModNpcIds.Base));
    }

    [Theory]
    [InlineData("barkeep", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("a/b", false)]
    public void NamesAreChecked(string? name, bool valid) => Assert.Equal(valid, ModNpcIds.ValidName(name));

    [Fact]
    public void LongNamesAreRefused() => Assert.False(ModNpcIds.ValidName(new string('x', 65)));

    [Fact]
    public void PutReplacesTheSameNpc()
    {
        var file = new ModNpcSaveFile();
        file.Put(Entry("A", "one", x: 1));
        file.Put(Entry("A", "one", x: 2));
        file.Put(Entry("B", "one"));

        Assert.Equal(2, file.Npcs.Count);
        Assert.Equal(2f, file.Find("A", "one").X);
        Assert.Equal(1, file.Count("A"));
        Assert.Equal(1, file.Drop("A", null));
        Assert.Null(file.Find("A", "one"));
    }

    [Fact]
    public void InSceneOnlyGivesLoadedMods()
    {
        var file = new ModNpcSaveFile();
        file.Put(Entry("A", "one", scene: "Bar"));
        file.Put(Entry("B", "two", scene: "Bar"));
        file.Put(Entry("A", "three", scene: "Street"));

        var here = file.InScene("Bar", mod => mod == "A");
        Assert.Single(here);
        Assert.Equal("one", here[0].Name);
    }

    [Fact]
    public void CleanDropsBrokenEntries()
    {
        var file = new ModNpcSaveFile();
        file.Npcs.Add(Entry("A", "fine"));
        file.Npcs.Add(null);
        file.Npcs.Add(Entry("", "nomod"));
        file.Npcs.Add(Entry("A", "a/b"));
        file.Npcs.Add(Entry("A", "noscene", scene: ""));
        file.Npcs.Add(Entry("A", "nan", x: float.NaN));
        var badTemplate = Entry("A", "template");
        badTemplate.Template = 0;
        file.Npcs.Add(badTemplate);

        var clean = ModNpcSaveFile.Clean(file);
        Assert.Single(clean.Npcs);
        Assert.Equal("fine", clean.Npcs[0].Name);
        Assert.Empty(ModNpcSaveFile.Clean(null).Npcs);
    }

    [Fact]
    public void CleanFillsDefaultsAndLimitsEachMod()
    {
        var file = new ModNpcSaveFile();
        var noNames = Entry("A", "x");
        noNames.FirstName = null;
        noNames.LastName = null;
        noNames.Look = "  ";
        noNames.Shapes = null;
        file.Npcs.Add(noNames);
        for (int i = 0; i < ModNpcSaveFile.MaxPerMod + 5; i++)
            file.Npcs.Add(Entry("B", "n" + i));

        var clean = ModNpcSaveFile.Clean(file);
        var x = clean.Find("A", "x");
        Assert.Equal("x", x.FirstName);
        Assert.Equal(string.Empty, x.LastName);
        Assert.Null(x.Look);
        Assert.Empty(x.Shapes);
        Assert.Equal(ModNpcSaveFile.MaxPerMod, clean.Count("B"));
    }

    [Fact]
    public void ShapesAreCleanedAndTheLastValueWins()
    {
        var shapes = new List<ModNpcShape>
        {
            new ModNpcShape { Mesh = "Body", Shape = "Fat", Weight = 10f },
            new ModNpcShape { Mesh = "", Shape = "Fat", Weight = 10f },
            new ModNpcShape { Mesh = "Body", Shape = "Thin", Weight = float.NaN },
            null,
            new ModNpcShape { Mesh = "Body", Shape = "Fat", Weight = 40f },
        };
        var clean = ModNpcSaveFile.CleanShapes(shapes);
        Assert.Single(clean);
        Assert.Equal(40f, clean[0].Weight);
    }

    private static ModNpcSaved Entry(string mod, string name, string scene = "Bar", float x = 0f) => new ModNpcSaved
    {
        Mod = mod,
        Name = name,
        Template = 11,
        FirstName = "Test",
        LastName = "Person",
        Scene = scene,
        X = x,
        Y = 1f,
        Z = 2f,
        Yaw = 90f,
        Body = 0,
        KeepDialogue = false,
    };
}
