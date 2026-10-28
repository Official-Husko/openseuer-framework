using ObenFramework.World;

namespace ObenCore.Tests.Transport;

public class DoorScheduleRulesTests
{
    [Fact]
    public void Game_day_zero_is_monday()
    {
        Assert.Equal(0, DoorScheduleRules.ToGameDay(DayOfWeek.Monday));
        Assert.Equal(6, DoorScheduleRules.ToGameDay(DayOfWeek.Sunday));
        Assert.Equal(DayOfWeek.Monday, DoorScheduleRules.FromGameDay(0));
        Assert.Equal(DayOfWeek.Sunday, DoorScheduleRules.FromGameDay(6));
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            Assert.Equal(day, DoorScheduleRules.FromGameDay(DoorScheduleRules.ToGameDay(day)));
    }

    [Fact]
    public void Hours_round_trip_through_the_game_table()
    {
        var hours = new List<OpeningHours> { new(DayOfWeek.Friday, 9, 17), new(DayOfWeek.Sunday, 0, 24) };
        var table = DoorScheduleRules.ToGame(hours);
        Assert.Equal((4, 9, 17), table[0]);
        Assert.Equal((6, 0, 24), table[1]);
        Assert.Equal(hours, DoorScheduleRules.FromGame(table));
    }

    [Fact]
    public void Bad_hours_are_refused()
    {
        Assert.NotNull(DoorScheduleRules.Check(null));
        Assert.Null(DoorScheduleRules.Check(new List<OpeningHours>()));
        Assert.NotNull(DoorScheduleRules.Check(new[] { new OpeningHours(DayOfWeek.Monday, 24, 24) }));
        Assert.NotNull(DoorScheduleRules.Check(new[] { new OpeningHours(DayOfWeek.Monday, 10, 10) }));
        Assert.NotNull(DoorScheduleRules.Check(new[] { new OpeningHours(DayOfWeek.Monday, 10, 25) }));
        Assert.NotNull(DoorScheduleRules.Check(new[] { new OpeningHours((DayOfWeek)9, 8, 16) }));
        Assert.Throws<ArgumentException>(() => DoorScheduleRules.ToGame(new[] { new OpeningHours(DayOfWeek.Monday, -1, 5) }));
    }

    [Fact]
    public void Open_from_the_opening_hour_until_before_the_closing_hour()
    {
        var table = DoorScheduleRules.ToGame(new[] { new OpeningHours(DayOfWeek.Tuesday, 8, 16) });
        Assert.False(DoorScheduleRules.IsOpen(table, 1, 7.99f));
        Assert.True(DoorScheduleRules.IsOpen(table, 1, 8f));
        Assert.True(DoorScheduleRules.IsOpen(table, 1, 15.5f));
        Assert.False(DoorScheduleRules.IsOpen(table, 1, 16f));
        Assert.False(DoorScheduleRules.IsOpen(table, 2, 12f));
    }

    [Fact]
    public void Unknown_days_in_the_game_table_are_skipped()
    {
        var hours = DoorScheduleRules.FromGame(new[] { (7, 8, 9), (-1, 8, 9), (3, 8, 9) });
        Assert.Single(hours);
        Assert.Equal(DayOfWeek.Thursday, hours[0].Day);
    }

    [Fact]
    public void Every_day_covers_the_week()
    {
        var hours = DoorScheduleRules.EveryDay(10, 18);
        Assert.Equal(7, hours.Count);
        Assert.Null(DoorScheduleRules.Check(hours));
        var table = DoorScheduleRules.ToGame(hours);
        for (int day = 0; day < 7; day++)
            Assert.True(DoorScheduleRules.IsOpen(table, day, 12f));
    }
}
