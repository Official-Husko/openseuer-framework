using ObenUIKit.Menu;

namespace ObenCore.Tests.Kit;

public class MainMenuSlotsTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, 0, 2)]
    [InlineData(3, 1, 0)]
    [InlineData(7, 2, 1)]
    public void ButtonsFillRowsOfThree(int index, int row, int column)
    {
        Assert.Equal((row, column), MainMenuSlots.Place(index));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void RowsRoundUp(int count, int rows)
    {
        Assert.Equal(rows, MainMenuSlots.Rows(count));
    }

    [Fact]
    public void DownFromTheGridLandsInTheSameColumn()
    {
        Assert.Equal(1, MainMenuSlots.FromGrid(1, 3));
        Assert.Equal(2, MainMenuSlots.FromGrid(2, 5));
    }

    [Fact]
    public void DownFromTheGridOverAShortRowTakesItsLastButton()
    {
        Assert.Equal(0, MainMenuSlots.FromGrid(2, 1));
        Assert.Equal(1, MainMenuSlots.FromGrid(2, 2));
    }

    [Fact]
    public void NoModButtonsMeansNowhereToGo()
    {
        Assert.Equal(-1, MainMenuSlots.FromGrid(0, 0));
        Assert.Equal(-1, MainMenuSlots.Move(0, 0, 0, 1));
    }

    [Fact]
    public void UpFromTheFirstRowGoesBackIntoTheGrid()
    {
        Assert.Equal(-2, MainMenuSlots.Move(1, 3, 0, -1));
        Assert.Equal(1, MainMenuSlots.GridColumnAbove(1));
    }

    [Fact]
    public void UpAndDownMoveBetweenModRows()
    {
        Assert.Equal(4, MainMenuSlots.Move(1, 6, 0, 1));
        Assert.Equal(1, MainMenuSlots.Move(4, 6, 0, -1));
    }

    [Fact]
    public void DownIntoAShortRowTakesItsLastButton()
    {
        Assert.Equal(3, MainMenuSlots.Move(2, 4, 0, 1));
    }

    [Fact]
    public void DownFromTheLastRowStays()
    {
        Assert.Equal(-1, MainMenuSlots.Move(4, 5, 0, 1));
    }

    [Fact]
    public void LeftAndRightStayInTheRow()
    {
        Assert.Equal(1, MainMenuSlots.Move(0, 6, 1, 0));
        Assert.Equal(-1, MainMenuSlots.Move(0, 6, -1, 0));
        Assert.Equal(-1, MainMenuSlots.Move(2, 6, 1, 0));
        Assert.Equal(-1, MainMenuSlots.Move(3, 4, 1, 0));
    }
}
