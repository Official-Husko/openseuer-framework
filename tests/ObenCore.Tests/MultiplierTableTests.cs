using ObenFramework.Player;

namespace ObenCore.Tests;

public class MultiplierTableTests
{
    private readonly MultiplierTable _table = new(3, 10f);

    [Fact]
    public void StartsAtOneOnEveryChannel()
    {
        Assert.True(_table.IsEmpty);
        for (int i = 0; i < _table.Channels; i++)
            Assert.Equal(1f, _table.Product(i));
    }

    [Fact]
    public void OwnersMultiplyTogether()
    {
        _table.Set("a", 0, null, 2f);
        _table.Set("b", 0, null, 1.5f);
        Assert.Equal(3f, _table.Product(0));
        Assert.Equal(1f, _table.Product(1));
    }

    [Fact]
    public void SameOwnerAndKeyReplaces()
    {
        _table.Set("a", 0, null, 2f);
        _table.Set("a", 0, "", 4f);
        Assert.Single(_table.Entries);
        Assert.Equal(4f, _table.Product(0));
    }

    [Fact]
    public void KeysKeepSeveralFromOneOwner()
    {
        _table.Set("a", 0, "boots", 2f);
        _table.Set("a", 0, "potion", 2f);
        Assert.Equal(4f, _table.Product(0));
        Assert.True(_table.Remove("a", 0, "boots"));
        Assert.Equal(2f, _table.Product(0));
    }

    [Fact]
    public void RemovingOneOwnerKeepsTheOther()
    {
        _table.Set("a", 1, null, 0.5f);
        _table.Set("b", 1, null, 3f);
        Assert.True(_table.Remove("a", 1, null));
        Assert.Equal(3f, _table.Product(1));
        Assert.False(_table.Remove("a", 1, null));
    }

    [Fact]
    public void RemovingAnEntryOfOneStillCounts()
    {
        _table.Set("a", 0, null, 1f);
        Assert.True(_table.Remove("a", 0, null));
        Assert.True(_table.IsEmpty);
    }

    [Fact]
    public void RemoveOwnerClearsEveryChannel()
    {
        _table.Set("a", 0, null, 2f);
        _table.Set("a", 2, "x", 3f);
        _table.Set("b", 2, null, 2f);
        Assert.Equal(2, _table.RemoveOwner("a"));
        Assert.Equal(1f, _table.Product(0));
        Assert.Equal(2f, _table.Product(2));
    }

    [Fact]
    public void ProductIsCappedAtMax()
    {
        _table.Set("a", 0, null, 5f);
        _table.Set("b", 0, null, 5f);
        Assert.Equal(10f, _table.Product(0));
    }

    [Fact]
    public void ZeroStopsTheChannel()
    {
        _table.Set("a", 0, null, 0f);
        _table.Set("b", 0, null, 8f);
        Assert.Equal(0f, _table.Product(0));
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(10.5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void RejectsValuesOutsideTheRange(float value)
    {
        Assert.False(_table.Accepts(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => _table.Set("a", 0, null, value));
    }

    [Fact]
    public void RejectsUnknownChannelsAndOwners()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _table.Set("a", 3, null, 1f));
        Assert.Throws<ArgumentException>(() => _table.Set("", 0, null, 1f));
    }

    [Fact]
    public void SetReportsWhetherTheProductChanged()
    {
        Assert.True(_table.Set("a", 0, null, 2f));
        Assert.False(_table.Set("a", 0, null, 2f));
        Assert.False(_table.Set("b", 0, null, 1f));
    }

    [Fact]
    public void ClearResetsEverything()
    {
        _table.Set("a", 0, null, 2f);
        _table.Clear();
        Assert.True(_table.IsEmpty);
        Assert.Equal(1f, _table.Product(0));
    }
}
