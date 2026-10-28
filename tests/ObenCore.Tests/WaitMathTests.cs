using ObenFramework.Player;

namespace ObenCore.Tests;

public class WaitMathTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(59, 0)]
    [InlineData(60, 1)]
    [InlineData(3599, 59)]
    [InlineData(-30, 0)]
    public void MinutesPassedRoundsDown(int seconds, int minutes) => Assert.Equal(minutes, WaitMath.MinutesPassed(seconds));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(5, 1440, true)]
    [InlineData(-1, 5, false)]
    [InlineData(6, 5, false)]
    [InlineData(0, 1441, false)]
    public void ValidRange(int min, int max, bool valid) => Assert.Equal(valid, WaitMath.ValidRange(min, max));

    [Theory]
    [InlineData(-3, 0, 10, 0)]
    [InlineData(4, 5, 10, 5)]
    [InlineData(11, 5, 10, 10)]
    [InlineData(7, 5, 10, 7)]
    public void Clamp(int minutes, int min, int max, int expected) => Assert.Equal(expected, WaitMath.Clamp(minutes, min, max));

    [Theory]
    [InlineData(1830f, 31)]
    [InlineData(1800f, 30)]
    [InlineData(1769f, 29)]
    [InlineData(0f, 0)]
    public void SliderSecondsRoundToTheNearestMinute(float seconds, int minutes) =>
        Assert.Equal(minutes, WaitMath.MinutesFromSlider(seconds));

    [Theory]
    [InlineData(31, 0, 120, 30, 30)]
    [InlineData(46, 0, 120, 30, 60)]
    [InlineData(31, 5, 120, 1, 31)]
    [InlineData(500, 0, 120, 30, 120)]
    [InlineData(3, 5, 120, 10, 5)]
    [InlineData(100, 0, 100, 60, 60)]
    [InlineData(-5, 0, 100, 60, 0)]
    [InlineData(45, 0, 100, 60, 60)]
    [InlineData(12, 0, 60, 0, 12)]
    public void SnapStaysOnTheStepGrid(int minutes, int min, int max, int step, int expected) =>
        Assert.Equal(expected, WaitMath.Snap(minutes, min, max, step));

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(75, "01:15")]
    [InlineData(1440, "24:00")]
    public void ClockText(int minutes, string text) => Assert.Equal(text, WaitMath.Clock(minutes));
}
