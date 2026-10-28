using ObenFramework.NewGames;

namespace ObenCore.Tests.NewGames;

public class NewGameOptionRulesTests
{
    private const string Mod = "ExampleMod";
    private const NewGamePlacement Below = NewGamePlacement.BelowDifficulty;

    [Fact]
    public void ToggleStoresOneOrZero()
    {
        var option = NewGameOptionRules.Toggle(Mod, "loud", "Loud", null, true, Below);
        Assert.Equal("1", option.Default);
        Assert.Equal("0", option.Normalize("0"));
        Assert.Equal("1", option.Normalize("yes"));
        Assert.Equal("Enabled", option.Display("1"));
        Assert.Equal("Disabled", option.Display("0"));
        Assert.Equal("", option.Description);
    }

    [Theory]
    [InlineData("bad id!", "key", "Title")]
    [InlineData(Mod, "", "Title")]
    [InlineData(Mod, "has.dot", "Title")]
    [InlineData(Mod, "key", " ")]
    public void RefusesBadNames(string modId, string key, string title)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Toggle(modId, key, title, null, false, Below));
    }

    [Fact]
    public void RefusesAnUnknownPlacement()
    {
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Toggle(Mod, "key", "Title", null, false, (NewGamePlacement)7));
    }

    [Fact]
    public void ChoiceChecksItsLists()
    {
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Choice(Mod, "k", "T", null, null, new[] { "A" }, 0, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Choice(Mod, "k", "T", null, new[] { "a" }, null, 0, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Choice(Mod, "k", "T", null, new string[0], new string[0], 0, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Choice(Mod, "k", "T", null, new[] { "" }, new[] { "A" }, 0, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Choice(Mod, "k", "T", null, new[] { "a", "b" }, new[] { "A" }, 0, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Choice(Mod, "k", "T", null, new[] { "a", "a" }, new[] { "A", "B" }, 0, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Choice(Mod, "k", "T", null, new[] { "a" }, new[] { "A" }, 1, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Choice(Mod, "k", "T", null, new[] { "a" }, new[] { "A" }, -1, Below));
    }

    [Fact]
    public void ChoiceStoresIdsAndShowsTitles()
    {
        var option = NewGameOptionRules.Choice(Mod, "mode", "Mode", null, new[] { "low", "mid", "high" }, new[] { "Low", "Mid", "High" }, 1, Below);
        Assert.Equal("mid", option.Default);
        Assert.Equal("High", option.Display("high"));
        Assert.Equal("mid", option.Normalize("gone"));
        Assert.Equal("high", NewGameOptionRules.Step(option, "mid", true));
        Assert.Equal("low", NewGameOptionRules.Step(option, "high", true));
        Assert.Equal("high", NewGameOptionRules.Step(option, "low", false));
    }

    [Fact]
    public void IntegerKeepsToItsRange()
    {
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Integer(Mod, "k", "T", null, 5, 2, 3, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Integer(Mod, "k", "T", null, 1, 10, 50, Below));
        var option = NewGameOptionRules.Integer(Mod, "count", "Count", null, 1, 10, 3, Below);
        Assert.Equal("3", option.Default);
        Assert.Equal("10", option.Normalize("99"));
        Assert.Equal("3", option.Normalize("many"));
        Assert.Equal("1", NewGameOptionRules.Step(option, "10", true));
        Assert.Equal("10", NewGameOptionRules.Step(option, "1", false));
    }

    [Fact]
    public void FloatSitsOnItsSteps()
    {
        var option = NewGameOptionRules.Float(Mod, "rate", "Rate", null, 0.5f, 2.5f, 0.5f, 1.5f, Below);
        Assert.Equal("1.5", option.Default);
        Assert.Equal(4, option.FloatSteps);
        Assert.Equal(0.5f, NewGameOptionRules.FloatAt(option, -1));
        Assert.Equal(2.5f, NewGameOptionRules.FloatAt(option, 9));
        Assert.Equal(2, NewGameOptionRules.FloatIndex(option, 1.6f));
        Assert.Equal("1.5", option.Normalize("1.6"));
        Assert.Equal("2.5", option.Normalize("7"));
        Assert.Equal("2", NewGameOptionRules.Step(option, "1.5", true));
        Assert.Equal("0.5", NewGameOptionRules.Step(option, "2.5", true));
    }

    [Fact]
    public void FloatRefusesBadRanges()
    {
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Float(Mod, "k", "T", null, 0.5f, 2.5f, 0.5f, 1.6f, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Float(Mod, "k", "T", null, 0.5f, 2.5f, 0.5f, 3f, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Float(Mod, "k", "T", null, 1f, 1f, 0.5f, 1f, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Float(Mod, "k", "T", null, 0.5f, 2.5f, 0f, 1.5f, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Float(Mod, "k", "T", null, 0f, 1f, 0.5f, float.NaN, Below));
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Float(Mod, "k", "T", null, 0f, 1001f, 1f, 0f, Below));
    }

    [Fact]
    public void FloatAcceptsAGridAtALargeMin()
    {
        var option = NewGameOptionRules.Float(Mod, "big", "Big", null, 1000f, 1100f, 0.1f, 1050.3f, Below);
        Assert.Equal(1000, option.FloatSteps);
        Assert.ThrowsAny<ArgumentException>(() => NewGameOptionRules.Float(Mod, "k", "T", null, 1000f, 1100f, 0.1f, 1050.35f, Below));
    }
}

public class NewGameOptionStoreTests
{
    private static NewGameOption Count(string mod = "ExampleMod") =>
        NewGameOptionRules.Integer(mod, "count", "Count", null, 1, 10, 3, NewGamePlacement.BelowDifficulty);

    [Fact]
    public void KeepsTheFirstOfTwoEqualKeys()
    {
        var store = new NewGameOptionStore();
        var first = Count();
        Assert.True(store.Add(first));
        Assert.False(store.Add(Count()));
        Assert.True(store.Add(Count("OtherMod")));
        Assert.Same(first, store.Find("ExampleMod", "count"));
        Assert.Equal(1, store.CountFor("OtherMod"));
        Assert.Equal("OtherMod", store.All()[1].ModId);
    }

    [Fact]
    public void DraftFallsBackToTheDefault()
    {
        var store = new NewGameOptionStore();
        store.Add(Count());
        Assert.Null(store.Draft("ExampleMod", "count"));
        Assert.Equal("3", store.DraftOrDefault("ExampleMod", "count"));
        store.Choose("ExampleMod", "count", "70");
        Assert.Equal("10", store.DraftOrDefault("ExampleMod", "count"));
        Assert.Null(store.DraftOrDefault("ExampleMod", "missing"));
        Assert.ThrowsAny<ArgumentException>(() => store.Choose("ExampleMod", "missing", "1"));
    }

    [Fact]
    public void CaptureStoresEveryOptionPickedOrDefault()
    {
        var store = new NewGameOptionStore();
        store.Add(NewGameOptionRules.Toggle("Zed", "on", "On", null, true, NewGamePlacement.CustomPanel));
        store.Add(Count());
        store.Choose("ExampleMod", "count", "7");
        var file = store.Capture();
        Assert.Equal(2, file.Options.Count);
        Assert.Equal("ExampleMod", file.Options[0].ModId);
        Assert.Equal("7", file.Get("ExampleMod", "count"));
        Assert.Equal("1", file.Get("Zed", "on"));
    }

    [Fact]
    public void RemovingAnOptionKeepsItsDraft()
    {
        var store = new NewGameOptionStore();
        var option = Count();
        store.Add(option);
        store.Choose("ExampleMod", "count", "5");
        Assert.True(store.Remove(option));
        Assert.Null(store.Find("ExampleMod", "count"));
        store.Add(Count());
        Assert.Equal("5", store.DraftOrDefault("ExampleMod", "count"));
        store.ClearDraft();
        Assert.Equal("3", store.DraftOrDefault("ExampleMod", "count"));
    }

    [Fact]
    public void FileCopiesOnlyCompleteEntries()
    {
        var source = new NewGameOptionsFile { DifficultyPreset = "ExampleMod.harsh" };
        source.Set("A", "x", "1");
        source.Options.Add(new NewGameOptionEntry { ModId = "B", Key = null, Value = "2" });
        source.Options.Add(null);
        var copy = new NewGameOptionsFile();
        copy.CopyOptionsFrom(source);
        Assert.Single(copy.Options);
        Assert.Equal("", copy.DifficultyPreset);
        copy.Set("A", "x", "0");
        Assert.Equal("0", copy.Get("A", "x"));
        Assert.Equal("1", source.Get("A", "x"));
        Assert.Null(copy.Get("A", "y"));
    }
}
