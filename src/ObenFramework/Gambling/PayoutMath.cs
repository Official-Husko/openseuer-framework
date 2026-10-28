// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Gambling;

/// <summary>Payout sums of the gambling games, without Unity types.</summary>
internal static class PayoutMath
{
    /// <summary>Largest single payout after scaling.</summary>
    public const int MaxPayout = 100000000;

    /// <summary>What a card table pays for each finishing place, as a multiple of the stake (1st to 4th).</summary>
    private static readonly float[] CardPlaces = { 2f, 1.2f, 0.8f, 0f };

    /// <summary>A whole payout times a factor, rounded half to even like the game's <c>Mathf.RoundToInt</c>, and capped.</summary>
    public static int Scale(int amount, float factor)
    {
        if (amount <= 0 || factor == 1f)
            return amount;
        if (float.IsNaN(factor) || factor <= 0f)
            return 0;
        double scaled = Math.Round(amount * (double)factor);
        return scaled >= MaxPayout ? MaxPayout : (int)scaled;
    }

    /// <summary>A Real Money payout times a factor, kept to tenths like the game's money sums.</summary>
    public static float ScaleTenths(float amount, float factor)
    {
        if (amount <= 0f || factor == 1f)
            return amount;
        if (float.IsNaN(factor) || factor <= 0f)
            return 0f;
        double scaled = Math.Round(amount * (double)factor, 1);
        return scaled >= MaxPayout ? MaxPayout : (float)scaled;
    }

    /// <summary>What a card table pays back for a place (1 to 4), as the game works it out; -1 for a place it has no rate for.</summary>
    public static int CardPayout(int stake, int placement)
    {
        if (placement < 1 || placement > CardPlaces.Length)
            return -1;
        return (int)Math.Round(stake * (double)CardPlaces[placement - 1]);
    }

    /// <summary>The combined factor of a game: every mod's factor for all games times every mod's for this one, capped.</summary>
    public static float Combine(float allGames, float thisGame, float max)
    {
        float product = allGames * thisGame;
        return product > max ? max : product;
    }
}
