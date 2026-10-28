// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;
using Currency = Money.Currency;
using Object = UnityEngine.Object;

namespace ObenFramework.Gambling;

/// <summary>A card table of the loaded scene and the game on it.</summary>
public sealed class CardTableInfo
{
    internal CardTableInfo(CardGameTable table)
    {
        Table = table;
        Id = ObjectAddress.Of(table).ToString();
        Scene = table.gameObject.scene.name;
        var game = CardTableFields.Game(table);
        Kind = game != null ? CardTableFields.KindOf(game) : GambleKind.All;
        Players = game != null ? game.PlayerCount : 0;
        Playing = game != null && game.gameState != GameState.NotStarted && game.gameState != GameState.GameEnded;
        Stake = Playing ? CardTableFields.Stake(table) : 0;
        Currency = CardTableFields.Currency(table);
        BetItem = CardTableFields.BetItemOf(table);
        NetWinnings = CardTableFields.NetWinnings(table);
    }

    public CardGameTable Table { get; }

    public string Id { get; }

    public string Scene { get; }

    /// <summary><see cref="GambleKind.ChinaHand"/> or <see cref="GambleKind.Ristiseiska"/>; <see cref="GambleKind.All"/> before a game is chosen.</summary>
    public GambleKind Kind { get; }

    public int Players { get; }

    /// <summary>A game is being played right now.</summary>
    public bool Playing { get; }

    /// <summary>The stake of the game being played (coins, Real Money or a number of <see cref="BetItem"/>).</summary>
    public int Stake { get; }

    public Currency Currency { get; }

    /// <summary>The item this table plays for instead of money, or null.</summary>
    public Item BetItem { get; }

    /// <summary>The player's payouts minus stakes at this table, as the table counts them.</summary>
    public int NetWinnings { get; }
}

/// <summary>A finished card game, raised by <see cref="CardGames.Ended"/>.</summary>
public readonly struct CardGameResult
{
    internal CardGameResult(CardTableInfo table, GambleKind kind, int placement, int stake, int payout)
    {
        Table = table;
        Kind = kind;
        Placement = placement;
        Stake = stake;
        Payout = payout;
    }

    public CardTableInfo Table { get; }

    public GambleKind Kind { get; }

    /// <summary>The player's place, 1 being the winner.</summary>
    public int Placement { get; }

    public bool Won => Placement == 1;

    public int Stake { get; }

    /// <summary>What the table paid back (twice the stake for first place, less further down), after <see cref="Payouts"/>.</summary>
    public int Payout { get; }
}

/// <summary>
/// The card tables (China Hand and Ristiseiska, played against NPCs for money or items; in the casino, the
/// bookshop and a test map). Lists tables and reports games starting and ending; table payouts can be scaled
/// with <see cref="Payouts"/>. Call on the main thread.
/// </summary>
public static class CardGames
{
    /// <summary>The player sat down to a game and the stake was taken.</summary>
    public static readonly WorldEvent<CardTableInfo> Started = new WorldEvent<CardTableInfo>(nameof(Started));

    /// <summary>A game ended and the table paid out.</summary>
    public static readonly WorldEvent<CardGameResult> Ended = new WorldEvent<CardGameResult>(nameof(Ended));

    /// <summary>Every card table of the loaded scenes.</summary>
    public static List<CardTableInfo> Tables()
    {
        var list = new List<CardTableInfo>();
        foreach (var table in Object.FindObjectsByType<CardGameTable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            list.Add(new CardTableInfo(table));
        return list;
    }

    /// <summary>The table with a game going on, or null.</summary>
    public static CardTableInfo Active()
    {
        foreach (var table in Tables())
        {
            if (table.Playing)
                return table;
        }
        return null;
    }
}
