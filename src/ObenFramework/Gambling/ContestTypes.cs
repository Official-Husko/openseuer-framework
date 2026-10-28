using Currency = Money.Currency;

namespace ObenFramework.Gambling;

/// <summary>The kinds of contest the player can take part in.</summary>
public enum ContestKind
{
    Unknown,

    /// <summary>Downing pints against an NPC at the Bazaar bar.</summary>
    Drinking,

    /// <summary>Lasting longest in the heat at the Bazaar sauna.</summary>
    Sauna,

    /// <summary>Betting on a rat at the rat fighting arena.</summary>
    RatFight,
}

/// <summary>A contest going on, or the one that just ended.</summary>
public sealed class ContestInfo
{
    internal ContestInfo(string id, ContestKind kind, string name, string opponent)
    {
        Id = id;
        Kind = kind;
        Name = name ?? "";
        Opponent = opponent ?? "";
    }

    /// <summary>The kind and the address of the opponent's seat or the arena.</summary>
    public string Id { get; }

    public ContestKind Kind { get; }

    /// <summary>The game's name for it (<c>Drinking Contest</c>, <c>Sauna Contest</c>, <c>Rat fight</c>).</summary>
    public string Name { get; }

    /// <summary>The NPC the player is up against, or empty for a rat fight.</summary>
    public string Opponent { get; }

    public override string ToString() => string.IsNullOrEmpty(Opponent) ? Name : Name + " vs " + Opponent;
}

/// <summary>How a contest ended, raised by <see cref="Contests.Finished"/>.</summary>
public readonly struct ContestResult
{
    internal ContestResult(ContestInfo contest, bool playerWon, string winner, int winCoins, float winRealMoney)
    {
        Contest = contest;
        PlayerWon = playerWon;
        Winner = winner ?? "";
        WinCoins = winCoins;
        WinRealMoney = winRealMoney;
    }

    public ContestInfo Contest { get; }

    /// <summary>The player won the contest, or the rat they bet on won.</summary>
    public bool PlayerWon { get; }

    /// <summary>The winning rat's name at a rat fight; empty for other contests.</summary>
    public string Winner { get; }

    /// <summary>Rat fight winnings waiting to be collected, in coins, after <see cref="Payouts"/>.</summary>
    public int WinCoins { get; }

    /// <summary>Rat fight winnings in Real Money.</summary>
    public float WinRealMoney { get; }
}

/// <summary>Money at the rat fighting arena: a bet placed or winnings collected.</summary>
public readonly struct RatFightMoney
{
    internal RatFightMoney(ContestInfo arena, int amount, Currency currency, int fighter)
    {
        Arena = arena;
        Amount = amount;
        Currency = currency;
        Fighter = fighter;
    }

    public ContestInfo Arena { get; }

    public int Amount { get; }

    public Currency Currency { get; }

    /// <summary>The index of the rat bet on, or -1 when collecting.</summary>
    public int Fighter { get; }
}
