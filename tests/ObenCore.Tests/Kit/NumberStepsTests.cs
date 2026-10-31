using ObenUIKit.Menu.Settings;

namespace ObenCore.Tests.Kit;

public class NumberStepsTests
{
    [Fact]
    public void AFloatOnTheLadderIsNotAddedTwice()
    {
        double[] values = NumberSteps.Around((float)0.15, whole: false, out int index);
        Assert.Single(values, v => NumberSteps.Labels(new[] { v }, false)[0] == "0.15");
        Assert.Equal(0.15, values[index], 9);
    }

    [Fact]
    public void AValueOffTheLadderIsAddedInOrder()
    {
        double[] values = NumberSteps.Around(0.33, whole: false, out int index);
        Assert.Equal(0.33, values[index]);
        Assert.True(values[index - 1] < 0.33 && values[index + 1] > 0.33);
    }

    [Fact]
    public void NegativeValuesGetANegativeLadder()
    {
        double[] values = NumberSteps.Around(-3, whole: true, out int index);
        Assert.Equal(-3, values[index]);
        Assert.Contains(-100, values);
    }
}
