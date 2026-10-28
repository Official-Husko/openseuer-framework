using ObenFramework.Patching;

namespace ObenCore.Tests;

public class PatchAreaBookTests
{
    [Fact]
    public void UnknownAreasAreAvailable()
    {
        var book = new PatchAreaBook();
        Assert.False(book.IsDisabled("Mod", "Shops"));
        Assert.Empty(book.Snapshot());
    }

    [Fact]
    public void FailingUndoesTheAreaNewestFirst()
    {
        var book = new PatchAreaBook();
        var undone = new List<int>();
        book.Applied("Mod", "Shops", () => undone.Add(1));
        book.Applied("Mod", "Shops", () => undone.Add(2));
        book.Applied("Mod", "World", () => undone.Add(3));

        Assert.True(book.Fail("Mod", "Shops", "Shop.Buy is gone"));

        Assert.Equal(new[] { 2, 1 }, undone);
        Assert.True(book.IsDisabled("Mod", "Shops"));
        Assert.False(book.IsDisabled("Mod", "World"));
    }

    [Fact]
    public void FailingTwiceOnlyCountsOnce()
    {
        var book = new PatchAreaBook();
        int undone = 0;
        book.Applied("Mod", "Shops", () => undone++);
        Assert.True(book.Fail("Mod", "Shops", "first"));
        Assert.False(book.Fail("Mod", "Shops", "second"));
        Assert.Equal(1, undone);
        Assert.Equal("first", book.Snapshot()[0].Error);
    }

    [Fact]
    public void AnUndoThatThrowsDoesNotStopTheOthers()
    {
        var book = new PatchAreaBook();
        var errors = new List<Exception>();
        bool second = false;
        book.Applied("Mod", "A", () => second = true);
        book.Applied("Mod", "A", () => throw new InvalidOperationException("boom"));

        book.Fail("Mod", "A", "x", errors.Add);

        Assert.True(second);
        Assert.Single(errors);
    }

    [Fact]
    public void AreasAreKeptPerOwner()
    {
        var book = new PatchAreaBook();
        book.Applied("One", "Shops", null);
        book.Applied("Two", "Shops", null);
        book.Fail("One", "Shops", "x");
        Assert.True(book.IsDisabled("One", "Shops"));
        Assert.False(book.IsDisabled("Two", "Shops"));
        Assert.Single(book.Snapshot("Two"));
    }

    [Fact]
    public void ForgetDropsAnOwnersAreas()
    {
        var book = new PatchAreaBook();
        book.Applied("One", "A", null);
        book.Fail("One", "A", "x");
        book.Applied("Two", "B", null);
        book.Forget("one");
        Assert.False(book.IsDisabled("One", "A"));
        var left = Assert.Single(book.Snapshot());
        Assert.Equal("Two", left.Owner);
    }

    [Fact]
    public void SnapshotCountsPatches()
    {
        var book = new PatchAreaBook();
        book.Touch("Mod", "Empty");
        book.Applied("Mod", "Two", null);
        book.Applied("Mod", "Two", null);
        var areas = book.Snapshot();
        Assert.Equal(new[] { "Empty", "Two" }, areas.Select(a => a.Name));
        Assert.Equal(0, areas[0].Patches);
        Assert.Equal(2, areas[1].Patches);
        Assert.True(areas[1].IsAvailable);
    }

    [Theory]
    [InlineData("ObenFramework.Economy", "ObenFramework", "Economy")]
    [InlineData("ObenFramework.World.Logic", "ObenFramework", "World.Logic")]
    [InlineData("ObenFramework", "ObenFramework", "Main")]
    [InlineData(null, "ObenFramework", "Main")]
    [InlineData("Other.Space", "ObenFramework", "Other.Space")]
    [InlineData("ObenFrameworkExtra.X", "ObenFramework", "ObenFrameworkExtra.X")]
    public void AreaFromNamespace(string? ns, string root, string expected) =>
        Assert.Equal(expected, PatchAreaBook.AreaFromNamespace(ns!, root));
}

public class MethodFinderTests
{
    private class Sample
    {
        public Sample() { }
        public Sample(int value) { }
        static Sample() { }
        public void Single() { }
        public void Twice(int a) { }
        public void Twice(string a) { }
        private static int Hidden(ref int a) => a;
        public int Value { get; set; }
    }

    [Fact]
    public void FindsAUniqueName()
    {
        var method = MethodFinder.Find(typeof(Sample), "Single", null, out string problem);
        Assert.NotNull(method);
        Assert.Null(problem);
    }

    [Fact]
    public void OverloadsNeedArgumentTypes()
    {
        Assert.Null(MethodFinder.Find(typeof(Sample), "Twice", null, out string problem));
        Assert.Contains("overloads", problem);
        var method = MethodFinder.Find(typeof(Sample), "Twice", new[] { typeof(string) }, out _);
        Assert.Equal(typeof(string), method!.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void MissingMethodsSaySo()
    {
        Assert.Null(MethodFinder.Find(typeof(Sample), "Gone", null, out string problem));
        Assert.Contains("doesn't exist", problem);
        Assert.Null(MethodFinder.Find(typeof(Sample), "Single", new[] { typeof(int) }, out problem));
    }

    [Fact]
    public void FindsConstructorsAccessorsAndPrivateMembers()
    {
        Assert.NotNull(MethodFinder.Find(typeof(Sample), ".ctor", new[] { typeof(int) }, out _));
        Assert.Null(MethodFinder.Find(typeof(Sample), ".ctor", null, out string problem));
        Assert.Contains("overloads", problem);
        Assert.NotNull(MethodFinder.Find(typeof(Sample), ".cctor", null, out _));
        Assert.NotNull(MethodFinder.Find(typeof(Sample), "get_Value", null, out _));
        Assert.NotNull(MethodFinder.Find(typeof(Sample), "Hidden", new[] { typeof(int).MakeByRefType() }, out _));
    }

    [Fact]
    public void HandlerMustExist()
    {
        Assert.NotNull(MethodFinder.Handler(typeof(MethodFinderTests), nameof(StaticHandler)));
        Assert.Throws<MissingMethodException>(() => MethodFinder.Handler(typeof(MethodFinderTests), "Nope"));
    }

    [Fact]
    public void DescribeListsArguments() =>
        Assert.EndsWith("Sample.Twice(Int32, String)", MethodFinder.Describe(typeof(Sample), "Twice", new[] { typeof(int), typeof(string) }));

    private static void StaticHandler() { }
}

public class PatchErrorsTests
{
    [Fact]
    public void AddsInnerMessagesOnOneLine()
    {
        var e = new InvalidOperationException("Patching exception in method X\nmore",
            new ArgumentException("Undefined target method"));
        string text = PatchErrors.Describe(e);
        Assert.Equal("InvalidOperationException: Patching exception in method X more | ArgumentException: Undefined target method", text);
    }

    [Fact]
    public void RepeatedMessagesAppearOnce()
    {
        var inner = new Exception("same");
        var e = new Exception("same", new Exception("same", inner));
        Assert.Equal("Exception: same", PatchErrors.Describe(e));
    }

    [Fact]
    public void LongTextIsCut()
    {
        string text = PatchErrors.Describe(new Exception(new string('a', 2000)));
        Assert.True(text.Length < 700);
        Assert.EndsWith("...", text);
    }

    [Fact]
    public void NullIsUnknown() => Assert.Equal("unknown error", PatchErrors.Describe(null!));
}
