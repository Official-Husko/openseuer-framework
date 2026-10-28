using ObenFramework.Quests;

namespace ObenCore.Tests.Story;

public class QuestIdsTests
{
    [Theory]
    [InlineData("MyMod", "intro", "MyMod.intro")]
    [InlineData("pawbeans.cheats", "step-2", "pawbeans.cheats.step-2")]
    [InlineData("MyMod", "has.dot", null)]
    [InlineData("MyMod", "", null)]
    [InlineData("My Mod", "intro", null)]
    [InlineData(".MyMod", "intro", null)]
    public void BuildsTaskIds(string modId, string key, string expected) => Assert.Equal(expected, QuestIds.TaskId(modId, key));

    [Theory]
    [InlineData("MyMod.intro", "MyMod")]
    [InlineData("pawbeans.cheats.step-2", "pawbeans.cheats")]
    [InlineData("FiveDucks15", null)]
    [InlineData("MyMod.", null)]
    [InlineData(".intro", null)]
    [InlineData("My Mod.intro", null)]
    public void ReadsTheOwnerBack(string taskId, string owner) => Assert.Equal(owner, QuestIds.OwnerOf(taskId));

    [Fact]
    public void OwnershipNeedsTheWholeModId()
    {
        Assert.True(QuestIds.Owns("MyMod", "MyMod.intro"));
        Assert.False(QuestIds.Owns("My", "MyMod.intro"));
        Assert.False(QuestIds.Owns("MyMod", "MyModExtra.intro"));
        Assert.False(QuestIds.Owns("MyMod", "aziz0"));
    }

    [Theory]
    [InlineData("MyMod.intro", "", true)]
    [InlineData("MyMod.intro", null, true)]
    [InlineData("MyMod.intro", "Assets/Content/Tasks/x.asset", false)]
    [InlineData("BazaarBank33", "", false)]
    [InlineData("a..b", "", false)]
    [InlineData("a.b c", "", false)]
    public void TellsModTasksFromGameTasks(string id, string path, bool expected)
    {
        Assert.Equal(expected, QuestIds.IsModTask(id, path));
        if (string.IsNullOrEmpty(path))
            Assert.Equal(QuestIds.OwnerOf(id) != null, QuestIds.IsModTask(id, path));
    }

