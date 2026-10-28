using ObenFramework.Env;

namespace ObenCore.Tests.Env;

public class SkyChannelTests
{
    private static SkyChannel<float> FloatChannel() => new(t => ((float[])t)[0], (t, v) => ((float[])t)[0] = v);

    private static SkyChannel<bool> BoolChannel() => new(t => ((bool[])t)[0], (t, v) => ((bool[])t)[0] = v);

    // What a hooked game writer does: our value is taken out, the game writes, the result is read back.
    private static void GameWrites(SkyChannel<bool> channel, bool[] cell, bool value)
    {
        channel.Suspend();
        cell[0] = value;
        channel.Capture();
    }

    [Fact]
    public void WritesTheOverrideAndRestoresTheGameValue()
    {
        var cell = new[] { 1f };
        var channel = FloatChannel();
        channel.Apply(cell, 5f);
        Assert.Equal(5f, cell[0]);
        Assert.True(channel.Held);
        channel.Apply(cell, 6f);
        Assert.Equal(6f, cell[0]);
        channel.Apply(cell, null);
        Assert.Equal(1f, cell[0]);
        Assert.False(channel.Held);
    }

    [Fact]
    public void AGameWriteBecomesTheValueUnderneath()
    {
        var cell = new[] { 1f };
        var channel = FloatChannel();
        channel.Apply(cell, 6f);
        cell[0] = 2f;
        channel.Apply(cell, 6f);
        Assert.Equal(6f, cell[0]);
        channel.Apply(cell, null);
        Assert.Equal(2f, cell[0]);
    }

    [Fact]
    public void ReleaseDoesNotOverwriteANewerGameValue()
    {
        var cell = new[] { 1f };
        var channel = FloatChannel();
        channel.Apply(cell, 6f);
        cell[0] = 3f;
        channel.Apply(cell, null);
        Assert.Equal(3f, cell[0]);
        Assert.False(channel.Held);
    }

    [Fact]
    public void ForgetLetsGoWithoutWriting()
    {
        var cell = new[] { 1f };
        var channel = FloatChannel();
        channel.Apply(cell, 8f);
        channel.Forget();
        channel.Apply(cell, null);
        Assert.Equal(8f, cell[0]);
    }

    [Fact]
    public void ANewTargetDropsTheOldOneWithoutRestoringIt()
    {
        var cell = new[] { 1f };
        var other = new[] { 1f };
        var channel = FloatChannel();
        channel.Apply(cell, 9f);
        channel.Apply(other, 9f);
        channel.Apply(other, null);
        Assert.Equal(9f, cell[0]);
        Assert.Equal(1f, other[0]);
    }

    [Fact]
    public void AnIdleReleaseWritesNothing()
    {
        var cell = new[] { 1f };
        FloatChannel().Apply(cell, null);
        Assert.Equal(1f, cell[0]);
    }

    [Fact]
    public void AGameWriteEqualToTheOverrideIsKeptOnRelease()
    {
        var fog = new[] { true };
        var channel = BoolChannel();
        channel.Apply(fog, false);
        GameWrites(channel, fog, false);
        channel.Apply(fog, false);
        channel.Apply(fog, null);
        Assert.False(fog[0]);

        fog[0] = false;
        channel = BoolChannel();
        channel.Apply(fog, true);
        GameWrites(channel, fog, true);
        channel.Apply(fog, true);
        channel.Apply(fog, null);
        Assert.True(fog[0]);
    }

    [Fact]
    public void SuspendShowsTheGameItsOwnValue()
    {
        var fog = new[] { true };
        var channel = BoolChannel();
        channel.Apply(fog, false);
        channel.Suspend();
        Assert.True(fog[0]);
        channel.Capture();
        channel.Apply(fog, false);
        channel.Apply(fog, null);
        Assert.True(fog[0]);
    }

    [Fact]
    public void SuspendLeavesAForeignValueAlone()
    {
        var fog = new[] { false };
        var channel = BoolChannel();
        channel.Apply(fog, true);
        fog[0] = false;
        channel.Suspend();
        channel.Capture();
        channel.Apply(fog, true);
        channel.Apply(fog, null);
        Assert.False(fog[0]);
    }
}
