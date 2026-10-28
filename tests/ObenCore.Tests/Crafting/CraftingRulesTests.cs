using ObenFramework.Crafting;

namespace ObenCore.Tests.Crafting;

public class CraftingRulesTests
{
    [Theory]
    [InlineData(1f, 1.2f, 1.2f)]
    [InlineData(1.8f, 2f, 2f)]
    [InlineData(0.1f, 0.1f, 0.05f)]
    [InlineData(0.7f, 1f, 0.7f)]
    public void ScalesQualityWithinTheGamesRange(float quality, float factor, float expected)
    {
        Assert.Equal(expected, QualityMath.Scale(quality, factor, 0.05f, 2f), 4);
    }

    [Fact]
    public void LeavesTheGamesTimeFactorAloneAtNormalSpeed()
    {
        Assert.Equal(0.8f, QualityMath.TimeFactor(0.8f, 1f, 600));
        Assert.Equal(0.8f, QualityMath.TimeFactor(0.8f, 0f, 600));
        Assert.Equal(0.8f, QualityMath.TimeFactor(0.8f, float.NaN, 600));
    }

    [Fact]
    public void DividesTheTimeFactorBySpeed()
    {
        Assert.Equal(0.5f, QualityMath.TimeFactor(1f, 2f, 600), 4);
        Assert.Equal(4f, QualityMath.TimeFactor(1f, 0.25f, 600), 4);
    }

    [Fact]
    public void CapsTheTimeSoTheGamesTimerHoldsIt()
    {
        float factor = QualityMath.TimeFactor(1f, 0.01f, 1_000_000_000);
        Assert.True(factor * 1_000_000_000.0 <= QualityMath.MaxProcessSeconds + 1);
    }

    [Theory]
    [InlineData("Turnip Mash", "MASH", true)]
    [InlineData("Turnip Mash", "", true)]
    [InlineData("Turnip Mash", null, true)]
    [InlineData("Turnip Mash", "soup", false)]
    [InlineData(null, "mash", false)]
    [InlineData(null, null, true)]
    public void SearchesNamesIgnoringCase(string? name, string? text, bool expected)
    {
        Assert.Equal(expected, RecipeText.Contains(name, text));
    }

    [Fact]
    public void KeepsOnlyWhatEveryFilterKeeps()
    {
        var filters = new List<Func<int, bool>> { n => n > 2, n => n % 2 == 0 };
        Assert.True(RecipeText.KeepAll(filters, 4, null));
        Assert.False(RecipeText.KeepAll(filters, 3, null));
        Assert.False(RecipeText.KeepAll(filters, 2, null));
        Assert.True(RecipeText.KeepAll(new List<Func<int, bool>>(), 1, null));
    }

    [Fact]
    public void AThrowingFilterKeepsTheItemAndIsReported()
    {
        var failures = new List<Exception>();
        var filters = new List<Func<int, bool>> { _ => throw new InvalidOperationException("broken"), n => n > 0 };
        Assert.True(RecipeText.KeepAll(filters, 1, failures.Add));
        Assert.False(RecipeText.KeepAll(filters, 0, failures.Add));
        Assert.Equal(2, failures.Count);
    }

    [Fact]
    public void OrdersSourcesByPriorityThenRegistration()
    {
        var items = new List<(string Name, int Priority, int Order)>
        {
            ("low-first", 0, 0), ("high", 5, 1), ("low-second", 0, 2), ("negative", -1, 3), ("high-later", 5, 4),
        };
        SourceOrder.Sort(items, i => i.Priority, i => i.Order);
        Assert.Equal(new[] { "high", "high-later", "low-first", "low-second", "negative" }, items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public void SortingIsStableForEqualEntries()
    {
        var items = Enumerable.Range(0, 20).Select(i => (Priority: i % 3, Order: i)).Reverse().ToList();
        SourceOrder.Sort(items, i => i.Priority, i => i.Order);
        for (int i = 1; i < items.Count; i++)
        {
            Assert.True(items[i - 1].Priority > items[i].Priority
                || (items[i - 1].Priority == items[i].Priority && items[i - 1].Order < items[i].Order));
        }
    }
}
