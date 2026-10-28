using ObenFramework.Crafting;
using ObenLoader.Runtime;

namespace ObenCore.Tests.Crafting;

public class FactorTableTests
{
    private sealed class Owner : ITracker
    {
        public List<IDisposable> Tracked { get; } = new();

        public T Track<T>(T disposable) where T : IDisposable
        {
            Tracked.Add(disposable);
            return disposable;
        }

        public void Unload()
        {
            foreach (var d in Tracked)
                d.Dispose();
            Tracked.Clear();
        }
    }

    [Fact]
    public void IsOneWithoutEntries()
    {
        var table = new FactorTable();
        Assert.False(table.Any);
        Assert.Equal(1f, table.Of("manufacturing"));
        Assert.Equal(1f, table.Combined(0.01f, 100f, "manufacturing", "Stove"));
    }

    [Fact]
    public void MultipliesFactorsOfDifferentOwners()
    {
        var table = new FactorTable();
        table.Set("a", "Stove", 2f);
        table.Set("b", "Stove", 1.5f);
        Assert.Equal(3f, table.Of("Stove"), 3);
    }

    [Fact]
    public void ReplacesAnOwnersEarlierFactor()
    {
        var table = new FactorTable();
        table.Set("a", "Stove", 2f);
        table.Set("a", "Stove", 4f);
        Assert.Equal(1, table.Count);
        Assert.Equal(4f, table.Of("Stove"));
    }

    [Fact]
    public void CombinesAllStationsWithEachKeyOnce()
    {
        var table = new FactorTable();
        table.Set("a", FactorTable.All, 2f);
        table.Set("a", "manufacturing", 3f);
        table.Set("b", "Stove", 5f);
        Assert.Equal(30f, table.Combined(0f, 1000f, "manufacturing", "Stove"), 3);
        // A key passed twice, or "*" passed as a key, is not counted again.
        Assert.Equal(6f, table.Combined(0f, 1000f, "manufacturing", "manufacturing", FactorTable.All), 3);
        Assert.Equal(2f, table.Combined(0f, 1000f, null), 3);
    }

    [Fact]
    public void ClampsTheProduct()
    {
        var table = new FactorTable();
        table.Set("a", "x", 50f);
        table.Set("b", "x", 50f);
        Assert.Equal(100f, table.Combined(0.01f, 100f, "x"));
        table.Set("a", "x", 0.0001f);
        Assert.Equal(0.01f, table.Combined(0.01f, 100f, "x"));
    }

    [Fact]
    public void RemovesOneEntryOrAllOfAnOwner()
    {
        var table = new FactorTable();
        table.Set("a", "x", 2f);
        table.Set("a", "y", 2f);
        table.Set("b", "x", 3f);
        Assert.True(table.Remove("a", "x"));
        Assert.False(table.Remove("a", "x"));
        Assert.Equal(3f, table.Of("x"));
        Assert.Equal(1, table.RemoveOwner("a"));
        Assert.Equal(1f, table.Of("y"));
    }

    [Fact]
    public void RefusesMissingOwnerOrKey()
    {
        var table = new FactorTable();
        Assert.Throws<ArgumentNullException>(() => table.Set(null, "x", 1f));
        Assert.Throws<ArgumentException>(() => table.Set("a", "", 1f));
    }

    [Fact]
    public void OwnedFactorsLeaveWithTheirOwner()
    {
        var factors = new OwnedFactors();
        var first = new Owner();
        var second = new Owner();
        factors.Set(first, "x", 2f);
        factors.Set(first, "y", 2f);
        factors.Set(second, "x", 3f);
        Assert.Single(first.Tracked);

        first.Unload();
        Assert.Equal(3f, factors.Table.Of("x"));
        Assert.Equal(1f, factors.Table.Of("y"));

        // Setting again after an unload tracks the owner afresh.
        factors.Set(first, "x", 2f);
        Assert.Single(first.Tracked);
        Assert.Equal(6f, factors.Table.Of("x"));
    }
}
