// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Economy;

/// <summary>
/// The sums of the game's <c>Bank.Exchange</c>, worked out the same way without Unity types, so a quote is
/// exactly what the exchange will pay. Its quirks are kept on purpose: the amount is capped by the supply of
/// the currency the player gets (even though the units differ), the credit is truncated to whole units, and
/// the Real Money to coin rate divides two whole numbers, so a bank whose coin supply is under its daily
/// spending pays nothing at all for Real Money.
/// </summary>
internal static class BankExchangeMath
{
    /// <summary>Real Money paid per coin, lowered while the bank's Real Money runs short.</summary>
    public static float RateToRm(float coinToRmRatio, float rmSupply, int dailyRmSpending) =>
        rmSupply < dailyRmSpending ? coinToRmRatio * (rmSupply / dailyRmSpending) : coinToRmRatio;

    /// <summary>Coins paid per Real Money. The game's integer division makes this 0 whenever the coin supply is under the daily spending.</summary>
    public static float RateToCoins(float rmToCoinRatio, int coinSupply, int dailyCoinSpending) =>
        coinSupply < dailyCoinSpending ? rmToCoinRatio * (float)(coinSupply / dailyCoinSpending) : rmToCoinRatio;

    /// <summary>How much the bank takes: the amount, capped by what the player has and by the bank's supply.</summary>
    public static float Taken(float amount, float wallet, float supply)
    {
        float taken = amount;
        if (taken > wallet)
            taken = wallet;
        if (taken > supply)
            taken = supply;
        return taken < 0f ? 0f : taken;
    }

    /// <summary>What the player gets, in whole units of the other currency.</summary>
    public static int Credit(float amount, float wallet, float supply, float rate) =>
        (int)(Taken(amount, wallet, supply) * rate);

    /// <summary>True when the bank would take the whole amount rather than quietly less.</summary>
    public static bool WithinLimits(float amount, float wallet, float supply) =>
        amount > 0f && amount <= wallet && amount <= supply;
}
