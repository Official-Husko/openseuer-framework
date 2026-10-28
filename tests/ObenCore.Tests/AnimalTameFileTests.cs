using ObenFramework.Npcs;

namespace ObenCore.Tests;

public class AnimalTameFileTests
{
    [Fact]
    public void ASecondChoiceReplacesTheFirst()
    {
        var file = new AnimalTameFile();
        Assert.True(file.Set("A", "Yard", "cat1", true));
        Assert.True(file.Set("A", "Yard", "cat1", false));

        Assert.Single(file.Entries);
        Assert.False(file.For("Yard", "cat1", _ => true));
    }

    [Fact]
    public void OnlyLoadedModsCountAndTheLastOneWins()
    {
        var file = new AnimalTameFile();
        file.Set("A", "Yard", "cat1", true);
        file.Set("B", "Yard", "cat1", false);

        Assert.False(file.For("Yard", "cat1", _ => true));
        Assert.True(file.For("Yard", "cat1", mod => mod == "A"));
        Assert.Null(file.For("Yard", "cat1", _ => false));
        Assert.Null(file.For("Street", "cat1", _ => true));
    }

    [Fact]
    public void ForgetDropsOneModsChoices()
    {
        var file = new AnimalTameFile();
        file.Set("A", "Yard", "cat1", true);
        file.Set("A", "Yard", "cat2", true);
        file.Set("B", "Yard", "cat1", true);

        Assert.Equal(2, file.Forget("A"));
        Assert.Equal(0, file.Count("A"));
        Assert.Equal(1, file.Count("B"));
    }

    [Fact]
    public void AModCannotFillTheFileBeyondItsShare()
    {
        var file = new AnimalTameFile();
        for (int i = 0; i < AnimalTameFile.MaxPerMod; i++)
            Assert.True(file.Set("A", "Yard", "a" + i, true));
        Assert.False(file.Set("A", "Yard", "one more", true));
        Assert.True(file.Set("A", "Yard", "a0", false));
        Assert.True(file.Set("B", "Yard", "b", true));
    }

    [Fact]
    public void CleanDropsBrokenEntriesAndRepeats()
    {
        var file = new AnimalTameFile();
        file.Entries.Add(new AnimalTameEntry { Mod = "A", Scene = "Yard", Guid = "cat1", Tame = true });
        file.Entries.Add(new AnimalTameEntry { Mod = "A", Scene = "Yard", Guid = "cat1", Tame = false });
        file.Entries.Add(new AnimalTameEntry { Mod = "", Scene = "Yard", Guid = "cat2" });
        file.Entries.Add(new AnimalTameEntry { Mod = "A", Scene = "Yard", Guid = null });
        file.Entries.Add(null);

        var clean = AnimalTameFile.Clean(file);
        Assert.Single(clean.Entries);
        Assert.False(clean.Entries[0].Tame);
        Assert.Empty(AnimalTameFile.Clean(null).Entries);
    }
}
