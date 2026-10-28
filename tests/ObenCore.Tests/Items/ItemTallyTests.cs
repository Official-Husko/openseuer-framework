using ObenFramework.Items;

namespace ObenCore.Tests.Items;

public class ItemTallyTests
{
    private static List<KeyValuePair<int, int>> Diff(ItemTally tally)
    {
        var changes = new List<KeyValuePair<int, int>>();
        tally.Diff(changes);
        return changes;
    }

    private static ItemTally Committed(params (int id, int amount)[] stacks)
    {
        var tally = new ItemTally();
        tally.Begin();
        foreach (var (id, amount) in stacks)
            tally.Add(id, amount);
        tally.Commit();
        return tally;
    }

    [Fact]
    public void StartsWithoutABaseline()
    {
        var tally = new ItemTally();
        Assert.False(tally.HasBaseline);
        tally.Begin();
        tally.Commit();
        Assert.True(tally.HasBaseline);
    }

    [Fact]
    public void SumsStacksOfTheSameItem()
    {
        var tally = Committed((12, 3), (12, 4), (5, 1));
        Assert.Equal(7, tally.Committed(12));
        Assert.Equal(1, tally.Committed(5));
        Assert.Equal(0, tally.Committed(99));
    }

    [Fact]
    public void IgnoresEmptySlotsAndZeroAmounts()
    {
        var tally = Committed((-1, 0), (12, 0), (-1, 5));
        Assert.Equal(0, tally.Committed(12));
        Assert.Equal(0, tally.Committed(-1));
    }

    [Fact]
    public void ReportsNothingWhenAStackOnlyMoved()
    {
        var tally = Committed((12, 3), (7, 1));
        tally.Begin();
        tally.Add(7, 1);
        tally.Add(12, 2);
        tally.Add(12, 1);
        Assert.Empty(Diff(tally));
    }

    [Fact]
    public void ReportsAdditionsAndRemovalsWithRemovalsFirst()
    {
        var tally = Committed((12, 3), (7, 1), (40, 2));
        tally.Begin();
        tally.Add(12, 5);   // +2
        tally.Add(40, 2);   // unchanged
        tally.Add(3, 1);    // new
        // 7 is gone
        var changes = Diff(tally);

        Assert.Equal(new[]
        {
            new KeyValuePair<int, int>(7, -1),
            new KeyValuePair<int, int>(3, 1),
            new KeyValuePair<int, int>(12, 2),
        }, changes);
    }

    [Fact]
    public void CommitMakesTheNewCountTheBaseline()
    {
        var tally = Committed((12, 3));
        tally.Begin();
        tally.Add(12, 1);
        Assert.Single(Diff(tally));
        tally.Commit();
        Assert.Equal(1, tally.Committed(12));

        tally.Begin();
        tally.Add(12, 1);
        Assert.Empty(Diff(tally));
    }

    [Fact]
    public void ForgetDropsTheBaseline()
    {
        var tally = Committed((12, 3));
        tally.Forget();
        Assert.False(tally.HasBaseline);
        Assert.Equal(0, tally.Committed(12));
    }

    [Fact]
    public void DiffClearsTheOutputList()
    {
        var tally = Committed((12, 1));
        var changes = new List<KeyValuePair<int, int>> { new(1, 1) };
        tally.Begin();
        tally.Add(12, 1);
        tally.Diff(changes);
        Assert.Empty(changes);
    }
}
