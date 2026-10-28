using ObenFramework.NewGames;

namespace ObenCore.Tests.NewGames;

public class DifficultyPresetMapTests
{
    // The game's DefaultMultiplier values in enum order, without Infinite.
    private static readonly float[] GameOrder = { 1f, 0f, 0.25f, 0.5f, 0.75f, 1.25f, 1.5f, 1.75f, 2f, 2.25f, 2.5f, 2.75f, 3f, 3.25f, 3.5f, 3.75f, 4f };

    private static float[] SortedSteps()
    {
        int[] order = DifficultyPresetMap.OrderByValue(GameOrder);
        return Array.ConvertAll(order, i => GameOrder[i]);
    }

    [Fact]
    public void FindsLevels()
    {
        int[] levels = { 0, 1, 6 };   // the list 0.5.13 has: Default, Casual, Custom
        Assert.Equal(0, DifficultyPresetMap.IndexOf(levels, 0));
        Assert.Equal(2, DifficultyPresetMap.IndexOf(levels, 6));
        Assert.Equal(-1, DifficultyPresetMap.IndexOf(levels, 3));
        Assert.Equal(-1, DifficultyPresetMap.IndexOf(null, 0));
    }

    [Fact]
    public void SortsTheGameStepsByValue()
    {
        int[] expected = { 1, 2, 3, 4, 0, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        Assert.Equal(expected, DifficultyPresetMap.OrderByValue(GameOrder));
        Assert.Equal(new[] { 1, 2, 0 }, DifficultyPresetMap.OrderByValue(new[] { 2f, 1f, 1f }));
        Assert.Empty(DifficultyPresetMap.OrderByValue(null));
    }

    [Theory]
    [InlineData(1f, 4)]
    [InlineData(0f, 0)]
    [InlineData(4f, 16)]
    [InlineData(1.6f, 6)]
    [InlineData(1.625f, 6)]
    [InlineData(-9f, 0)]
    [InlineData(9f, 16)]
    [InlineData(float.NaN, 0)]
    public void SnapsToTheNearestStep(float value, int step)
    {
        Assert.Equal(step, DifficultyPresetMap.Nearest(value, SortedSteps()));
    }

    [Fact]
    public void NearestWithoutStepsIsNothing()
    {
        Assert.Equal(-1, DifficultyPresetMap.Nearest(1f, null));
        Assert.Equal(-1, DifficultyPresetMap.Nearest(1f, Array.Empty<float>()));
    }

    [Theory]
    [InlineData(2, 3, 2, 2)]
    [InlineData(4, 3, 2, 2)]
    [InlineData(99, 3, -1, 2)]
    [InlineData(-4, 3, -1, 0)]
    [InlineData(-4, 3, 1, 1)]
    [InlineData(9, 3, 9, 2)]
    [InlineData(2, 0, 0, 0)]
    public void ClampsAnIndexIntoTheList(int index, int count, int preferred, int expected)
    {
        Assert.Equal(expected, DifficultyPresetMap.Clamp(index, count, preferred));
    }

    [Fact]
    public void KnowsWhichIndexesAreModPresets()
    {
        Assert.True(DifficultyPresetMap.IsListed(3, 3, 2));
        Assert.True(DifficultyPresetMap.IsListed(4, 3, 2));
        Assert.False(DifficultyPresetMap.IsListed(5, 3, 2));
        Assert.False(DifficultyPresetMap.IsListed(2, 3, 2));
        Assert.False(DifficultyPresetMap.IsListed(3, 3, 0));
    }
}
