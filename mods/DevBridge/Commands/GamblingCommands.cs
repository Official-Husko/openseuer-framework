using System;
using System.Linq;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Gambling;

namespace DevBridge.Commands;

/// <summary>Casino machines, card tables, contests and payout multipliers.</summary>
internal static class GamblingCommands
{
    [BridgeCommand("casino.machines", "The slot machines and pajatso of the loaded scene, and the player's gambling record.")]
    private static object Machines(JObject p)
    {
        var stats = Casino.Stats;
        return new JObject
        {
            ["machines"] = new JArray(Casino.Machines().Select(m => new JObject
            {
                ["id"] = m.Id,
                ["kind"] = m.Kind.ToString(),
                ["scene"] = m.Scene,
                ["stake"] = m.Stake,
                ["moneyIn"] = m.MoneyIn,
                ["winnings"] = m.Winnings,
            }).ToArray()),
            ["won"] = stats.Won,
            ["lost"] = stats.Lost,
            ["addiction"] = stats.Addiction,
            ["need"] = stats.Need,
        };
    }

    [BridgeCommand("cards.tables", "The card tables of the loaded scene and the game on each.")]
    private static object Tables(JObject p) => new JArray(CardGames.Tables().Select(t => new JObject
    {
        ["id"] = t.Id,
        ["scene"] = t.Scene,
        ["kind"] = t.Kind.ToString(),
        ["players"] = t.Players,
        ["playing"] = t.Playing,
        ["stake"] = t.Stake,
        ["currency"] = t.Currency.ToString(),
        ["betItem"] = t.BetItem?.ID,
        ["netWinnings"] = t.NetWinnings,
    }).ToArray());

    [BridgeCommand("contests.list", "The drinking or sauna contest and rat fights going on.")]
    private static object ContestList(JObject p) => new JObject
    {
        ["active"] = new JArray(Contests.Active().Select(c => new JObject
        {
            ["id"] = c.Id,
            ["kind"] = c.Kind.ToString(),
            ["name"] = c.Name,
            ["opponent"] = c.Opponent,
        }).ToArray()),
        ["opponentTolerance"] = Contests.OpponentTolerance(),
    };

    [BridgeCommand("gambling.payout", "The payout multipliers in force, or set the bridge's own for a game (1 removes it).",
        Params = new[] { "game?: all, slotmachine, pajatso, chinahand, ristiseiska or ratfight", "multiplier?: 0 to 10", "clear?: true removes the bridge's multiplier for game, or all of them without game" })]
    private static object Payout(JObject p)
    {
        string game = p.Str("game", null);
        if (p.Bool("clear", false))
        {
            foreach (GambleKind kind in Enum.GetValues(typeof(GambleKind)))
            {
                if (game == null || string.Equals(kind.ToString(), game, StringComparison.OrdinalIgnoreCase))
                    Payouts.Remove(DevBridgeMod.Instance, kind);
            }
        }
        else if (game != null && p["multiplier"] != null)
        {
            if (!Enum.TryParse(game, true, out GambleKind kind))
                throw new ArgumentException("Unknown game '" + game + "'.");
            float multiplier = p.Float("multiplier");
            if (multiplier == 1f)
                Payouts.Remove(DevBridgeMod.Instance, kind);
            else
                Payouts.Set(DevBridgeMod.Instance, kind, multiplier);
        }
        return new JObject
        {
            ["multipliers"] = new JObject(Enum.GetValues(typeof(GambleKind)).Cast<GambleKind>()
                .Select(k => new JProperty(k.ToString(), Payouts.Multiplier(k)))),
            ["set"] = new JArray(Payouts.Active.Select(e => new JObject
            {
                ["owner"] = e.OwnerId,
                ["game"] = ((GambleKind)e.Channel).ToString(),
                ["key"] = e.Key,
                ["value"] = e.Value,
            }).ToArray()),
        };
    }
}
