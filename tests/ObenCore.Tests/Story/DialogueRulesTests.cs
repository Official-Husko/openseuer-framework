using ObenFramework.Dialogue;

namespace ObenCore.Tests.Story;

public class DialoguePlanTests
{
    private static ConversationDefinition Talk() => new ConversationDefinition
    {
        Key = "greeting",
        Lines =
        {
            new ConversationLine { Key = "hello", Text = "Hello there.", Next = new[] { "ask", "leave" } },
            new ConversationLine { Key = "ask", Player = true, Text = "What's new?", Next = new[] { "news" } },
            new ConversationLine { Key = "leave", Player = true, Text = "Bye." },
            new ConversationLine { Key = "news", Text = "Nothing much." },
        },
    };

    [Fact]
    public void AcceptsAValidConversation() => Assert.Null(DialoguePlan.Check("MyMod", Talk()));

    [Fact]
    public void RefusesLinksToMissingLines()
    {
        var talk = Talk();
        talk.Lines[3].Next = new[] { "nowhere" };
        Assert.Contains("nowhere", DialoguePlan.Check("MyMod", talk));
    }

    [Fact]
    public void RefusesDuplicateKeysAndEmptyText()
    {
        var talk = Talk();
        talk.Lines[2].Key = "ask";
        Assert.NotNull(DialoguePlan.Check("MyMod", talk));

        talk = Talk();
        talk.Lines[0].Text = " ";
        Assert.NotNull(DialoguePlan.Check("MyMod", talk));
        talk.Lines[0].TextCallback = () => "dynamic";
        talk.Lines[0].Text = null;
        Assert.Null(DialoguePlan.Check("MyMod", talk));
    }

    [Fact]
    public void RefusesLongLua()
    {
        var talk = Talk();
        talk.Lines[0].Condition = new string('a', DialoguePlan.MaxLua + 1);
        Assert.NotNull(DialoguePlan.Check("MyMod", talk));
    }

    [Fact]
    public void IdsAreStableAndInTheModRange()
    {
        int id = DialoguePlan.StableId("MyMod.greeting");
        Assert.Equal(id, DialoguePlan.StableId("MyMod.greeting"));
        Assert.InRange(id, DialoguePlan.FirstId, DialoguePlan.FirstId + DialoguePlan.IdRange - 1);
        Assert.Equal(id + 2, DialoguePlan.FreeIdFrom(id, i => i < id + 2));
        Assert.Equal(DialoguePlan.FirstId, DialoguePlan.FreeIdFrom(DialoguePlan.FirstId + DialoguePlan.IdRange - 1, i => i != DialoguePlan.FirstId));
    }

    [Fact]
    public void KeysDependOnEverythingThatMakesAResponse()
    {
        string a = DialoguePlan.ResponseKey("MyMod", "Bazaar/Trader/Jens Brom", null, 1, "Hi", null, null);
        Assert.Equal(a, DialoguePlan.ResponseKey("MyMod", "Bazaar/Trader/Jens Brom", null, 1, "Hi", null, null));
        Assert.NotEqual(a, DialoguePlan.ResponseKey("MyMod", "Bazaar/Trader/Jens Brom", null, 2, "Hi", null, null));
        Assert.NotEqual(a, DialoguePlan.ResponseKey("MyMod", "Bazaar/Trader/Jens Brom", "Heh heh.", 1, "Hi", null, null));
        Assert.StartsWith("MyMod.", a);
        Assert.NotEqual(DialoguePlan.BranchKey("MyMod", "c", "text", -1, "b"), DialoguePlan.BranchKey("MyMod", "c", null, 3, "b"));
    }

    [Theory]
    [InlineData(-1, 3, 3)]
    [InlineData(0, 3, 0)]
    [InlineData(2, 3, 2)]
    [InlineData(9, 3, 3)]
    public void InsertIndex(int requested, int count, int expected) => Assert.Equal(expected, DialoguePlan.InsertIndex(requested, count));

    [Fact]
    public void PriorityRules()
    {
        Assert.True(DialoguePlan.ValidPriority(-1));
        Assert.True(DialoguePlan.ValidPriority(4));
        Assert.False(DialoguePlan.ValidPriority(5));
        Assert.Equal(2, DialoguePlan.RequestedPriority(-1, 2));
        Assert.Equal(4, DialoguePlan.RequestedPriority(4, 2));
        // An NPC-first branch without an asked priority goes one below the menu, but not below Low.
        Assert.Equal(1, DialoguePlan.LinkPriority(-1, 2, npcFirst: true));
        Assert.Equal(0, DialoguePlan.LinkPriority(-1, 0, npcFirst: true));
        Assert.Equal(2, DialoguePlan.LinkPriority(-1, 2, npcFirst: false));
        Assert.Equal(3, DialoguePlan.LinkPriority(3, 2, npcFirst: true));
    }

    [Fact]
    public void PlainDropsTypingCodes()
    {
        Assert.Equal("Heh heh. I got stuff!", DialoguePlan.Plain("Heh heh.\\,\\, I got stuff!"));
        Assert.Equal("Fine... I can buy your alcohol", DialoguePlan.Plain("Fine...\\,\\, I can buy your\\, alcohol"));
        Assert.Equal("a\\b", DialoguePlan.Plain("  a\\b  "));
        Assert.Equal("", DialoguePlan.Plain(null));
    }

    [Fact]
    public void AnchorMatchingFallsBackStepByStep()
    {
        var texts = new List<string> { "Heh heh.\\,\\, I got stuff!", "Sure.", "sure.", " Bye " };
        Assert.Equal(new[] { 1 }, DialoguePlan.MatchAnchor(texts, "Sure."));
        Assert.Equal(new[] { 1, 2 }, DialoguePlan.MatchAnchor(texts, "SURE."));
        Assert.Equal(new[] { 3 }, DialoguePlan.MatchAnchor(texts, "bye"));
        Assert.Equal(new[] { 0 }, DialoguePlan.MatchAnchor(texts, "Heh heh. I got stuff!"));
        Assert.Empty(DialoguePlan.MatchAnchor(texts, "nothing like it"));
        Assert.Empty(DialoguePlan.MatchAnchor(texts, "  "));
    }

    [Fact]
    public void TopicsChainTheReplies()
    {
        var topic = DialoguePlan.Topic("MyMod", "Bazaar/Trader/Jens Brom", 1, "Any work?", new[] { "Maybe.", "Come back later." }, "Variable['x'] == true");
        Assert.Null(DialoguePlan.Check("MyMod", topic));
        Assert.True(topic.Lines[0].Player);
        Assert.Equal("Variable['x'] == true", topic.Lines[0].Condition);
        Assert.Equal(new[] { "reply2" }, topic.Lines[1].Next);
        Assert.Null(topic.Lines[2].Next);
        Assert.StartsWith("topic", topic.Key);
    }
}
