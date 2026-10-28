using ObenFramework.Items;

namespace ObenCore.Tests.Items;

public class TakeMathTests
{
    [Theory]
    [InlineData(3, 10, 7, TakeResult.Exact, 3)]
    [InlineData(3, 10, 8, TakeResult.Short, 2)]
    [InlineData(3, 10, 10, TakeResult.Nothing, 0)]
    [InlineData(3, 10, 6, TakeResult.Over, 4)]
    [InlineData(3, 10, 12, TakeResult.Nothing, -2)]
    [InlineData(1, 1, 0, TakeResult.Exact, 1)]
    internal void AccountsForWhatTheGameRemoved(int requested, int before, int after, TakeResult expected, int expectedRemoved)
    {
        Assert.Equal(expected, TakeMath.Account(requested, before, after, out int removed));
        Assert.Equal(expectedRemoved, removed);
    }

    [Fact]
    public void ReadsTheLiquidCategory()
    {
        Assert.False(TakeMath.IsLiquid(null));
        Assert.False(TakeMath.IsLiquid(Array.Empty<string>()));
        Assert.False(TakeMath.IsLiquid(new[] { "Food" }));
        Assert.True(TakeMath.IsLiquid(new[] { "Liquid" }));
        Assert.True(TakeMath.IsLiquid(new[] { "Food", "Liquid", "Drink" }));
    }
}
