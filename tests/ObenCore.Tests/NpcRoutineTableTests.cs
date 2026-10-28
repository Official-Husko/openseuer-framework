using ObenFramework.Npcs;

namespace ObenCore.Tests;

public class NpcRoutineTableTests
{
    private const int Monday = (int)NpcDays.Monday;
    private const int Sunday = (int)NpcDays.Sunday;

    [Fact]
    public void WeekMinutesStartOnMonday()
    {
        DayOfWeek[] order = { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        for (int i = 0; i < order.Length; i++)
        {
            Assert.Equal(i * 1440, NpcRoutineTable.WeekMinute(order[i], 0, 0));
            Assert.Equal(i * 1440 + 1439, NpcRoutineTable.WeekMinute(order[i], 23, 59));
            Assert.Equal(NpcRoutineTable.WeekMinute(order[i], 7, 30), NpcRoutineTable.WeekMinute(i, 7, 30));
        }
        Assert.Equal(10079, NpcRoutineTable.WeekMinute(DayOfWeek.Sunday, 23, 59));
    }

    [Theory]
    [InlineData(0, 480, 720)]
    [InlineData(128, 480, 720)]
    [InlineData(Monday, -1, 720)]
    [InlineData(Monday, 1440, 720)]
    [InlineData(Monday, 480, 0)]
    [InlineData(Monday, 480, 1441)]
    [InlineData(Monday, 480, 480)]
    public void BadStepsAreRefused(int days, int from, int to) =>
        Assert.NotNull(NpcRoutineTable.Check(days, from, to));

    [Theory]
    [InlineData(127, 480, 720)]
    [InlineData(Monday, 0, 1440)]
    [InlineData(Monday, 1380, 120)]
    public void GoodStepsPass(int days, int from, int to) =>
        Assert.Null(NpcRoutineTable.Check(days, from, to));

    [Fact]
    public void AWindowCoversItsStartButNotItsEnd()
    {
        var office = NpcRoutineTable.Windows(Monday, 480, 720);
        Assert.Single(office);
        Assert.True(NpcRoutineTable.Covers(office[0], NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 8, 0)));
        Assert.True(NpcRoutineTable.Covers(office[0], NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 11, 59)));
        Assert.False(NpcRoutineTable.Covers(office[0], NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 12, 0)));
        Assert.False(NpcRoutineTable.Covers(office[0], NpcRoutineTable.WeekMinute(DayOfWeek.Tuesday, 8, 0)));

        var toMidnight = NpcRoutineTable.Windows(Monday, 1380, 1440);
        Assert.Single(toMidnight);
        Assert.True(NpcRoutineTable.Covers(toMidnight[0], NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 23, 59)));
    }

    [Fact]
    public void AWindowPastMidnightSplitsIntoTheNextDay()
    {
        var sundayNight = NpcRoutineTable.Windows(Sunday, 1380, 120);
        Assert.Equal(2, sundayNight.Length);
        Assert.Equal(Monday, sundayNight[1].Days);
        Assert.Equal(0, sundayNight[1].From);
        Assert.Equal(120, sundayNight[1].To);
        Assert.Equal(Sunday | Monday, NpcRoutineTable.Windows((int)NpcDays.Weekend, 1380, 120)[1].Days);

        Assert.Equal(1, NpcRoutineTable.ActiveIndex(sundayNight, NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 1, 59)));
        Assert.Equal(-1, NpcRoutineTable.ActiveIndex(sundayNight, NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 2, 0)));
        Assert.Equal(0, NpcRoutineTable.ActiveIndex(sundayNight, NpcRoutineTable.WeekMinute(DayOfWeek.Sunday, 23, 30)));

        var mondayNight = NpcRoutineTable.Windows(Monday, 1380, 120);
        Assert.Equal(-1, NpcRoutineTable.ActiveIndex(mondayNight, NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 1, 0)));
        Assert.Equal(1, NpcRoutineTable.ActiveIndex(mondayNight, NpcRoutineTable.WeekMinute(DayOfWeek.Tuesday, 1, 0)));
    }

    [Fact]
    public void EarlierWindowsWinWhereTheyOverlap()
    {
        Assert.Equal(-1, NpcRoutineTable.ActiveIndex(new List<NpcWindow>(), 0));

        var overlap = new List<NpcWindow>();
        overlap.AddRange(NpcRoutineTable.Windows(Monday, 480, 720));
        overlap.AddRange(NpcRoutineTable.Windows(Monday, 600, 900));
        Assert.Equal(0, NpcRoutineTable.ActiveIndex(overlap, NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 10, 0)));
        Assert.Equal(1, NpcRoutineTable.ActiveIndex(overlap, NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 12, 30)));

        var gap = new List<NpcWindow>();
        gap.AddRange(NpcRoutineTable.Windows(Monday, 480, 600));
        gap.AddRange(NpcRoutineTable.Windows(Monday, 700, 800));
        Assert.Equal(-1, NpcRoutineTable.ActiveIndex(gap, NpcRoutineTable.WeekMinute(DayOfWeek.Monday, 11, 0)));
    }

    [Theory]
    [InlineData("B", "B", true, true, NpcPresence.Keep)]
    [InlineData("B", "B", true, false, NpcPresence.Keep)]
    [InlineData("B", "B", false, true, NpcPresence.Spawn)]
    [InlineData("B", "B", false, false, NpcPresence.Spawn)]
    [InlineData("B", "A", true, true, NpcPresence.Leave)]
    [InlineData("B", "A", true, false, NpcPresence.None)]
    [InlineData("B", "A", false, true, NpcPresence.None)]
    [InlineData(null, "A", true, true, NpcPresence.Leave)]
    [InlineData(null, null, false, false, NpcPresence.None)]
    [InlineData(null, null, true, true, NpcPresence.Leave)]
    internal void PresenceFollowsTheStepsScene(string? step, string? active, bool here, bool spawned, NpcPresence expected) =>
        Assert.Equal(expected, NpcRoutineTable.Presence(step, active, here, spawned));
}
