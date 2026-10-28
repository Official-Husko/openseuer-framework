using ObenFramework.Quests;

namespace ObenCore.Tests.Story;

public class QuestRewindPlanTests
{
    private static List<string>[] Lists() => new[] { new List<string>(), new List<string>(), new List<string>(), new List<string>(), new List<string>() };

    [Fact]
    public void ATaskIsInExactlyTheListsOfItsStatus()
    {
        var l = Lists();
        l[4].Add("MyMod.a");
        l[4].Add("MyMod.a");
        l[1].Add("other");
        QuestRewindPlan.TaskLists(l[0], l[1], l[2], l[3], l[4], "MyMod.a", QuestRewindPlan.Active);
        Assert.Equal(new[] { "MyMod.a" }, l[0]);
        Assert.Equal(new[] { "other", "MyMod.a" }, l[1]);
        Assert.Empty(l[4]);
    }

    [Fact]
    public void GoneTakesItOutOfEverything()
    {
        var l = Lists();
        foreach (var list in l)
            list.Add("MyMod.a");
        QuestRewindPlan.TaskLists(l[0], l[1], l[2], l[3], l[4], "MyMod.a", QuestRewindPlan.Gone);
        Assert.All(l, Assert.Empty);
    }

    [Fact]
    public void AHiddenObjectiveIsInNoList()
    {
        var l = Lists();
        l[0].Add("MyMod.a_x");
        l[1].Add("MyMod.a_x");
        QuestRewindPlan.ObjectiveLists(l[0], l[1], l[2], l[3], l[4], "MyMod.a_x", QuestRewindPlan.Hidden);
        Assert.All(l, Assert.Empty);
    }

    [Fact]
    public void AFinishedObjectiveIsCompleted()
    {
        var l = Lists();
        QuestRewindPlan.ObjectiveLists(l[0], l[1], l[2], l[3], l[4], "MyMod.a_x", QuestRewindPlan.Finished);
        Assert.Single(l[0]);
        Assert.Single(l[4]);
        Assert.Empty(l[1]);
    }

    [Fact]
    public void JumpFinishesWhatComesBeforeAndResetsWhatComesAfter()
    {
        Assert.Equal(QuestRewindPlan.Finished, QuestRewindPlan.JumpStatus(0, 2, QuestRewindPlan.Active));
        Assert.Equal(QuestRewindPlan.Active, QuestRewindPlan.JumpStatus(2, 2, QuestRewindPlan.Hidden));
        Assert.Equal(QuestRewindPlan.Hidden, QuestRewindPlan.JumpStatus(3, 2, QuestRewindPlan.Hidden));
        Assert.Equal(5, QuestRewindPlan.JumpAmount(1, 2, 5));
        Assert.Equal(0, QuestRewindPlan.JumpAmount(2, 2, 5));
    }

    [Fact]
    public void TextPrefersPlaceholderThenRecordThenCapture()
    {
        var hidden = QuestRewindPlan.Text(QuestRewindPlan.Hidden, true, "Real", "Desc", "Cap", "CapD");
        Assert.Equal(QuestIds.Placeholder, hidden.Title);
        Assert.Equal("", hidden.Description);

        var record = QuestRewindPlan.Text(QuestRewindPlan.Active, true, "Real", null, "Cap", "CapD");
        Assert.Equal("Real", record.Title);
        Assert.Equal("", record.Description);

        var captured = QuestRewindPlan.Text(QuestRewindPlan.Active, false, null, null, "Cap", null);
        Assert.Equal("Cap", captured.Title);

        Assert.False(QuestRewindPlan.Text(QuestRewindPlan.Active, false, null, null, null, null).HasText);
    }
}

public class QuestFollowUpPlanTests
{
    [Fact]
    public void StartsWhenThereIsANewFollowUp() => Assert.Null(QuestFollowUpPlan.Skip("one", "two", false, false, 0));

