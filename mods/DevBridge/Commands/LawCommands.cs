using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Story;

namespace DevBridge.Commands;

/// <summary>The player's crime record, fines and arrests.</summary>
internal static class LawCommands
{
    [BridgeCommand("law.crimes", "The crime record: totals, fine, sentence, stolen goods and every record line.")]
    private static object Crimes(JObject p)
    {
        Require();
        var lines = new JArray();
        foreach (var line in Law.Records())
        {
            lines.Add(new JObject
            {
                ["kind"] = line.Kind.ToString(),
                ["victimId"] = line.VictimId,
                ["victim"] = line.VictimName,
                ["amount"] = line.Amount,
            });
        }
        return new JObject
        {
            ["total"] = Law.Total,
            ["onRecord"] = Law.TotalOnRecord,
            ["fine"] = Law.Fine,
            ["sentenceHours"] = Law.SentenceHours,
            ["timesInJail"] = Law.TimesInJail,
            ["stolenGoods"] = Law.HasStolenGoods,
            ["stolenValue"] = Law.StolenGoodsValue,
            ["arrestInProgress"] = Law.IsArrestInProgress,
            ["inPrison"] = Law.InPrison,
            ["records"] = lines,
        };
    }

    [BridgeCommand("law.commit", "Put a crime on the record like the game does.",
        Params = new[] { "kind: Theft, Violence, Fraud, Trespassing, Vandalism ...", "severity: 1 to 1000000", "victim: optional NPC id", "silent: no notification, default false" })]
    private static object Commit(JObject p)
    {
        Require();
        bool done = Law.Commit(ParseKind(p.Str("kind")), p.Int("severity", 10), p.Int("victim", 0), p.Bool("silent", false));
        return new JObject { ["committed"] = done, ["onRecord"] = Law.TotalOnRecord };
    }

    [BridgeCommand("law.clear", "Clear the record: all of it, one kind, one victim, or one kind against one victim.",
        Params = new[] { "kind: optional crime kind", "victim: optional NPC id" })]
    private static object Clear(JObject p)
    {
        Require();
        int victim = p.Int("victim", 0);
        string kind = p.Str("kind", null);
        bool done = kind == null
            ? victim > 0 ? Law.Clear(victim) : Law.Clear()
            : victim > 0 ? Law.Clear(victim, ParseKind(kind)) : Law.Clear(ParseKind(kind));
        return new JObject { ["cleared"] = done, ["onRecord"] = Law.TotalOnRecord };
    }

    [BridgeCommand("law.fine", "Read the fine, or pay it from the player's real money with pay=true (stolen goods are taken too).",
        Params = new[] { "pay: default false" })]
    private static object Fine(JObject p)
    {
        Require();
        int fine = Law.Fine;
        if (!p.Bool("pay", false))
            return new JObject { ["fine"] = fine };
        return new JObject { ["fine"] = fine, ["paid"] = Law.PayFine(), ["onRecord"] = Law.TotalOnRecord };
    }

    [BridgeCommand("law.arrest", "Arrest the player like the police do (the player passes out and wakes in prison).",
        Params = new[] { "factor: record multiplier, default 1" })]
    private static object Arrest(JObject p)
    {
        Require();
        return new JObject { ["arrested"] = Law.Arrest(p.Float("factor", 1f)) };
    }

    private static void Require()
    {
        if (!Law.IsAvailable)
            throw new InvalidOperationException("There is no crime record: not in a gameplay scene.");
    }

    private static CrimeKind ParseKind(string text)
    {
        if (Enum.TryParse(text, ignoreCase: true, out CrimeKind kind) && Enum.IsDefined(typeof(CrimeKind), kind) && kind != CrimeKind.None)
            return kind;
        throw new ArgumentException($"'{text}' is not a crime kind; use {string.Join(", ", Enum.GetNames(typeof(CrimeKind)))} (not None).");
    }
}
