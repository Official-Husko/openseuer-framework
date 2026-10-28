using System;

namespace ObenFramework.Economy;

/// <summary>
/// Balance arithmetic for bank accounts, without Unity types. Accounts keep coins as whole numbers and Real
/// Money as a float the game rounds to two decimals (interest does that), unlike the wallet, which keeps tenths.
/// </summary>
internal static class AccountMath
{
    /// <summary>Largest single amount the framework moves in or out of an account.</summary>
    public const float MaxAmount = 100000000f;

    // A balance read back from float maths can be off by a hair; never refuse a debit over that.
    private const float Tolerance = 0.0001f;

    /// <summary>An amount as the account would store it (whole coins, Real Money to two decimals), or false when it is not a usable amount.</summary>
    public static bool TryNormalize(bool realMoney, float amount, out float value)
    {
        value = 0f;
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f || amount > MaxAmount)
            return false;
        value = realMoney ? (float)Math.Round(amount, 2) : (float)Math.Floor(amount);
        return value > 0f;
    }

    /// <summary>True when the balance covers the amount.</summary>
    public static bool Covers(bool realMoney, int coins, float rm, float amount) =>
        realMoney ? !float.IsNaN(rm) && rm + Tolerance >= amount : coins >= (long)amount;

    /// <summary>A Real Money balance after taking <paramref name="amount"/>, rounded the way the game rounds it and never below zero.</summary>
    public static float DebitRm(float rm, float amount) => Math.Max(0f, (float)Math.Round(rm - amount, 2));

    /// <summary>A Real Money balance after adding <paramref name="amount"/>.</summary>
    public static float CreditRm(float rm, float amount) => (float)Math.Round(rm + amount, 2);

    /// <summary>True when adding <paramref name="amount"/> coins keeps the balance a valid whole number.</summary>
    public static bool CoinsFit(int coins, float amount) => (long)coins + (long)amount <= int.MaxValue;

    /// <summary>
    /// What the wallet, which holds tenths, pays to clear a Real Money debt that may have hundredths: the debt
    /// rounded up to the next tenth (0.97 gives 1.0, 0.9 stays 0.9), so a debt is never cleared for less than it is.
    /// </summary>
    public static float WalletTenths(float debt)
    {
        if (!(debt > 0f))
            return 0f;
        return (float)(Math.Ceiling(Math.Round(debt * 10.0, 4)) / 10.0);
    }
}
