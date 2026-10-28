using ObenFramework.Transport;

namespace ObenCore.Tests.Transport;

public class TicketPriceRulesTests
{
    [Fact]
    public void No_rules_keep_the_machines_price()
    {
        Assert.Equal(25, TicketPriceRules.Apply(25, new List<TicketPriceRule>(), "m1"));
        Assert.Equal(25, TicketPriceRules.Apply(25, null, "m1"));
    }

    [Fact]
    public void A_machine_price_beats_a_price_for_all_and_the_newest_wins()
    {
        var rules = new List<TicketPriceRule>
        {
            new() { Machine = "m1", Price = 10 },
            new() { Price = 40 },
            new() { Price = 50 },
        };
        Assert.Equal(10, TicketPriceRules.Apply(25, rules, "m1"));
        Assert.Equal(50, TicketPriceRules.Apply(25, rules, "m2"));
        rules.Add(new TicketPriceRule { Machine = "m1", Price = 12 });
        Assert.Equal(12, TicketPriceRules.Apply(25, rules, "m1"));
    }

    [Fact]
    public void Factors_multiply_after_the_price_and_round()
    {
        var rules = new List<TicketPriceRule>
        {
            new() { Factor = 1.5f },
            new() { Machine = "m1", Factor = 2f },
            new() { Machine = "m2", Price = 9 },
        };
        Assert.Equal(75, TicketPriceRules.Apply(25, rules, "m1"));
        Assert.Equal(14, TicketPriceRules.Apply(25, rules, "m2"));   // 9 * 1.5 = 13.5, rounded up
        Assert.Equal(38, TicketPriceRules.Apply(25, rules, "m3"));   // 37.5
    }

    [Fact]
    public void Prices_never_go_below_zero_and_bad_rules_are_refused()
    {
        Assert.Equal(0, TicketPriceRules.Apply(25, new List<TicketPriceRule> { new() { Factor = 0f } }, "m1"));
        Assert.Equal(0, TicketPriceRules.Apply(-5, null, "m1"));
        Assert.NotNull(TicketPriceRules.Check(new TicketPriceRule { Price = -1 }));
        Assert.NotNull(TicketPriceRules.Check(new TicketPriceRule { Factor = float.NaN }));
        Assert.NotNull(TicketPriceRules.Check(new TicketPriceRule { Factor = -0.5f }));
        Assert.NotNull(TicketPriceRules.Check(null));
        Assert.Null(TicketPriceRules.Check(new TicketPriceRule { Price = 0, Factor = 0.5f }));
    }
}
