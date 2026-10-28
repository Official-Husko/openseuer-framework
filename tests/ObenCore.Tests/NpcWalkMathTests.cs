using ObenFramework.Npcs;

namespace ObenCore.Tests;

public class NpcWalkMathTests
{
    [Theory]
    [InlineData(0f, 0f, 10f)]
    [InlineData(10f, 0f, 70f)]
    [InlineData(9f, 3f, 19f)]
    public void TimeoutsGrowWithTheWalk(float distance, float speed, float expected) =>
        Assert.Equal(expected, NpcWalkMath.Timeout(distance, speed), 3);

    [Fact]
    public void DiscPointsStayInTheRadius()
    {
        var random = new Random(7);
        for (int i = 0; i < 10000; i++)
        {
            NpcWalkMath.DiscPoint(random, 4f, out float dx, out float dz);
            Assert.True(dx * dx + dz * dz <= 16f * (1f + 1e-5f));
        }
    }

    [Fact]
    public void PatrolsGoRound()
    {
        Assert.Equal(1, NpcWalkMath.NextPatrolIndex(0, 3));
        Assert.Equal(0, NpcWalkMath.NextPatrolIndex(2, 3));
        Assert.Equal(0, NpcWalkMath.NextPatrolIndex(0, 0));
    }

    [Fact]
    public void WaitsStayBetweenFourAndFifteenSeconds()
    {
        var random = new Random(11);
        for (int i = 0; i < 10000; i++)
        {
            float wait = NpcWalkMath.WaitSeconds(random);
            Assert.InRange(wait, 4f, 15f);
        }
    }

    [Fact]
    public void OriginAndFiniteChecks()
    {
        Assert.True(NpcWalkMath.IsOrigin(0f, 0f, 0f));
        Assert.False(NpcWalkMath.IsOrigin(0f, 0.1f, 0f));
        Assert.True(NpcWalkMath.Finite(1.5f));
        Assert.False(NpcWalkMath.Finite(float.NaN));
        Assert.False(NpcWalkMath.Finite(float.PositiveInfinity));
    }
}
