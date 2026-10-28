using ObenFramework.Items;

namespace ObenCore.Tests.Items;

public class LootWeightsTests
{
    [Fact]
    public void AddsTheItemOncePerWeight()
    {
        var pool = new List<int> { 4 };
        LootWeights.Add(pool, 12, 3);
        Assert.Equal(new[] { 4, 12, 12, 12 }, pool);
    }

    [Fact]
    public void RemovesFromTheEndAndOnlyWhatIsThere()
    {
        var pool = new List<int> { 12, 4, 12, 7, 12 };
        Assert.Equal(2, LootWeights.Remove(pool, 12, 2));
        Assert.Equal(new[] { 12, 4, 7 }, pool);
        Assert.Equal(1, LootWeights.Remove(pool, 12, 5));
        Assert.Equal(0, LootWeights.Remove(pool, 99, 1));
        Assert.Equal(new[] { 4, 7 }, pool);
    }

    [Fact]
    public void CountsWeightsInFirstListedOrder()
    {
        var counts = LootWeights.Count(new List<int> { 7, 12, 7, 3, 12, 7 });
        Assert.Equal(new[]
        {
            new KeyValuePair<int, int>(7, 3),
            new KeyValuePair<int, int>(12, 2),
            new KeyValuePair<int, int>(3, 1),
        }, counts);
        Assert.Empty(LootWeights.Count(new List<int>()));
    }

    [Fact]
    public void AddThenRemoveLeavesThePoolAsItWas()
    {
        var pool = new List<int> { 1, 2, 3, 2 };
        var original = pool.ToList();
        LootWeights.Add(pool, 2, 4);
        LootWeights.Remove(pool, 2, 4);
        Assert.Equal(original, pool);
    }
}
