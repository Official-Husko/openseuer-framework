using ObenFramework.Player;

namespace ObenCore.Tests;

public class CustomSkillMathTests
{
    [Theory]
    [InlineData(-1f, 10f, 0f)]
    [InlineData(12f, 10f, 10f)]
    [InlineData(4.5f, 10f, 4.5f)]
    public void ClampKeepsZeroToMax(float value, float max, float expected) =>
        Assert.Equal(expected, CustomSkillMath.Clamp(value, max));

    [Theory]
    [InlineData(0.999f, 0)]
    [InlineData(1f, 1)]
    [InlineData(9.99f, 9)]
    [InlineData(10f, 10)]
    public void LevelIsTheFloor(float value, int level) => Assert.Equal(level, CustomSkillMath.Level(value));

    [Fact]
    public void BannerTextsMatchTheGame()
    {
        Assert.Equal("Music\nincreased to\nlevel 3!", CustomSkillMath.LevelUpText("Music", 3));
        Assert.Equal("New skill\nMusic\nlearned!", CustomSkillMath.LearnedText("Music"));
    }

    [Fact]
    public void BannerOnlyForALevelUp()
    {
        Assert.Equal(CustomSkillMath.LearnedText("Fishing"), CustomSkillMath.BannerFor("Fishing", 0.5f, 1.2f));
        Assert.Equal(CustomSkillMath.LevelUpText("Fishing", 4), CustomSkillMath.BannerFor("Fishing", 2.5f, 4.1f));
        Assert.Null(CustomSkillMath.BannerFor("Fishing", 2.1f, 2.9f));
        Assert.Null(CustomSkillMath.BannerFor("Fishing", 3f, 1f));
    }

    [Theory]
    [InlineData("fishing", true)]
    [InlineData("deep-sea_fishing.2", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("a:b", false)]
    public void Ids(string id, bool valid) => Assert.Equal(valid, CustomSkillMath.ValidId(id));

    [Fact]
    public void CleanValuesDropsBrokenEntries()
    {
        var saved = new Dictionary<string, float>
        {
            ["MyMod:fishing"] = 2f,
            ["Other Mod:cooking"] = 3.5f,
            ["MyMod:"] = 1f,
            ["nokey"] = 1f,
            ["MyMod:bad id"] = 1f,
            ["MyMod:nan"] = float.NaN,
            ["MyMod:negative"] = -1f,
        };
        var clean = CustomSkillMath.CleanValues(saved);
        Assert.Equal(2, clean.Count);
        Assert.Equal(2f, clean["MyMod:fishing"]);
        Assert.Equal(3.5f, clean["Other Mod:cooking"]);
        Assert.Empty(CustomSkillMath.CleanValues(null));
    }
}

public class SkillBannerQueueTests
{
    private const float S = SkillBannerQueue.Seconds;
    private readonly SkillBannerQueue _queue = new();

    [Fact]
    public void GameBannersShowAtOnceWithoutModBanners()
    {
        Assert.True(_queue.ClaimForGame(0f, "a"));
        Assert.True(_queue.ClaimForGame(1f, "b")); // the game replaces its own banner as before
        Assert.Equal(0, _queue.Count);
    }

    [Fact]
    public void ModBannersWaitForAGameBanner()
    {
        Assert.True(_queue.ClaimForGame(0f, "game"));
        _queue.AddSkill("m:s", "skill");
        Assert.False(_queue.Next(S - 0.5f, out _));
        Assert.True(_queue.Next(S, out string text));
        Assert.Equal("skill", text);
    }

    [Fact]
    public void GameBannersQueueBehindModBanners()
    {
        _queue.AddText("mod");
        Assert.True(_queue.Next(0f, out _));
        Assert.False(_queue.ClaimForGame(1f, "game"));
        Assert.False(_queue.Next(S - 1f, out _));
        Assert.True(_queue.Next(S, out string text));
        Assert.Equal("game", text);
    }

    [Fact]
    public void GameBannerQueuesWhileModBannersWait()
    {
        _queue.AddSkill("m:s", "skill");
        Assert.False(_queue.ClaimForGame(0f, "game"));
        Assert.True(_queue.Next(0f, out string first));
        Assert.True(_queue.Next(S, out string second));
        Assert.Equal("skill", first);
        Assert.Equal("game", second);
    }

    [Fact]
    public void SecondBannerOfOneSkillReplacesTheFirst()
    {
        _queue.AddSkill("m:s", "level 2");
        _queue.AddSkill("m:s", "level 3");
        Assert.Equal(1, _queue.Count);
        Assert.True(_queue.Next(0f, out string text));
        Assert.Equal("level 3", text);
    }

    [Fact]
    public void SkillBannerCapDropsTheOldestSkillBanner()
    {
        _queue.AddText("text first");
        for (int i = 1; i <= SkillBannerQueue.MaxSkillBanners + 1; i++)
            _queue.AddSkill("m:s" + i, "skill " + i);
        Assert.Equal(SkillBannerQueue.MaxSkillBanners + 1, _queue.Count);

        var order = Drain();
        Assert.Equal("text first", order[0]);
        Assert.Equal("skill 2", order[1]);
        Assert.DoesNotContain("skill 1", order);
    }

    [Fact]
    public void TextBannersAreRefusedAboveTheirCap()
    {
        for (int i = 0; i < SkillBannerQueue.MaxTextBanners; i++)
            Assert.True(_queue.AddText("t" + i));
        Assert.False(_queue.AddText("one too many"));
        _queue.AddSkill("m:s", "skill still fits");
        Assert.Equal(SkillBannerQueue.MaxTextBanners + 1, _queue.Count);
    }

    [Fact]
    public void ClearEmptiesAndFreesTheSpot()
    {
        _queue.AddText("a");
        Assert.True(_queue.Next(0f, out _));
        _queue.AddText("b");
        _queue.Clear();
        Assert.Equal(0, _queue.Count);
        Assert.True(_queue.ClaimForGame(1f, "game"));
    }

    private List<string> Drain()
    {
        var order = new List<string>();
        float at = 0f;
        while (_queue.Next(at, out string text))
        {
            order.Add(text);
            at += S;
        }
        return order;
    }
}
