using ObenFramework.Items;

namespace ObenCore.Tests.Items;

public class ItemManifestTests
{
    private static readonly int Key = ReservedIds.For("keymod", "rusty_key");
    private static readonly int Lamp = ReservedIds.For("lampmod", "lamp");

    private static ItemManifestEntry Entry(int id, string mod, string title) =>
        new ItemManifestEntry { Id = id, Mod = mod, Title = title };

    private static ItemManifestFile File(params ItemManifestEntry[] entries) =>
        new ItemManifestFile { Items = entries.ToList() };

    [Fact]
    public void CarriesOnlyItemsThatAreNotRegistered()
    {
        var book = new ItemManifestBook();
        book.Load(File(Entry(Key, "keymod", "Rusty Key"), Entry(Lamp, "lampmod", "Lamp")), id => id == Lamp);
        Assert.Equal(1, book.CarriedCount);
        Assert.Equal("Rusty Key", book.Find(Key)!.Title);
        Assert.Null(book.Find(Lamp));
    }

    [Fact]
    public void IgnoresIdsOutsideTheModRange()
    {
        var book = new ItemManifestBook();
        book.Load(File(Entry(18560, "x", "Paper Bag"), null!), _ => false);
        Assert.Equal(0, book.CarriedCount);
    }

    [Fact]
    public void SaveListsRegisteredThenCarriedOnce()
    {
        var book = new ItemManifestBook();
        book.Load(File(Entry(Key, "keymod", "Rusty Key"), Entry(Lamp, "lampmod", "Old Lamp")), _ => false);

        var file = book.Build(new[] { Entry(Lamp, "lampmod", "Lamp") });

        Assert.Equal(new[] { Lamp, Key }, file.Items.Select(e => e.Id));
        Assert.Equal("Lamp", file.Items[0].Title);
    }

    [Fact]
    public void ReturnedItemsStopBeingCarried()
    {
        var book = new ItemManifestBook();
        book.Load(File(Entry(Key, "keymod", "Rusty Key")), _ => false);
        book.Forget(Key);
        Assert.Empty(book.Build(Array.Empty<ItemManifestEntry>()).Items);
    }

    [Fact]
    public void UnloadedItemsAreCarriedUntilTheNextLoad()
    {
        var book = new ItemManifestBook();
        Assert.True(book.Carry(Entry(Key, "keymod", "Rusty Key")));
        Assert.False(book.Carry(Entry(Key, "keymod", "Rusty Key")));
        Assert.Equal("keymod", book.Find(Key)!.Mod);
        book.Load(File(), _ => false);
        Assert.Null(book.Find(Key));
    }

    [Fact]
    public void ForgetModDropsOnlyThatModsEntries()
    {
        var book = new ItemManifestBook();
        book.Load(File(Entry(Key, "keymod", "Rusty Key"), Entry(Lamp, "lampmod", "Lamp")), _ => false);
        Assert.Equal(1, book.ForgetMod("KEYMOD"));
        Assert.Null(book.Find(Key));
        Assert.NotNull(book.Find(Lamp));
        Assert.Equal(1, book.ForgetMod(null));
        Assert.Equal(0, book.CarriedCount);
    }

    [Fact]
    public void LoadIsCapped()
    {
        var entries = Enumerable.Range(0, ItemManifestBook.MaxCarried + 5)
            .Select(i => Entry(ReservedIds.Min + i, "m", "t" + i)).ToArray();
        var book = new ItemManifestBook();
        Assert.Equal(5, book.Load(File(entries), _ => false));
        Assert.Equal(ItemManifestBook.MaxCarried, book.CarriedCount);
    }

    [Fact]
    public void BuiltFileIsACopy()
    {
        var book = new ItemManifestBook();
        var registered = Entry(Key, "keymod", "Rusty Key");
        var file = book.Build(new[] { registered });
        file.Items[0].Title = "changed";
        Assert.Equal("Rusty Key", registered.Title);
    }

    [Theory]
    [InlineData("keymod", "Rusty Key", "Missing item (keymod: Rusty Key)")]
    [InlineData("keymod", null, "Missing item (keymod)")]
    [InlineData(null, "Rusty Key", "Missing item (Rusty Key)")]
    [InlineData(null, null, "Missing item")]
    public void Labels(string? mod, string? title, string expected) =>
        Assert.Equal(expected, ItemManifestBook.Label(Entry(1, mod!, title!)));

    [Fact]
    public void LabelWithoutEntry() => Assert.Equal("Missing item", ItemManifestBook.Label(null!));

    [Fact]
    public void RecipeLabels()
    {
        Assert.Equal("Missing recipe (keymod: Key Mould)", ItemManifestBook.Label(Entry(1, "keymod", "Key Mould"), "Missing recipe"));
        Assert.Equal("Missing recipe", ItemManifestBook.Label(null!, "Missing recipe"));
    }

    [Fact]
    public void SavesWithoutRecipesLoadEmpty()
    {
        var book = new ItemManifestBook();
        Assert.Equal(0, book.Load((List<ItemManifestEntry>)null!, _ => false));
        Assert.Equal(0, book.CarriedCount);
        Assert.Empty(new ItemManifestFile().Recipes);
    }

    [Fact]
    public void RecipesLoadFromTheirOwnList()
    {
        var file = new ItemManifestFile { Recipes = { Entry(Key, "keymod", "Key Mould") } };
        var book = new ItemManifestBook();
        book.Load(file.Recipes, _ => false);
        Assert.Equal("Key Mould", book.Find(Key)!.Title);
        Assert.Single(book.Build(Array.Empty<ItemManifestEntry>()).Items);
    }
}
