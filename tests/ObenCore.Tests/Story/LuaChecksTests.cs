using System;
using ObenFramework.Dialogue;

namespace ObenCore.Tests.Story;

public class LuaChecksTests
{
    [Theory]
    [InlineData("Day")]
    [InlineData("PlayerFirstName")]
    [InlineData("Converrsant")]
    [InlineData("DREAM_1")]
    public void RefusesTheGamesOwnVariables(string name)
    {
        Assert.True(LuaChecks.IsGameVariable(name));
        var e = Assert.Throws<ArgumentException>(() => LuaChecks.VariableName(name, "name"));
        Assert.Contains(name, e.Message);
    }

    [Theory]
    [InlineData("MyMod_Day")]
    [InlineData("day")]
    [InlineData("Dream_1")]
    public void AcceptsOtherNames(string name)
    {
        Assert.False(LuaChecks.IsGameVariable(name));
        LuaChecks.VariableName(name, "name");
    }

    [Fact]
    public void StillChecksTheNameItself() =>
        Assert.Throws<ArgumentException>(() => LuaChecks.VariableName("My\\Var", "name"));

    [Fact]
    public void NullIsNotAGameVariable() => Assert.False(LuaChecks.IsGameVariable(null));
}
