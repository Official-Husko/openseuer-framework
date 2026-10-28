using ObenFramework.NewGames;

namespace ObenCore.Tests.NewGames;

public class StartChoiceRulesTests
{
    [Fact]
    public void TheFirstFamilyOrPartnerChoiceDecides()
    {
        Assert.Equal("Single", StartChoiceRules.Relationship(null));
        Assert.Equal("Single", StartChoiceRules.Relationship(new[] { "Alcoholic", "Unemployed" }));
        Assert.Equal("Married", StartChoiceRules.Relationship(new[] { "Alcoholic", "Family", "Girlfriend" }));
        Assert.Equal("In a relationship", StartChoiceRules.Relationship(new[] { "Boyfriend", "Family" }));
    }
}

public class TutorialStepsTests
{
    [Fact]
    public void NothingSetIsNone() => Assert.Equal(TutorialStep.None, TutorialSteps.StepOf(new TutorialFlags()));

    [Fact]
    public void TheOpeningSequenceComesFirst()
    {
        Assert.Equal(TutorialStep.LookAround, TutorialSteps.StepOf(new TutorialFlags { Look = true, Move = true, Building = true }));
        Assert.Equal(TutorialStep.Interact, TutorialSteps.StepOf(new TutorialFlags { Use = true, Tenement = true }));
        Assert.Equal(TutorialStep.Menu, TutorialSteps.StepOf(new TutorialFlags { Menu = true, Inventory = true, Map = true }));
        Assert.Equal(TutorialStep.Inventory, TutorialSteps.StepOf(new TutorialFlags { Inventory = true, Map = true, Task = true }));
        Assert.Equal(TutorialStep.Map, TutorialSteps.StepOf(new TutorialFlags { Map = true, Task = true }));
        Assert.Equal(TutorialStep.Tasks, TutorialSteps.StepOf(new TutorialFlags { Task = true, Reload = true }));
    }

    [Fact]
    public void TheTenementPromptWaitsUntilUsed()
    {
        Assert.Equal(TutorialStep.Tenement, TutorialSteps.StepOf(new TutorialFlags { Tenement = true, Building = true }));
        Assert.Equal(TutorialStep.Building, TutorialSteps.StepOf(new TutorialFlags { Tenement = true, TenementUsed = true, Building = true }));
    }

    [Fact]
    public void LaterPromptsFollowTheGameOrder()
    {
        Assert.Equal(TutorialStep.Cleaning, TutorialSteps.StepOf(new TutorialFlags { Cleaning = true, HeadLight = true }));
        Assert.Equal(TutorialStep.HeadLight, TutorialSteps.StepOf(new TutorialFlags { HeadLight = true, Reload = true }));
        Assert.Equal(TutorialStep.Reload, TutorialSteps.StepOf(new TutorialFlags { Reload = true }));
    }
}

public class OptionIdsTests
{
    [Theory]
    [InlineData("ObenFramework", true)]
    [InlineData("my.mod_1-2", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("has space", false)]
    [InlineData("ä", false)]
    public void ChecksIds(string? id, bool valid) => Assert.Equal(valid, OptionIds.IsValid(id));

    [Fact]
    public void KeysHaveNoDots()
    {
        Assert.True(OptionIds.IsValidKey("harsh-winter"));
        Assert.False(OptionIds.IsValidKey("harsh.winter"));
        Assert.False(OptionIds.IsValid(new string('a', 65)));
    }
}