    [Fact]
    public void TextRules()
    {
        Assert.True(QuestIds.ValidText(null, 10, required: false, multiline: false));
        Assert.False(QuestIds.ValidText(null, 10, required: true, multiline: false));
        Assert.False(QuestIds.ValidText("   ", 10, required: true, multiline: false));
        Assert.False(QuestIds.ValidText("12345678901", 10, required: false, multiline: false));
        Assert.False(QuestIds.ValidText("a\nb", 10, required: false, multiline: false));
        Assert.True(QuestIds.ValidText("a\nb", 10, required: false, multiline: true));
        Assert.False(QuestIds.ValidText("a\tb", 10, required: false, multiline: true));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(50, 3, true)]
    [InlineData(50, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(-1, 0, false)]
    [InlineData(50, 10000, false)]
    public void ItemRules(int item, int count, bool expected) => Assert.Equal(expected, QuestIds.ValidItem(item, count));

    [Theory]
    [InlineData("before-boss", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("under_score", false)]
    [InlineData("123456789012345678901234567890123", false)]
    public void CheckpointNames(string name, bool expected) => Assert.Equal(expected, QuestIds.ValidCheckpointName(name));
}

public class QuestPlanTests
{
    private static QuestDefinition Simple(string key = "intro") => new QuestDefinition
    {
        Key = key,
        Title = "A small favour",
        Objectives = { new QuestObjectiveDefinition { Key = "talk", Title = "Talk to Aziz" } },
    };

    [Fact]
    public void AcceptsASimpleTask() => Assert.Null(QuestPlan.Check("MyMod", Simple()));

    [Fact]
    public void RefusesBadModIdsAndKeys()
    {
        Assert.NotNull(QuestPlan.Check("My Mod", Simple()));
        Assert.NotNull(QuestPlan.Check("MyMod", Simple("bad.key")));
        Assert.NotNull(QuestPlan.Check("MyMod", null));
    }

    [Fact]
    public void NeedsOneToTwelveObjectivesWithUniqueKeys()
    {
        var none = Simple();
        none.Objectives.Clear();
        Assert.NotNull(QuestPlan.Check("MyMod", none));

        var twin = Simple();
        twin.Objectives.Add(new QuestObjectiveDefinition { Key = "talk", Title = "Again" });
        Assert.Contains("talk", QuestPlan.Check("MyMod", twin));

        var many = Simple();
        for (int i = 0; i < 12; i++)
            many.Objectives.Add(new QuestObjectiveDefinition { Key = "o" + i, Title = "Step " + i });
        Assert.NotNull(QuestPlan.Check("MyMod", many));
    }

    [Fact]
    public void HiddenObjectivesMayLeaveTheTitleOut()
    {
        var task = Simple();
        task.Objectives.Add(new QuestObjectiveDefinition { Key = "secret", Hidden = true });
        Assert.Null(QuestPlan.Check("MyMod", task));
        task.Objectives[1].Hidden = false;
        Assert.NotNull(QuestPlan.Check("MyMod", task));
    }

    [Fact]
    public void CountersAndItemsHaveLimits()
    {
        Assert.NotNull(QuestPlan.CheckObjective(new QuestObjectiveDefinition { Key = "a", Title = "A", Target = 10000 }));
        Assert.NotNull(QuestPlan.CheckObjective(new QuestObjectiveDefinition { Key = "a", Title = "A", ItemId = 5 }));
        Assert.Null(QuestPlan.CheckObjective(new QuestObjectiveDefinition { Key = "a", Title = "A", ItemId = 5, Count = 2, Target = 2 }));
    }

    [Fact]
    public void ChecksTheFollowUpChain()
    {
        var first = Simple("one");
        first.NextDefinition = Simple("two");
        first.NextDefinition.Title = "";
        Assert.Contains("follow-up 'two'", QuestPlan.Check("MyMod", first));

        first.NextDefinition.Title = "Second";
        Assert.Null(QuestPlan.Check("MyMod", first));

        first.Next = "three";
        Assert.NotNull(QuestPlan.Check("MyMod", first));
    }

    [Fact]
    public void AChainOfNineIsTooLong()
    {
        var head = Simple("t0");
        var current = head;
        for (int i = 1; i <= QuestPlan.MaxChain; i++)
        {
            current.NextDefinition = Simple("t" + i);
            current = current.NextDefinition;
        }
        Assert.Contains("longer", QuestPlan.Check("MyMod", head));
    }

    [Fact]
    public void ALoopingChainEndsWhereItComesBack()
    {
        var a = Simple("a");
        var b = Simple("b");
        b.Title = "B";
        a.NextDefinition = b;
        b.NextDefinition = a;
        Assert.Null(QuestPlan.Check("MyMod", a));
    }

    [Fact]
    public void RevealRules()
    {
        Assert.True(QuestPlan.CanReveal(QuestPlan.Hidden));
        Assert.False(QuestPlan.CanReveal(QuestPlan.Active));
        Assert.True(QuestPlan.ShouldFinishOnReveal(3, 3, false));
        Assert.False(QuestPlan.ShouldFinishOnReveal(3, 3, true));
        Assert.False(QuestPlan.ShouldFinishOnReveal(2, 3, false));
        Assert.False(QuestPlan.ShouldFinishOnReveal(0, 0, false));
        Assert.Equal(QuestIds.Placeholder, QuestPlan.StoredTitle(true, "Real"));
        Assert.Equal("", QuestPlan.StoredDescription(true, "Real"));
    }
}