    [Theory]
    [InlineData(null, false, false, 0, QuestFollowUpPlan.None)]
    [InlineData("one", false, false, 0, QuestFollowUpPlan.Self)]
    [InlineData("two", true, false, 0, QuestFollowUpPlan.Exists)]
    [InlineData("two", false, true, 0, QuestFollowUpPlan.Again)]
    [InlineData("two", false, false, 8, QuestFollowUpPlan.Depth)]
    public void SaysWhyNot(string next, bool exists, bool again, int started, string expected) =>
        Assert.Equal(expected, QuestFollowUpPlan.Skip("one", next, exists, again, started));

    [Fact]
    public void OnlyUnusualSkipsAreLogged()
    {
        Assert.False(QuestFollowUpPlan.IsWorthLogging(QuestFollowUpPlan.None));
        Assert.False(QuestFollowUpPlan.IsWorthLogging(QuestFollowUpPlan.Exists));
        Assert.True(QuestFollowUpPlan.IsWorthLogging(QuestFollowUpPlan.Depth));
        Assert.Equal("MyMod.two", QuestFollowUpPlan.TargetId("MyMod", "two"));
        Assert.Null(QuestFollowUpPlan.TargetId("MyMod", null));
    }
}

public class QuestBookTests
{
    private static QuestDefinition Task(string key, string title) => new QuestDefinition
    {
        Key = key,
        Title = title,
        FinishWhenDone = true,
        Objectives = { new QuestObjectiveDefinition { Key = "s1", Title = "Step", ItemId = 7, Count = 2, Hidden = true } },
    };

    [Fact]
    public void RemembersAChainAndBuildsItBack()
    {
        var first = Task("one", "First");
        first.NextDefinition = Task("two", "Second");
        var book = new QuestBook();
        book.Remember("MyMod", first);

        Assert.NotNull(book.Find("MyMod.one"));
        Assert.Equal("two", book.Find("MyMod.one").NextKey);
        var rebuilt = book.ToDefinition(book.Find("MyMod.one"));
        Assert.Equal("Second", rebuilt.NextDefinition.Title);
        Assert.True(rebuilt.FinishWhenDone);
        Assert.Equal(7, rebuilt.Objectives[0].ItemId);
        Assert.True(rebuilt.Objectives[0].Hidden);
    }

    [Fact]
    public void AStoredFollowUpIsNotOverwrittenByTheChain()
    {
        var book = new QuestBook();
        book.Remember("MyMod", Task("two", "Changed later"));
        var first = Task("one", "First");
        first.NextDefinition = Task("two", "Original");
        book.Remember("MyMod", first);
        Assert.Equal("Changed later", book.Find("MyMod.two").Title);
    }

    [Fact]
    public void TextChangesAndForgetting()
    {
        var book = new QuestBook();
        book.Remember("MyMod", Task("one", "First"));
        book.RememberText("MyMod.one", null, "Renamed", "New text");
        book.RememberText("MyMod.one", "s1", "Real step", "Real text");
        Assert.Equal("Renamed", book.Find("MyMod.one").Title);
        Assert.Equal("Real step", QuestBook.FindObjective(book.Find("MyMod.one"), "s1").Title);

        book.RememberObjective("MyMod.one", new QuestObjectiveDefinition { Key = "s2", Title = "Added" });
        Assert.Equal(2, book.Find("MyMod.one").Objectives.Count);

        book.Forget("MyMod.one");
        Assert.Null(book.Find("MyMod.one"));
    }

    [Fact]
    public void ASelfLoopDoesNotRecurse()
    {
        var book = new QuestBook();
        var looped = Task("one", "First");
        looped.Next = "one";
        book.Remember("MyMod", looped);
        Assert.Null(book.ToDefinition(book.Find("MyMod.one")).NextDefinition);
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var book = new QuestBook();
        book.Remember("MyMod", Task("one", "First"));
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(book);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<QuestBook>(json);
        Assert.Equal("First", back.Find("MyMod.one").Title);
        Assert.Equal(2, back.Find("MyMod.one").Objectives[0].Count);
    }
}
