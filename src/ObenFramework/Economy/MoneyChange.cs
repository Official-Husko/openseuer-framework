using Currency = Money.Currency;

namespace ObenFramework.Economy;

/// <summary>One balance change, raised by <see cref="Wallet.Changed"/>.</summary>
public readonly struct MoneyChange
{
    internal MoneyChange(Currency currency, float before, float after)
    {
        Currency = currency;
        Before = before;
        After = after;
    }

    public Currency Currency { get; }

    /// <summary>The balance before the change.</summary>
    public float Before { get; }

    /// <summary>The balance after the change.</summary>
    public float After { get; }

    /// <summary>Positive when money came in, negative when it went out.</summary>
    public float Delta => After - Before;

    public override string ToString() => $"{Currency} {Before} -> {After}";
}
