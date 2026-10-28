using System;
using System.Collections.Generic;

namespace ObenFramework.Transport;

/// <summary>One mod's say on ticket prices: a fixed price, a factor, or both, for one machine or all of them.</summary>
public sealed class TicketPriceRule
{
    /// <summary>The machine's id, or null for every ticket machine.</summary>
    public string Machine;

    /// <summary>A price that replaces the machine's own, or null to keep it.</summary>
    public int? Price;

    /// <summary>Multiplies the price (after any replacement). 1 leaves it.</summary>
    public float Factor = 1f;

    public bool AppliesTo(string machine) => Machine == null || Machine == machine;
}

/// <summary>
/// How several mods' ticket price rules combine, without Unity. A replacement price for the one machine beats one
/// for every machine; among equals the newest rule wins. Then every factor that applies multiplies the price,
/// which is rounded to whole coins and never goes below zero.
/// </summary>
public static class TicketPriceRules
{
    public const int MaxPrice = 1_000_000;

    public static string Check(TicketPriceRule rule)
    {
        if (rule == null)
            return "No rule given.";
        if (rule.Price.HasValue && (rule.Price.Value < 0 || rule.Price.Value > MaxPrice))
            return $"A price is 0 to {MaxPrice}.";
        if (float.IsNaN(rule.Factor) || float.IsInfinity(rule.Factor) || rule.Factor < 0f || rule.Factor > 100f)
            return "A factor is 0 to 100.";
        return null;
    }

    /// <summary>The price a machine charges given its own price and the rules, oldest first.</summary>
    public static int Apply(int basePrice, IList<TicketPriceRule> rules, string machine)
    {
        int price = basePrice;
        if (rules == null || rules.Count == 0)
            return Math.Max(0, price);

        int? general = null;
        int? specific = null;
        foreach (var rule in rules)
        {
            if (rule == null || !rule.Price.HasValue || !rule.AppliesTo(machine))
                continue;
            if (rule.Machine == null)
                general = rule.Price;
            else
                specific = rule.Price;
        }
        price = specific ?? general ?? basePrice;

        double scaled = price;
        foreach (var rule in rules)
        {
            if (rule != null && rule.AppliesTo(machine))
                scaled *= rule.Factor;
        }
        return (int)Math.Max(0, Math.Min(MaxPrice, Math.Round(scaled, MidpointRounding.AwayFromZero)));
    }
}
